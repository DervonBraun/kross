using System;
using UnityEngine;

namespace Player
{
    public class CursorController : MonoBehaviour
    {
        [SerializeField] private bool _lockOnStart = true;

        private void Start()
        {
            if (_lockOnStart) Lock();
        }

        public void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}