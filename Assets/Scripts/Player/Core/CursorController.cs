using UnityEngine;

namespace Player
{
    public sealed class CursorController : MonoBehaviour
    {
        [SerializeField] private bool _lockCursorInGameplay = true;

        public void SetCursor(bool uiMode)
        {
            Cursor.visible = uiMode;

            if (uiMode)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = _lockCursorInGameplay
                    ? CursorLockMode.Locked
                    : CursorLockMode.None;
            }
        }
    }
}