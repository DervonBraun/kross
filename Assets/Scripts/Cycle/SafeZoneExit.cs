using AN_;
using Player;
using UnityEngine;

namespace Cycle
{
    /// <summary>
    /// Объект в сейфзоне (кровать, терминал).
    ///
    /// • Idle          → BeginCycle()          — первый запуск
    /// • Active        → TryCompleteCycle()    — без кода (штраф ОКТС), КД должен истечь
    /// • CodeSubmitted → TryCompleteCycle()    — с кодом (без штрафа), КД должен истечь
    /// • Остальные фазы — недоступно
    ///
    /// CanInteractDefault возвращает true только когда КД истёк.
    /// </summary>
    public sealed class SafeZoneExit : MonoBehaviour, IInteractableDefault
    {
        [Header("Refs")]
        [SerializeField] private CycleManager    _cycle;
        [SerializeField] private NotificationBus _notify;

        // ─────────────────────────────────────────────────────────────────────
        #region IInteractableDefault

        public bool CanInteractDefault(PlayerContext context)
        {
            if (_cycle == null) return false;

            // Первый старт — всегда доступно
            if (_cycle.CurrentPhase == CyclePhase.Idle) return true;

            // Завершение — только после КД
            return _cycle.CanExit;
        }

        public void InteractDefault(PlayerContext context)
        {
            if (_cycle == null)
            {
                Debug.LogError("[SafeZoneExit] CycleManager не назначен!", this);
                return;
            }

            switch (_cycle.CurrentPhase)
            {
                case CyclePhase.Idle:
                    Debug.Log("[SafeZoneExit] Первый старт.");
                    _cycle.BeginCycle();
                    break;

                case CyclePhase.Active:
                case CyclePhase.CodeSubmitted:
                    // CanInteractDefault уже проверил КД;
                    // если вдруг вызов прямой — TryCompleteCycle сам выдаст предупреждение
                    _cycle.TryCompleteCycle();
                    break;

                case CyclePhase.Ending:
                    Debug.Log("[SafeZoneExit] Цикл уже завершается.");
                    break;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_cycle == null) _cycle = GetComponentInParent<CycleManager>();
        }
#endif
    }
}