using UnityEngine;

namespace Player
{
    public class FocusBoundsFromRenderer : MonoBehaviour, IFocusBoundsProvider
    {
        [SerializeField] private Renderer _renderer;

        private void Reset()
        {
            _renderer = GetComponentInChildren<Renderer>();
        }

        public bool TryGetFocusBounds(out Bounds bounds)
        {
            if (_renderer == null)
            {
                bounds = default;
                return false;
            }

            bounds = _renderer.bounds;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_renderer == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(_renderer.bounds.center, _renderer.bounds.size);
        }
#endif
    }
}