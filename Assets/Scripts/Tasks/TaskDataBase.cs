using System.Collections.Generic;
using UnityEngine;

namespace Tasks
{
    [CreateAssetMenu(menuName = "KROSS/Tasks/Task Database")]
    public class TaskDatabase : ScriptableObject
    {
        public List<RoutineTaskDef> tasks = new();

        private Dictionary<string, RoutineTaskDef> _map;

        public bool TryGet(string id, out RoutineTaskDef def)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                def = null;
                return false;
            }

            _map ??= BuildMap();
            return _map.TryGetValue(id, out def) && def != null;
        }

        private Dictionary<string, RoutineTaskDef> BuildMap()
        {
            var map = new Dictionary<string, RoutineTaskDef>(tasks.Count);

            foreach (var t in tasks)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.id)) continue;
                map[t.id] = t; // последнее побеждает, если дубликаты
            }

            return map;
        }

#if UNITY_EDITOR
        private void OnValidate() => _map = null;
#endif
    }
}