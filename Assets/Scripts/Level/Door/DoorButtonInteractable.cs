using Player;
using UnityEngine;

namespace Level
{
    [RequireComponent(typeof(FocusBoundsFromCollider))]
    public sealed class DoorButtonInteractable : MonoBehaviour, IInteractableAim
    {
        [Header("Door")]
        [SerializeField] private InteractableDoor _door;

        [Header("Rules")]
        [SerializeField] private bool _canUseWhileDoorMoving = true;

        public bool CanInteractAim(PlayerContext context)
        {
            if (_door == null) return false;
            if (_canUseWhileDoorMoving) return true;
            return !_door.IsMoving;
        }

        public void InteractAim(PlayerContext context)
        {
            if (_door == null) return;
            _door.ToggleFromButton(this, context); // пробрасываем context
        }

        private void Reset()
        {
            // Попытка найти дверь рядом, чтобы в инспекторе меньше страдать
            if (_door == null)
                _door = GetComponentInParent<InteractableDoor>();
        }
    }
}