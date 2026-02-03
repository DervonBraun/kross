using UnityEngine;

namespace Player
{
    public class FocusBoundsFromCollider : MonoBehaviour, IFocusBoundsProvider
    {
        [SerializeField] private BoxCollider _collider;

        private void Reset()
        {
            _collider = GetComponent<BoxCollider>();
        }

        public bool TryGetFocusBounds(out Bounds bounds)
        {
            if (_collider == null)
            {
                bounds = default;
                return false;
            }

            bounds = _collider.bounds;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_collider == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_collider.bounds.center, _collider.bounds.size);
        }
#endif
    }
}