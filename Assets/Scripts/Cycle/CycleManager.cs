using System;
using AN_;
using Player.EffectSystem;
using Tasks;
using UnityEngine;

namespace Cycle
{
    public enum CyclePhase
    {
        Idle,           // До первого старта
        Active,         // Цикл идёт: задания, генерация кода
        CodeSubmitted,  // Код сдан в точке → Defend выдан → идём в сейфзону
        Ending          // Финализация (один кадр)
    }

    /// <summary>
    /// Оркестратор игрового цикла.
    ///
    /// Поток (успех):  BeginCycle → Active → [CodeSubmitPoint] → CodeSubmitted → [SafeZoneExit] → Ending → BeginCycle
    /// Поток (штраф):  BeginCycle → Active → [SafeZoneExit после КД, без кода] → Ending → BeginCycle + ОКТС++
    ///
    /// КД сейфзоны отсчитывается с BeginCycle().
    /// Heat сбрасывается всегда при финализации цикла.
    /// </summary>
    public sealed class CycleManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private CycleDef _def;

        [Header("Refs")]
        [SerializeField] private GameState     _state;
        [SerializeField] private HeatRegistry  _heat;
        [SerializeField] private NotificationBus _notify;

        // ── События ──────────────────────────────────────────────────────────
        public event Action<CyclePhase> PhaseChanged;
        public event Action<int>        OktsStageChanged;
        public event Action             CycleCompleted;     // новый цикл вот-вот начнётся

        // ── Публичное состояние ───────────────────────────────────────────────
        public CyclePhase       CurrentPhase  { get; private set; } = CyclePhase.Idle;
        public int              CycleNumber   { get; private set; } = 0;
        public CycleDef         Def           => _def;
        public EffectDefinition DefendEffect  => _def != null ? _def.defendEffect : null;

        /// <summary>
        /// Сколько секунд осталось до разблокировки сейфзоны.
        /// 0 — уже можно завершить цикл.
        /// </summary>
        public float ExitCooldownRemaining { get; private set; }

        /// <summary> true — завершить цикл через SafeZoneExit разрешено. </summary>
        public bool CanExit => CurrentPhase is CyclePhase.Active or CyclePhase.CodeSubmitted
                               && ExitCooldownRemaining <= 0f;

