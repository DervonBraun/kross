using System.Collections;
using System.Threading;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Flow.Inventory
{
    /// <summary>
    /// Инвентарь, интегрированный в Pseudo-Web UI Flow Architecture.
    ///
    /// Реализует:
    ///   IPageView        — ShowInstant / HideInstant (мгновенно, без анимации)
    ///   IAnimatableView  — PlayAnimationAsync("open") / ("close") → используется FlowRunner'ом
    ///
    /// Что изменилось по сравнению с оригиналом:
    ///   - Убрана подписка на PlayerContext.ModeChanged — теперь открытием управляет WebUIManager
    ///   - Добавлены ShowInstant / HideInstant / PlayAnimationAsync
    ///   - Вся анимационная логика (корутины, DOTween sequences) — без изменений
    ///
    /// Иерархия:
    ///   Canvas
    ///   ├── InventoryView   ← этот компонент
    ///   ├── InventoryRoot   ← _inventoryRoot
    ///   │   └── ContentGroup
    ///   └── ...
    ///
    /// PageFlowDefinition (OpenSteps):
    ///   1. LockInputStep
    ///   2. ShowViewStep          (ViewId = "Inventory")     ← вызовет ShowInstant → root.SetActive(true)
    ///   3. PlayAnimationStep     (ViewId = "Inventory", Key = "open")
    ///   4. SetActivePageStep
    ///   5. UnlockInputStep
    ///
    /// PageFlowDefinition (CloseSteps):
    ///   1. LockInputStep
    ///   2. PlayAnimationStep     (ViewId = "Inventory", Key = "close")
    ///   3. HideViewStep          (ViewId = "Inventory")     ← вызовет HideInstant → root.SetActive(false)
    ///   4. ClearActivePageStep
    ///   5. UnlockInputStep
    /// </summary>
    public sealed class InventoryView : MonoBehaviour, IPageView, IAnimatableView
    {
        // ── IPageView ─────────────────────────────────────────────────────────

        [Header("Flow")]
        [SerializeField] private PageId _viewId = new PageId("Inventory");
        public PageId ViewId => _viewId;

        // ── UI Refs ───────────────────────────────────────────────────────────

        [Header("UI Root")]
        [SerializeField] private CanvasGroup  _rootGroup;    // CanvasGroup на самом root объекте
        [SerializeField] private CanvasGroup  _contentGroup;
        [SerializeField] private GameObject   _firstSelected;

        [Header("Strips")]
        [Tooltip("Родитель полосок — дочерние RectTransform собираются автоматически.")]
        [SerializeField] private RectTransform   _stripsParent;
        [SerializeField] private RectTransform[] _strips;

        [Header("Strip Animation")]
        [SerializeField, Min(0.01f)] private float _openDur      = 0.30f;
        [SerializeField, Min(0.01f)] private float _closeDur     = 0.22f;
        [SerializeField]             private Ease  _openEase     = Ease.OutCubic;
        [SerializeField]             private Ease  _closeEase    = Ease.InCubic;
        [SerializeField, Min(0f)]    private float _openStagger  = 0.05f;
        [SerializeField, Min(0f)]    private float _closeStagger = 0.04f;

        [Header("Content Animation")]
        [SerializeField, Min(0.01f)] private float _fadeInDur   = 0.20f;
        [SerializeField, Min(0.01f)] private float _fadeOutDur  = 0.15f;
        [SerializeField]             private Ease  _fadeInEase  = Ease.OutQuad;
        [SerializeField]             private Ease  _fadeOutEase = Ease.InQuad;

        [Header("Strip Duration Randomness")]
        [SerializeField, Min(0.01f)] private float _multiplierMin = 0.85f;
        [SerializeField, Min(0.01f)] private float _multiplierMax = 1.15f;

        // ── Internal ──────────────────────────────────────────────────────────

        private Sequence _seq;

        // ══════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_rootGroup == null) _rootGroup = GetComponent<CanvasGroup>();
            RefreshStrips();
            HideInstant();
        }

        // ══════════════════════════════════════════════════════════════════════
        // IPageView
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается FlowRunner'ом перед анимацией открытия (ShowViewStep).
        /// Активирует root, сбрасывает состояние — всё готово к анимации.
        /// </summary>
        public void ShowInstant()
        {
            // Убиваем любую текущую анимацию и сбрасываем позиции
            _seq?.Kill(complete: false);
            StopAllCoroutines();

            if (_rootGroup != null)
            {
                _rootGroup.alpha          = 1f;
                _rootGroup.interactable   = true;
                _rootGroup.blocksRaycasts = true;
            }
            SetContentAlpha(0f);
            RefreshStrips();

            // ForceUpdateCanvases нужен чтобы rect.width был актуален до PlaceStripsOffscreenLeft
            Canvas.ForceUpdateCanvases();
            PlaceStripsOffscreenLeft();
        }

        /// <summary>
        /// Вызывается FlowRunner'ом после анимации закрытия (HideViewStep).
        /// Скрывает через CanvasGroup — не деактивирует GameObject.
        /// </summary>
        public void HideInstant()
        {
            _seq?.Kill();
            StopAllCoroutines();
            if (_rootGroup != null)
            {
                _rootGroup.alpha          = 0f;
                _rootGroup.interactable   = false;
                _rootGroup.blocksRaycasts = false;
            }
            SetContentAlpha(0f);
            PlaceStripsOffscreenLeft();
        }

        // ══════════════════════════════════════════════════════════════════════
        // IAnimatableView
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Запускает именованную анимацию и ждёт завершения.
        /// Поддерживаемые ключи: "open", "close".
        /// Отмена по CancellationToken — прыгает к финальному состоянию.
        /// </summary>
        public async Awaitable PlayAnimationAsync(string key, CancellationToken ct)
        {
            switch (key)
            {
                case "open":
                    await AwaitableUtils.RunCoroutineAsync(this, OpenAnimRoutine(ct), ct,
                        onCancel: SnapToFinalState);
                    break;
                case "close":
                    await AwaitableUtils.RunCoroutineAsync(this, CloseAnimRoutine(ct), ct,
                        onCancel: () => { _seq?.Kill(); });
                    break;
                default:
                    Debug.LogWarning($"[InventoryView] Unknown animation key: '{key}'");
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Animation coroutines (оригинальная логика — без изменений по смыслу)
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator OpenAnimRoutine(CancellationToken ct)
        {
            // Layout уже сделан в ShowInstant, ещё один кадр для надёжности
            yield return null;

            _seq = BuildOpenStripsSeq();
            _seq.Play();
            yield return _seq.WaitForCompletion();

            if (ct.IsCancellationRequested) yield break;

            if (_contentGroup != null)
            {
                yield return _contentGroup
                    .DOFade(1f, _fadeInDur)
                    .SetEase(_fadeInEase)
                    .WaitForCompletion();

                if (!ct.IsCancellationRequested)
                {
                    _contentGroup.blocksRaycasts = true;
                    _contentGroup.interactable   = true;
                }
            }

            if (!ct.IsCancellationRequested)
            {
                var es = EventSystem.current;
                if (es != null)
                {
                    es.SetSelectedGameObject(null);
                    if (_firstSelected != null) es.SetSelectedGameObject(_firstSelected);
                }
            }
        }

        private IEnumerator CloseAnimRoutine(CancellationToken ct)
        {
            if (_contentGroup != null)
            {
                _contentGroup.blocksRaycasts = false;
                _contentGroup.interactable   = false;
                yield return _contentGroup
                    .DOFade(0f, _fadeOutDur)
                    .SetEase(_fadeOutEase)
                    .WaitForCompletion();
            }

            if (ct.IsCancellationRequested) yield break;

            EventSystem.current?.SetSelectedGameObject(null);

            _seq = BuildCloseStripsSeq();
            _seq.Play();
            yield return _seq.WaitForCompletion();
        }

        /// <summary>
        /// При отмене — доводим UI до финального видимого состояния.
        /// FlowRunner вызовет HideInstant() или SetActivePage() — они разберутся дальше.
        /// </summary>
        private void SnapToFinalState()
        {
            if (_contentGroup != null)
            {
                _contentGroup.alpha          = 1f;
                _contentGroup.blocksRaycasts = true;
                _contentGroup.interactable   = true;
            }

            if (_strips != null)
                foreach (var s in _strips)
                    if (s != null) { var p = s.anchoredPosition; p.x = 0f; s.anchoredPosition = p; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Strip sequences
        // ══════════════════════════════════════════════════════════════════════

        private Sequence BuildOpenStripsSeq()
        {
            var seq   = DOTween.Sequence();
            int count = _strips?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                var strip = _strips![i];
                if (strip == null) continue;
                seq.Insert(_openStagger * i,
                    strip.DOAnchorPosX(0f, _openDur * RandomMult()).SetEase(_openEase));
            }
            return seq;
        }

        private Sequence BuildCloseStripsSeq()
        {
            var   seq    = DOTween.Sequence();
            int   count  = _strips?.Length ?? 0;
            float target = _stripWidth > 1f ? _stripWidth : Screen.width;

            for (int i = 0; i < count; i++)
            {
                int ri    = count - 1 - i;
                var strip = _strips![ri];
                if (strip == null) continue;
                seq.Insert(_closeStagger * i,
                    strip.DOAnchorPosX(target, _closeDur * RandomMult()).SetEase(_closeEase));
            }
            return seq;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════

        public void RefreshStrips()
        {
            if (_stripsParent == null) return;
            var found = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < _stripsParent.childCount; i++)
                if (_stripsParent.GetChild(i) is RectTransform rt)
                    found.Add(rt);
            _strips = found.ToArray();
        }

        // Кэшируем ширину полоски — rect.width может быть 0 до первого layout rebuild
        private float _stripWidth = -1f;

        private void PlaceStripsOffscreenLeft()
        {
            if (_strips == null) return;

            // Обновляем кэш если rect уже посчитан
            foreach (var s in _strips)
            {
                if (s != null && s.rect.width > 1f)
                {
                    _stripWidth = s.rect.width;
                    break;
                }
            }

            // Fallback: берём ширину экрана если layout ещё не готов
            float offscreen = _stripWidth > 1f ? _stripWidth : Screen.width;

            foreach (var s in _strips)
            {
                if (s == null) continue;
                var p = s.anchoredPosition;
                p.x = -offscreen;
                s.anchoredPosition = p;
            }
        }

        private void SetContentAlpha(float a)
        {
            if (_contentGroup == null) return;
            _contentGroup.alpha          = a;
            _contentGroup.blocksRaycasts = false;
            _contentGroup.interactable   = false;
        }

        private float RandomMult() => Random.Range(
            Mathf.Min(_multiplierMin, _multiplierMax),
            Mathf.Max(_multiplierMin, _multiplierMax));

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_multiplierMin > _multiplierMax) _multiplierMax = _multiplierMin;
            RefreshStrips();
        }

        [ContextMenu("Refresh Strips")]
        private void RefreshStripsMenu()
        {
            RefreshStrips();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}