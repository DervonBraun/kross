using EffectSystem;
using Player;
using Player.EffectSystem;
using UnityEngine;

namespace Level
{
    public sealed class EffectGiverDefaultInteractable : MonoBehaviour, IInteractableDefault
    {
        public enum ReapplyMode
        {
            RefreshIfExists,   // если уникальный и уже есть -> обновить таймер (по сути менеджер и так так делает)
            IgnoreIfExists,    // если уже есть -> ничего
            RemoveIfExists     // если уже есть -> снять (как переключатель)
        }

        [Header("Effect")]
        [SerializeField] private EffectDefinition _effect;

        [Tooltip("0 = взять duration из EffectDefinition. >0 = переопределить.")]
        [Min(0f)]
        [SerializeField] private float _durationOverrideSeconds = 0f;

        [Header("Rules")]
        [SerializeField] private ReapplyMode _reapplyMode = ReapplyMode.RefreshIfExists;

        [Tooltip("Одноразовый интеракт: после успешного применения больше не работает.")]
        [SerializeField] private bool _oneShot = false;

        [Tooltip("Кулдаун между применениями (сек). 0 = без кулдауна.")]
        [Min(0f)]
        [SerializeField] private float _cooldownSeconds = 0f;

        [Header("Optional gating")]
        [Tooltip("Требовать, чтобы у игрока НЕ было этого эффекта (быстрее, чем ReapplyMode.IgnoreIfExists).")]
        [SerializeField] private bool _requireNotPresent = false;

        [Header("Debug")]
        [SerializeField] private bool _log = false;

        private bool _used;
        private float _nextAllowedTime;

        public bool CanInteractDefault(PlayerContext context)
        {
            if (_used && _oneShot) return false;
            if (_effect == null) return false;
            if (context == null) return false;

            if (_cooldownSeconds > 0f && Time.time < _nextAllowedTime)
                return false;

            var effects = GetEffects(context);
            if (effects == null) return false;

            if (_requireNotPresent && effects.Has(_effect.Id))
                return false;

            return true;
        }

        public void InteractDefault(PlayerContext context)
        {
            if (!CanInteractDefault(context))
                return;

            var effects = GetEffects(context);
            if (effects == null) return;

            bool has = effects.Has(_effect.Id);

            // toggle/remove mode
            if (has && _reapplyMode == ReapplyMode.RemoveIfExists)
            {
                effects.Remove(_effect.Id);
                AfterSuccess();
                if (_log) Debug.Log($"{name}: Removed effect '{_effect.Id}'", this);
                return;
            }

            // ignore mode
            if (has && _reapplyMode == ReapplyMode.IgnoreIfExists)
            {
                if (_log) Debug.Log($"{name}: Ignored, effect '{_effect.Id}' already present", this);
                return;
            }

            float? durationOverride = _durationOverrideSeconds > 0f ? _durationOverrideSeconds : null;
            var inst = effects.Add(_effect, durationOverride);

            AfterSuccess();

            if (_log)
                Debug.Log($"{name}: Gave effect '{inst.Def.Id}' to player", this);
        }

        private void AfterSuccess()
        {
            _used = true;
            if (_cooldownSeconds > 0f)
                _nextAllowedTime = Time.time + _cooldownSeconds;
        }

        private EffectManager GetEffects(PlayerContext context)
        {
            // ВАЖНО: подстрой под твоё реальное API PlayerContext.
            // Например: return context.EffectManager;
            // или: return context.PlayerRoot.Effects;
            return context != null ? context.EffectManager : null;
        }
    }
}
