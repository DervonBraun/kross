using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Выполняет задание: проверяет требования, вычисляет heat-модифицированную награду,
    /// выдаёт токены, уведомляет HeatRegistry.
    /// </summary>
    public sealed class TaskService : MonoBehaviour
    {
        [SerializeField] private GameState    _state;
        [SerializeField] private TaskDatabase _db;
        [SerializeField] private HeatRegistry _heatRegistry;

        private void Awake()
        {
            if (_heatRegistry != null && _db != null)
            {
                foreach (var def in _db.AllDefs)
                {
                    if (def?.heatConfig != null)
                        _heatRegistry.Register(def.id, def.heatConfig);
                }
            }
        }

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>Возвращает TaskDef по id или null.</summary>
        public RoutineTaskDef GetDef(string taskId)
            => _db != null && _db.TryGet(taskId, out var def) ? def : null;

        /// <summary>Возвращает реестр heat (нужен UI для расчёта текущей цены).</summary>
        public HeatRegistry GetHeatRegistry() => _heatRegistry;

        // ── Execution ─────────────────────────────────────────────────────────

        /// <summary>
        /// Пытается выполнить задание.
        /// </summary>
        /// <param name="taskId">ID задания.</param>
        /// <param name="earnedTokens">Фактически начисленные токены (с учётом heat).</param>
        /// <returns>true, если задание выполнено успешно.</returns>
        public bool Execute(string taskId, out TokenAmount earnedTokens)
        {
            earnedTokens = default;

            if (_state == null || _db == null)
                return false;

            if (!_db.TryGet(taskId, out var def) || def == null)
                return false;

            if (!RequirementEvaluator.AreAllMet(def.requirements, _state))
                return false;

            if (_heatRegistry != null && !_heatRegistry.CanComplete(taskId))
            {
                Debug.Log($"[TaskService] Task '{taskId}' hit session completion limit.");
                return false;
            }

            float multiplier = _heatRegistry != null
                ? _heatRegistry.GetRewardMultiplier(taskId)
                : 1f;

            earnedTokens = def.tokenReward.Scale(multiplier);
            _state.AddTokens(earnedTokens);

            _heatRegistry?.OnTaskCompleted(taskId);

            return true;
        }

        /// <summary>Перегрузка без out-параметра для обратной совместимости.</summary>
        public bool Execute(string taskId) => Execute(taskId, out _);
    }
}