using AN_;
using Player;
using UnityEngine;

namespace Tasks
{
    public sealed class TaskInteractable : MonoBehaviour,
        IInteractableDefault,
        IInteractableAim,
        IInteractableAimHover,
        IInteractableAimExit,
        IThemeable
    {
        [Header("Task")]
        [SerializeField] private string      _taskId;
        [SerializeField] private TaskService _taskService;

        [Header("Theme (optional — или через TaskHudThemeAdapter)")]
        [SerializeField] private TaskHudTheme _theme;

        [Header("Feedback (optional)")]
        [SerializeField] private string          _successMessage = "Task completed!";
        [SerializeField] private string          _failMessage    = "Can't complete task.";
        [SerializeField] private NotificationBus _bus;

        // ─── Cached data ───────────────────────────────────────────────────────
        private RoutineTaskDef _taskDef;
        private bool           _dataCached;

        private string      _displayName;
        private TokenAmount _baseReward;
        private TokenAmount _currentReward;
        private bool        _heatAffected;

        // IThemeable
        public void SetTheme(TaskHudTheme theme) => _theme = theme;

        // ══════════════════════════════════════════════════════════════════════
        // IInteractableAimHover
        // ══════════════════════════════════════════════════════════════════════

        public void OnAimEnter(PlayerContext context)
        {
            RefreshData();
            TaskHUD.Instance?.Show(_displayName, _baseReward, _currentReward, _heatAffected, _theme);
        }

        public void OnAimExit() => TaskHUD.Instance?.Hide();

        // ══════════════════════════════════════════════════════════════════════
        // IInteractableAim / IInteractableDefault
        // ══════════════════════════════════════════════════════════════════════

        public bool CanInteractAim(PlayerContext context)     => true;
        public void InteractAim(PlayerContext context)        => ExecuteTask();

        public bool CanInteractDefault(PlayerContext context) => true;
        public void InteractDefault(PlayerContext context)    => ExecuteTask();

        // ══════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════

        private void ExecuteTask()
        {
            if (_taskService == null)
            {
                Debug.LogWarning($"[TaskInteractable] TaskService не назначен на {name}!", this);
                return;
            }

            bool success = _taskService.Execute(_taskId);
            _bus?.Push(NotifyType.Info, _taskId, success ? _successMessage : _failMessage);
            _dataCached = false;

            Debug.Log(success
                ? $"[TaskInteractable] {_successMessage} (taskId={_taskId})"
                : $"[TaskInteractable] {_failMessage} (taskId={_taskId})");
        }

        private void RefreshData()
        {
            _taskDef ??= _taskService?.GetDef(_taskId);

            if (_taskDef == null)
            {
                _displayName   = _taskId;
                _baseReward    = TokenAmount.Zero;
                _currentReward = TokenAmount.Zero;
                _heatAffected  = false;
                return;
            }

            _displayName = string.IsNullOrWhiteSpace(_taskDef.displayName) ? _taskId : _taskDef.displayName;
            _baseReward  = _taskDef.tokenReward;

            if (_taskDef.heatConfig != null)
            {
                var registry = _taskService?.GetHeatRegistry();
                if (registry != null)
                {
                    float multiplier = registry.GetRewardMultiplier(_taskId);
                    _currentReward   = _taskDef.tokenReward.Scale(multiplier);
                    _heatAffected    = Mathf.Abs(_currentReward.red   - _baseReward.red)   > 1
                                    || Mathf.Abs(_currentReward.green - _baseReward.green) > 1
                                    || Mathf.Abs(_currentReward.blue  - _baseReward.blue)  > 1;
                    return;
                }
            }

            _currentReward = _baseReward;
            _heatAffected  = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_taskService == null)
                _taskService = FindAnyObjectByType<TaskService>();
        }
#endif
    }
}