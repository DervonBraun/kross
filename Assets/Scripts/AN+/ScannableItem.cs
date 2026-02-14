using Level;
using Player;
using UnityEngine;

namespace AN_
{
    public sealed class ScannableItem : MonoBehaviour, IInteractableAim
    {
        [Header("Data")]
        [SerializeField] private ScannableItemDef _def;

        [Header("Settings")]
        [SerializeField] private bool _oneShot = true;

        private bool _scanned;

        public bool CanInteractAim(PlayerContext context)
        {
            if (_scanned && _oneShot) return false;
            if (_def == null) return false;

            // если хочешь — можно проверять, есть ли вообще ANService
            return context != null && context.Service != null;
        }

        public void InteractAim(PlayerContext context)
        {
            if (context == null || context.Service == null) return;
            if (_def == null) return;

            var ok = context.Service.SubmitScannedItem(_def);

            if (ok)
            {
                _scanned = true;
                Debug.Log($"[Scan] File scanned: {_def.displayName}");
            }
            else
            {
                Debug.Log($"[Scan] Failed or already scanned: {_def.displayName}");
            }
        }
    }
}