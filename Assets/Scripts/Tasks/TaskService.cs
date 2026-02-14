using UnityEngine;

namespace Tasks
{
    public sealed class TaskService : MonoBehaviour
    {
        [SerializeField] private GameState _state;
        [SerializeField] private TaskDatabase _db;

        public bool Execute(string taskId)
        {
            if (_state == null || _db == null)
                return false;

            if (!_db.TryGet(taskId, out var def) || def == null)
                return false;

            if (!RequirementEvaluator.AreAllMet(def.requirements, _state))
                return false;

            _state.AddTokens(def.tokenReward);
            return true;
        }
    }
}