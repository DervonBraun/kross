using System.Collections.Generic;
using UnityEngine;

namespace AN_
{
    [CreateAssetMenu(menuName = "KROSS/AN+/AN Database")]
    public class ANDatabase : ScriptableObject
    {
        public List<ANRequestDef> requests = new();
        private Dictionary<string, ANRequestDef> _map;

        public bool TryGet(string id, out ANRequestDef def)
        {
            def = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            _map ??= Build();
            return _map.TryGetValue(id, out def) && def != null;
        }

        private Dictionary<string, ANRequestDef> Build()
        {
            var map = new Dictionary<string, ANRequestDef>(requests.Count);
            foreach (var r in requests)
                if (r != null && !string.IsNullOrWhiteSpace(r.id))
                    map[r.id] = r;
            return map;
        }

#if UNITY_EDITOR
        private void OnValidate() => _map = null;
#endif
    }
}