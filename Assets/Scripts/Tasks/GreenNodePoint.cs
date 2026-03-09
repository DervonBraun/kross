using AN_;
using Player;
using UnityEngine;

namespace Tasks
{
    public sealed class GreenNodePoint : MonoBehaviour,
        IInteractableDefault,
        IInteractableAim,
        IInteractableAimHover,
        IInteractableAimExit,
        IThemeable
    {
        [Header("Identity")]
        [SerializeField] private string _nodeLabel = "Node A";

        [Header("Durability")]
        [SerializeField, Min(1)] private int   _maxDurability   = 100;
        [SerializeField, Min(1)] private int   _degradeAmount   = 20;
        [SerializeField, Min(1)] private float _degradeInterval = 30f;

        [Header("Refs")]
        [SerializeField] private NotificationBus _bus;
        [SerializeField] private TaskHudTheme    _theme;

        private int   _currentDurability;
        private float _degradeTimer;

        public bool   IsOperational  => _currentDurability > 0;
        public float  DurabilityRatio => _maxDurability > 0 ? (float)_currentDurability / _maxDurability : 0f;
        public string NodeLabel      => _nodeLabel;

        // IThemeable
        public void SetTheme(TaskHudTheme theme) => _theme = theme;

        private void Awake()
        {
            _currentDurability = _maxDurability;
            _degradeTimer      = _degradeInterval;
        }

        private void Update()
        {
            _degradeTimer -= Time.deltaTime;
            if (_degradeTimer <= 0f)
            {
                _degradeTimer = _degradeInterval;
                Degrade();
            }
        }

        public bool CanInteractDefault(PlayerContext context) => _currentDurability < _maxDurability;
        public void InteractDefault(PlayerContext context)
        {
            if (_currentDurability >= _maxDurability) return;
            _currentDurability = _maxDurability;
            _degradeTimer      = _degradeInterval;
            _bus?.Push(NotifyType.Info, _nodeLabel, $"{_nodeLabel} отремонтирован — 100%");
            RefreshHud();
        }

        public bool CanInteractAim(PlayerContext context) => CanInteractDefault(context);
        public void InteractAim(PlayerContext context)    => InteractDefault(context);
        public void OnAimEnter(PlayerContext context)     => RefreshHud();
        public void OnAimExit()                           => TaskHUD.Instance?.Hide();

        private void Degrade()
        {
            if (_currentDurability <= 0) return;
            _currentDurability = Mathf.Max(0, _currentDurability - _degradeAmount);
            if (_currentDurability <= 0)
                _bus?.Push(NotifyType.Warning, _nodeLabel, $"{_nodeLabel} вышел из строя!");
        }

        private void RefreshHud()
        {
            int    pct    = Mathf.RoundToInt(DurabilityRatio * 100f);
            string status = IsOperational ? $"{pct}% — исправен" : "⚠ неисправен";
            TaskHUD.Instance?.Show(_nodeLabel, status, _theme);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_nodeLabel)) _nodeLabel = gameObject.name;
        }
#endif
    }
}