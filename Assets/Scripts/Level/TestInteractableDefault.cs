using Player;
using UnityEngine;

namespace Level
{
    public class TestInteractableDefault : MonoBehaviour, IInteractableDefault
    {
        [SerializeField] private string _message = "Interactable triggered";

        public bool CanInteractDefault(PlayerContext context)
        {
            return true;
        }

        public void InteractDefault(PlayerContext context)
        {
            Debug.Log($"[InteractDefault] {_message} | Object: {name}");
        }
    }
}