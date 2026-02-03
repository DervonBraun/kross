using Player;
using UnityEngine;

namespace Level
{
    public class TestInteractableAim : MonoBehaviour, IInteractableAim
    {
        [SerializeField] private string _message = "Interactable triggered";

        public bool CanInteractAim(PlayerContext context)
        {
            return true; // пока без условий
        }

        public void InteractAim(PlayerContext context)
        {
            Debug.Log($"[Interact] {_message} | Object: {name}");
        }
    }
}