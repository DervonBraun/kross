using Player;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// Вешается на отдельный GameObject с Collider (Is Trigger = true).
    /// Пробрасывает события входа/выхода игрока в InteractableDoor.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ProximityTriggerRelay : MonoBehaviour
    {
        [SerializeField] private InteractableDoor _door;

        private void Reset()
        {
            _door = GetComponentInParent<InteractableDoor>();

            if (TryGetComponent<Collider>(out var col))
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_door == null) return;
            if (other.TryGetComponent<PlayerContext>(out var player))
                _door.OnProximityEnter(player);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_door == null) return;
            if (other.TryGetComponent<PlayerContext>(out var player))
                _door.OnProximityExit(player);
        }
    }
}