        // ── Приватное ─────────────────────────────────────────────────────────
        private bool _cooldownRunning;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Update()
        {
            if (!_cooldownRunning) return;

            ExitCooldownRemaining -= Time.deltaTime;

            if (ExitCooldownRemaining <= 0f)
            {
                ExitCooldownRemaining = 0f;
                _cooldownRunning      = false;
                _notify?.Push(NotifyType.Info, "Сейфзона", "Можно завершить цикл.");
                Debug.Log("[CycleManager] КД сейфзоны истёк — выход разрешён.");
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        public void BeginCycle()
        {
            if (_def == null)
            {
                Debug.LogError("[CycleManager] CycleDef не назначен!", this);
                return;
            }

            CycleNumber++;

            // Запускаем КД сейфзоны
            ExitCooldownRemaining = _def.exitCooldownDuration;
            _cooldownRunning      = _def.exitCooldownDuration > 0f;

            SetPhase(CyclePhase.Active);

            _notify?.Push(NotifyType.Info, "Цикл",
                $"Цикл #{CycleNumber} начался. Выход через {_def.exitCooldownDuration}с.");
            Debug.Log($"[CycleManager] ── Цикл #{CycleNumber} ── exitCD={_def.exitCooldownDuration}с");
        }

        /// <summary> Вызывается из CodeSubmitPoint после сдачи кода. </summary>
        public bool SubmitCode()
        {
            if (CurrentPhase != CyclePhase.Active)
            {
                Debug.LogWarning($"[CycleManager] SubmitCode в фазе {CurrentPhase}.");
                return false;
            }

            SetPhase(CyclePhase.CodeSubmitted);
            _notify?.Push(NotifyType.Info, "Цикл", "Код сдан. Возвращайтесь в сейфзону.");
            Debug.Log("[CycleManager] Код сдан → CodeSubmitted.");
            return true;
        }

        /// <summary>
        /// Вызывается из CodeSubmitPoint, когда окно сдачи истекло.
        /// Точка уже заблокирована — просто фиксируем факт.
        /// </summary>
        public void OnSubmitWindowExpired()
        {
            // Окно истекло — ничего критичного для CycleManager,
            // игрок просто не сможет больше сдать код в этом цикле.
            // Штраф ОКТС будет при TryCompleteCycle() если CodeSubmitted так и не наступил.
            _notify?.Push(NotifyType.Warning, "Точка сдачи", "Окно сдачи истекло.");
            Debug.Log("[CycleManager] Окно сдачи истекло. Фаза осталась: " + CurrentPhase);
        }

        /// <summary>
        /// Вызывается из SafeZoneExit.
        /// Работает в фазах Active (без кода, штраф) и CodeSubmitted (с кодом, ок).
        /// </summary>
        public void TryCompleteCycle()
        {
            if (!CanExit)
            {
                if (ExitCooldownRemaining > 0f)
                    _notify?.Push(NotifyType.Warning, "Сейфзона",
                        $"Нельзя выйти ещё {ExitCooldownRemaining:F0}с.");
                else
                    _notify?.Push(NotifyType.Warning, "Сейфзона", "Нельзя завершить цикл сейчас.");

                Debug.LogWarning($"[CycleManager] TryCompleteCycle заблокирован. Phase={CurrentPhase}, CD={ExitCooldownRemaining:F1}");
                return;
            }

            _cooldownRunning = false;
            SetPhase(CyclePhase.Ending);
            FinalizeCycle();
        }

        public void ForceReset()
        {
            _cooldownRunning      = false;
            ExitCooldownRemaining = 0f;
            SetPhase(CyclePhase.Idle);
            Debug.Log("[CycleManager] Принудительный сброс.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Private

        private void FinalizeCycle()
        {
            bool codeWasSubmitted = CurrentPhase == CyclePhase.Ending
                                    && PreviousPhaseWasCodeSubmitted();

            // Проверяем Defend-эффект (актуально когда код был сдан)
            bool hasDefend = _state != null && _def != null
                             && _state.HasEffectTag(_def.defendEffectTag);

            bool penalty = !hasDefend;

            if (penalty)
            {
                int newStage = IncrementOktsStage();
                Debug.Log($"[CycleManager] Штраф: OktsStage → {newStage}");
                _notify?.Push(NotifyType.Warning, "ОКТС", $"Фаза ОКТС повышена до {newStage}!");

                if (CheckGameOver(newStage)) return;
            }
            else
            {
                _notify?.Push(NotifyType.Info, "Цикл", "Цикл завершён. Defend сохранён — штрафа нет.");
                Debug.Log("[CycleManager] Цикл без штрафа.");
            }

            // Heat сбрасывается всегда
            if (_heat != null)
            {
                _heat.ResetAll();
                Debug.Log("[CycleManager] HeatRegistry сброшен.");
            }

            CycleCompleted?.Invoke();
            BeginCycle();
        }

        // Отслеживаем предыдущую фазу, чтобы знать был ли код сдан
        private CyclePhase _phaseBeforeEnding;

        private bool PreviousPhaseWasCodeSubmitted()
            => _phaseBeforeEnding == CyclePhase.CodeSubmitted;

        private bool CheckGameOver(int stage)
        {
            if (_def == null || stage < _def.maxOktsStage) return false;

            // TODO: настоящий Game Over
            Debug.Log($"[CycleManager] ОКТС {stage} — Game Over (заглушка).");
            _notify?.Push(NotifyType.Error, "ОКТС", $"Стадия {stage}: конец игры (заглушка).");

            if (_heat != null) _heat.ResetAll();
            SetPhase(CyclePhase.Idle);
            return true;
        }

        private int IncrementOktsStage()
        {
            if (_state == null) return 0;
            _state.IncrementOktsStage();
            int s = _state.OktsStage;
            OktsStageChanged?.Invoke(s);
            return s;
        }

        private void SetPhase(CyclePhase phase)
        {
            _phaseBeforeEnding = CurrentPhase;   // запоминаем перед сменой
            CurrentPhase       = phase;
            PhaseChanged?.Invoke(phase);
            Debug.Log($"[CycleManager] Phase → {phase}");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_state == null) _state = GetComponent<GameState>();
            if (_heat  == null) _heat  = GetComponent<HeatRegistry>();
        }
#endif
    }
}