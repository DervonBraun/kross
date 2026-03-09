using System;
using System.Threading;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Центральный координатор UI-системы. Реализует IPageFlowBus — единственный публичный API
    /// для gameplay-слоя.
    ///
    /// Три ключевых поля:
    ///   currentFlow     — активный FlowRunner (null между переходами)
    ///   activePage      — id открытой страницы (None между переходами)
    ///   pendingRequest  — ровно один ожидающий PageRequest (не очередь — один слот)
    ///
    /// Правило: gameplay-код обращается только к IPageFlowBus.
    /// Прямые вызовы WebUIManager из MonoBehaviour — антипаттерн.
    /// </summary>
    public sealed class WebUIManager : MonoBehaviour, IPageFlowBus
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Refs")]
        [SerializeField] private PageFlowDefinition[] _autoRegisterFlows;

        // ─── Runtime state ────────────────────────────────────────────────────

        [Header("Debug (ReadOnly)")]
        [SerializeField, global::ReadOnly] private string _debugActivePage    = "<none>";
        [SerializeField, global::ReadOnly] private string _debugPendingPage   = "<none>";
        [SerializeField, global::ReadOnly] private string _debugCurrentPhase  = "Idle";

        private FlowRunner    _currentFlow;
        private PageId        _activePage     = PageId.None;
        private PageRequest   _pendingRequest;
        private bool          _flowInProgress;

        /// <summary>True пока открывается или закрывается любой flow.</summary>
        public bool IsFlowInProgress => _flowInProgress;

        // Инициализируем inline — готовы до первого Awake любого другого компонента
        private readonly PageRegistry        _registry = new();
        private readonly SimpleServiceLocator _services = new();
        private readonly PageHistory         _history  = new();

        // Страница которую сейчас открываем (до SetActivePageStep) или уже открыта
        private PageId _intendedPage = PageId.None;

        public event Action<PageId> PageOpened;
        public event Action<PageId> PageClosed;

        // ══════════════════════════════════════════════════════════════════════
        // Setup
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Авторегистрация flow-ассетов из Inspector
            if (_autoRegisterFlows != null)
            {
                foreach (var def in _autoRegisterFlows)
                    _registry.Register(def);
            }

            // Авторегистрация всех IPageView на сцене —
            // избавляет каждый View от необходимости делать FindAnyObjectByType самостоятельно.
            // Вызываем через Start чтобы дать время всем Awake'ам завершиться.
        }

        private void Start()
        {
            ScanAndRegisterViews();
        }

        /// <summary>
        /// Находит все IPageView на сцене и регистрирует их.
        /// Вызывается в Start — после всех Awake.
        /// Можно вызвать повторно если view добавляется динамически.
        /// </summary>
        public void ScanAndRegisterViews()
        {
            // FindObjectsInactive — находит в том числе неактивные GameObject
            var views = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var mb in views)
            {
                if (mb is IPageView view)
                    _registry.RegisterView(view);
            }
        }

        /// <summary>Внешняя регистрация (из кода или AutoRegister-компонентов).</summary>
        public void Register(PageFlowDefinition def)                         => _registry.Register(def);
        public void RegisterView(IPageView view)                             => _registry.RegisterView(view);
        public void RegisterProvider(PageId id, IPageDataProvider provider)  => _registry.RegisterProvider(id, provider);

        /// <summary>Доступ к реестру для AutoRegister-компонентов.</summary>
        public PageRegistry Registry => _registry;

        /// <summary>Доступ к сервис-локатору для Bootstrap-компонента.</summary>
        public SimpleServiceLocator GetServiceLocator() => _services;

        // ══════════════════════════════════════════════════════════════════════
        // IPageFlowBus
        // ══════════════════════════════════════════════════════════════════════

        public void OpenPage<TPayload>(PageId pageId, TPayload payload,
            PageOpenFlags flags    = PageOpenFlags.None,
            int           priority = 0)
        {
            var req = BuildRequest(pageId, payload, typeof(TPayload), flags, priority);
            HandleOpenRequest(req);
        }

        public void OpenPage(PageId pageId,
            PageOpenFlags flags    = PageOpenFlags.None,
            int           priority = 0)
        {
            var req = BuildRequest(pageId, null, null, flags, priority);
            HandleOpenRequest(req);
        }

        public void ClosePage(PageId pageId)
        {
            if (_activePage != pageId && _intendedPage != pageId)
            {
                Debug.LogWarning($"[WebUIManager] ClosePage '{pageId}' called but active='{_activePage}', intended='{_intendedPage}'.");
                return;
            }
            StartCloseFlow(pageId);
        }

        public bool IsPageOpen(PageId pageId) => _activePage == pageId || _intendedPage == pageId;

        // ══════════════════════════════════════════════════════════════════════
        // Internal — request handling
        // ══════════════════════════════════════════════════════════════════════

        private void HandleOpenRequest(PageRequest req)
        {
            if (!_registry.TryGetFlow(req.PageId, out var def))
            {
                Debug.LogError($"[WebUIManager] Page '{req.PageId}' is not registered.");
                return;
            }

            // Если нет активной страницы И нет текущего flow — запускаем сразу
            if (!_flowInProgress && _activePage.IsEmpty)
            {
                StartOpenFlow(req, def);
                return;
            }

            // ── Разрешение конфликта ──
            ResolveConflict(req, def);
        }

        private void ResolveConflict(PageRequest incoming, PageFlowDefinition incomingDef)
        {
            // Политика определяется ВХОДЯЩЕЙ страницей
            switch (incomingDef.ConflictPolicy)
            {
                case NavigationPolicy.Reject:
                    Debug.Log($"[WebUIManager] Request for '{incoming.PageId}' rejected (active: '{_activePage}').");
                    break;

                case NavigationPolicy.Queue:
                    TrySetPending(incoming);
                    break;

                case NavigationPolicy.Interrupt:
                    _currentFlow?.Cancel();
                    // После отмены — поток сам вызовет OnFlowCompleted → pending будет подхвачен
                    TrySetPending(incoming);
                    break;

                case NavigationPolicy.ReplaceAfterClose:
                    TrySetPending(incoming);
                    // Запускаем close для активной страницы (если ещё не закрывается)
                    if (!_activePage.IsEmpty)
                        StartCloseFlow(_activePage);
                    break;
            }
        }

        private void TrySetPending(PageRequest incoming)
        {
            // Single pending slot: заменяем только если приоритет выше
            if (_pendingRequest == null || incoming.Priority >= _pendingRequest.Priority)
            {
                _pendingRequest     = incoming;
                _debugPendingPage   = incoming.PageId.ToString();
            }
            else
            {
                Debug.Log($"[WebUIManager] Pending slot occupied with higher priority. '{incoming.PageId}' dropped.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Internal — flow execution
        // ══════════════════════════════════════════════════════════════════════

        private async void StartOpenFlow(PageRequest req, PageFlowDefinition def)
        {
            _flowInProgress    = true;
            _intendedPage      = req.PageId; // сразу помечаем — IsPageOpen вернёт true
            _debugCurrentPhase = "Opening";

            var ctx = await BuildContext(req, FlowPhase.Opening);
            if (ctx == null)
            {
                _flowInProgress = false;
                return;
            }

            _currentFlow           = new FlowRunner();
            _currentFlow.Completed += OnOpenFlowCompleted;

            await _currentFlow.RunAsync(def, ctx);
        }

        private async void StartCloseFlow(PageId pageId)
        {
            if (!_registry.TryGetFlow(pageId, out var def)) return;

            _flowInProgress    = true;
            _debugCurrentPhase = "Closing";

            var req = new PageRequest(pageId, policy: def.ConflictPolicy);
            var ctx = new PageContext(req, CancellationToken.None, _services, _history);
            ctx.Phase = FlowPhase.Closing;

            _currentFlow           = new FlowRunner();
            _currentFlow.Completed += OnCloseFlowCompleted;

            await _currentFlow.RunAsync(def, ctx);
        }

        private void OnOpenFlowCompleted(FlowRunner runner)
        {
            runner.Completed -= OnOpenFlowCompleted;
            _currentFlow       = null;
            _flowInProgress    = false;
            _debugCurrentPhase = "Open";

            ProcessPending();
        }

        private void OnCloseFlowCompleted(FlowRunner runner)
        {
            runner.Completed -= OnCloseFlowCompleted;
            _currentFlow       = null;
            _flowInProgress    = false;

            // Сохраняем id до сброса — подписчики (IdleTimerService) должны получить реальный id
            var closedPage     = _activePage.IsEmpty ? _intendedPage : _activePage;

            _activePage        = PageId.None;
            _intendedPage      = PageId.None;
            _debugActivePage   = "<none>";
            _debugCurrentPhase = "Idle";

            PageClosed?.Invoke(closedPage);

            ProcessPending();
        }

        private void ProcessPending()
        {
            if (_pendingRequest == null) return;

            var req             = _pendingRequest;
            _pendingRequest     = null;
            _debugPendingPage   = "<none>";

            if (!_registry.TryGetFlow(req.PageId, out var def))
            {
                Debug.LogWarning($"[WebUIManager] Pending page '{req.PageId}' no longer registered — dropped.");
                return;
            }

            StartOpenFlow(req, def);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Internal — SetActivePage / ClearActivePage (вызываются из шагов)
        // ══════════════════════════════════════════════════════════════════════

        internal void SetActivePage(PageId pageId)
        {
            _activePage         = pageId;
            _debugActivePage    = pageId.ToString();
            _history.MarkOpened(pageId);
            PageOpened?.Invoke(pageId);
        }

        internal void ClearActivePage()
        {
            _activePage        = PageId.None;
            _debugActivePage   = "<none>";
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════

        private PageRequest BuildRequest(PageId pageId, object payload, Type payloadType,
            PageOpenFlags flags, int priority)
        {
            _registry.TryGetFlow(pageId, out var def);
            var policy = def?.ConflictPolicy ?? NavigationPolicy.Reject;

            return new PageRequest(pageId, payload, payloadType, policy, flags, priority);
        }

        private async Awaitable<PageContext> BuildContext(PageRequest req, FlowPhase phase)
        {
            object model = null;

            if (_registry.TryGetProvider(req.PageId, out var provider))
            {
                try
                {
                    model = await provider.ProvideAsync(req, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WebUIManager] IPageDataProvider failed for '{req.PageId}': {ex}");
                    return null;
                }
            }

            var ctx   = new PageContext(req, CancellationToken.None, _services, _history);
            ctx.Phase = phase;
            ctx.Model = model;
            return ctx;
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Обновим debug-поля если они пусты (первый запуск)
            if (string.IsNullOrEmpty(_debugActivePage))  _debugActivePage  = "<none>";
            if (string.IsNullOrEmpty(_debugPendingPage)) _debugPendingPage = "<none>";
            if (string.IsNullOrEmpty(_debugCurrentPhase)) _debugCurrentPhase = "Idle";
        }
#endif
    }
}