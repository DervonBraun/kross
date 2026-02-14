using UnityEngine;

namespace Tasks
{
    public sealed class TaskDebugRunner : MonoBehaviour
    {
        [SerializeField] private TaskService _tasks;
        [SerializeField] private string _taskId;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (_tasks == null)
                {
                    Debug.LogError("TaskService missing");
                    return;
                }

                var ok = _tasks.Execute(_taskId);

                if (ok)
                    Debug.Log($"[TASK] Completed: {_taskId}");
                else
                    Debug.LogWarning($"[TASK] Failed: {_taskId}");
            }
        }
    }
}