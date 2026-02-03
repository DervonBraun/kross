using System;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(PlayerContext))]
    public class InputFilter : MonoBehaviour
    {
        private PlayerContext _playerContext;
        
        private InputReader _inputReader;
        private PlayerConfig _playerConfig;
        
        private void Awake()
        {
            _playerContext = GetComponent<PlayerContext>();
            _inputReader = _playerContext.InputReader;
            _playerConfig = _playerContext.PlayerConfig;
            
            if (!_inputReader) Debug.LogError($"Input reader not found");
            if (!_playerConfig) Debug.LogError($"Player config not found");
        }

        public Vector2 Move() => _playerConfig.CanMove ? _inputReader.Move : Vector2.zero;
        
        public Vector2 Look() => _playerConfig.CanLook ? _inputReader.Look : Vector2.zero;

        public bool Sprint() => _playerConfig.CanSprint && _inputReader.Sprint;
        public bool Aim() => _playerConfig.CanAim && _inputReader.Aim;

        public bool InteractPressed() => _inputReader.ConsumeInteractPressed();
        


    }
}