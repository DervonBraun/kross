using EffectSystem;
using Player.EffectSystem;
using Tasks;
using UnityEngine;

namespace AN_
{
    public sealed class CodeService : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameState _state;
        [SerializeField] private EffectManager _effects;
        [SerializeField] private NotificationBus _notify;

        [Header("Defs")]
        [SerializeField] private EffectDefinition _defendEffect;

        public bool UseNormalCode()
        {
            if (_state == null || _effects == null || _defendEffect == null)
                return false;

            if (!_state.TryConsumeNormalCode(1))
            {
                _notify?.Push(NotifyType.Warning, "Код", "Нет доступных кодов");
                return false;
            }

            _effects.Add(_defendEffect);
            _notify?.Push(NotifyType.Info, "Defend", "Защита активирована");

            return true;
        }
    }
}