using AN_;
using Player;
using Tasks;
using UnityEngine;

namespace Cycle
{
    /// <summary>
    /// Точка сдачи кода.
    ///
    /// • При старте цикла (Active) — открывается окно сдачи на submitWindowDuration секунд.
    /// • Игрок взаимодействует → код списывается → Defend-эффект выдаётся из CycleDef →
    ///   CycleManager.SubmitCode() → точка переходит в Used (объект не деактивируется).
    /// • Если таймер истёк раньше → Expired, взаимодействие заблокировано.
    ///
    /// TimeRemaining и State видны в инспекторе в Runtime для отладки.
    /// Для UI-таймера подписывайся на событие StateChanged или читай TimeRemaining.
    /// </summary>
    public sealed class CodeSubmitPoint : MonoBehaviour, IInteractableDefault
    {
        [Header("Refs")]
        [SerializeField] private CycleManager    _cycle;
        [SerializeField] private GameState       _state;
        [SerializeField] private NotificationBus _notify;

        // ── Состояния точки ──────────────────────────────────────────────────
        public enum PointState { Inactive, Available, Used, Expired }

        [Header("Runtime (read-only)")]
        [SerializeField, ReadOnly] private PointState _state2;    // видно в инспекторе
        [SerializeField, ReadOnly] private float      _timeRemaining;

        public PointState State         => _state2;
        public float      TimeRemaining => _timeRemaining;

        /// <summary> Подписывайся для обновления визуала и UI-таймера. </summary>
        public event System.Action<PointState> StateChanged;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void OnEnable()
        {
            if (_cycle != null) _cycle.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (_cycle != null) _cycle.PhaseChanged -= OnPhaseChanged;
        }

        private void Update()
        {
            if (_state2 != PointState.Available) return;

            _timeRemaining -= Time.deltaTime;

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                ExpireWindow();
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region IInteractableDefault

        public bool CanInteractDefault(PlayerContext context)
        {
            if (_cycle == null || _state == null)  return false;
            if (_state2 != PointState.Available)   return false;
            return _state.NormalCodes >= 1;
        }

        public void InteractDefault(PlayerContext context)
        {
            if (!CanInteractDefault(context))
            {
                _notify?.Push(NotifyType.Warning, "Точка сдачи", GetFailReason());
                Debug.LogWarning($"[CodeSubmitPoint] Заблокировано: {GetFailReason()}");
                return;
            }

            // 1. Списываем код
            if (!_state.TryConsumeNormalCode(1))
            {
                _notify?.Push(NotifyType.Warning, "Точка сдачи", "Нет кода для сдачи.");
                return;
            }

            // 2. Выдаём Defend-эффект (длительность из SO-эффекта, не из таймера)
            var defend = _cycle.DefendEffect;
            if (defend != null)
                context.EffectManager.Add(defend);
            else
                Debug.LogWarning("[CodeSubmitPoint] CycleDef.defendEffect не назначен.", this);

            // 3. Останавливаем таймер
            _timeRemaining = 0f;

            // 4. Сообщаем CycleManager
            _cycle.SubmitCode();

            // 5. Блокируем без деактивации
            SetState(PointState.Used);

            _notify?.Push(NotifyType.Info, "Точка сдачи", "Код принят. Возвращайтесь домой.");
            Debug.Log("[CodeSubmitPoint] Код сдан.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Private

        private void OnPhaseChanged(CyclePhase phase)
        {
            switch (phase)
            {
                case CyclePhase.Active:
                    OpenWindow();
                    break;

                case CyclePhase.Idle:
                case CyclePhase.Ending:
                    _timeRemaining = 0f;
                    SetState(PointState.Inactive);
                    break;
                // CodeSubmitted — точка уже Used, не трогаем
            }
        }

        private void OpenWindow()
        {
            float duration = _cycle?.Def != null ? _cycle.Def.submitWindowDuration : 300f;
            _timeRemaining = duration;
            SetState(PointState.Available);
            Debug.Log($"[CodeSubmitPoint] Окно открыто: {duration}с.");
        }

        private void ExpireWindow()
        {
            if (_state2 != PointState.Available) return;

            SetState(PointState.Expired);
            _cycle?.OnSubmitWindowExpired();
            _notify?.Push(NotifyType.Warning, "Точка сдачи", "Время вышло — окно закрыто.");
            Debug.Log("[CodeSubmitPoint] Окно истекло.");
        }

        private void SetState(PointState s)
        {
            _state2 = s;
            StateChanged?.Invoke(s);
        }

        private string GetFailReason()
        {
            if (_cycle == null)                   return "CycleManager не назначен.";
            if (_state == null)                   return "GameState не назначен.";
            if (_state2 == PointState.Used)       return "Код уже сдан в этом цикле.";
            if (_state2 == PointState.Expired)    return "Окно сдачи истекло.";
            if (_state2 == PointState.Inactive)   return "Цикл не активен.";
            if (_state.NormalCodes < 1)           return "Нет кода для сдачи.";
            return "Неизвестная ошибка.";
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_state == null) _state = GetComponentInParent<GameState>();
            if (_cycle == null) _cycle = FindFirstObjectByType<CycleManager>();
        }
#endif
    }
}