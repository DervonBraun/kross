using AN_;
using Player;
using UnityEngine;

namespace Tasks
{
    public sealed class GreenTokenHub : MonoBehaviour,
        IInteractableDefault,
        IInteractableAim,
        IInteractableAimHover,
        IInteractableAimExit,
        IThemeable
    {
        [Header("Refs")]
        [SerializeField] private GameState             _state;
        [SerializeField] private GreenTokenAccumulator _accumulator;
        [SerializeField] private NotificationBus       _bus;
        [SerializeField] private TaskHudTheme          _theme;

        [Header("Labels")]
        [SerializeField] private string _hubTitle = "Производство";

        // IThemeable
        public void SetTheme(TaskHudTheme theme) => _theme = theme;

        public bool CanInteractDefault(PlayerContext context)
            => _accumulator != null && _accumulator.PendingTokens > 0;

        public void InteractDefault(PlayerContext context)
        {
            if (_accumulator == null || _state == null) return;

            int amount = _accumulator.Collect();
            if (amount <= 0)
            {
                _bus?.Push(NotifyType.Info, _hubTitle, "Токены ещё не накоплены.");
                return;
            }

            _state.AddTokens(new TokenAmount { green = amount });
            _bus?.Push(NotifyType.Info, _hubTitle, $"+{amount}G получено");
            RefreshHud();
        }

        public bool CanInteractAim(PlayerContext context) => CanInteractDefault(context);
        public void InteractAim(PlayerContext context)    => InteractDefault(context);
        public void OnAimEnter(PlayerContext context)     => RefreshHud();
        public void OnAimExit()                           => TaskHUD.Instance?.Hide();

        private void RefreshHud()
        {
            if (_accumulator == null) return;
            string info = $"{_accumulator.PendingTokens}G накоплено  •  " +
                          $"{_accumulator.OperationalCount}/{_accumulator.TotalNodeCount} узлов  •  " +
                          $"+{_accumulator.ProductionRate:0.#}G/с";
            TaskHUD.Instance?.Show(_hubTitle, info, _theme);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_accumulator == null) _accumulator = FindAnyObjectByType<GreenTokenAccumulator>();
            if (_state == null)       _state       = FindAnyObjectByType<GameState>();
        }
#endif
    }
}