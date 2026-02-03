using System;
using System.Collections.Generic;
using UnityEngine;

namespace Player.EffectSystem
{
    public enum EffectChangeType
    {
        Added,
        Refreshed,
        Removed,
        Expired,
        Cleared
    }

    public sealed class EffectManager : MonoBehaviour
    {
        [Header("Tick")]
        [Tooltip("Как часто дергать UI-обновление (сек). 0 = каждый кадр (не надо так).")]
        [Min(0f)]
        [SerializeField] private float _uiTickInterval = 0.10f;

        private readonly List<EffectInstance> _active = new();
        private readonly Dictionary<string, EffectInstance> _byId = new();

        private float _uiTickTimer;

        /// <summary> Срабатывает при добавлении/удалении/обновлении/истечении. </summary>
        public event Action<EffectChangeType, EffectInstance> EffectChanged;

        /// <summary> Периодический тик для UI (например обновить countdown). </summary>
        public event Action<float> Tick;

        public IReadOnlyList<EffectInstance> Active => _active;

        public bool Has(string effectId) => !string.IsNullOrEmpty(effectId) && _byId.ContainsKey(effectId);

        public bool TryGet(string effectId, out EffectInstance instance)
        {
            instance = null;
            if (string.IsNullOrEmpty(effectId)) return false;
            return _byId.TryGetValue(effectId, out instance);
        }

        public EffectInstance Add(EffectDefinition def, float? durationOverrideSeconds = null)
        {
            if (def == null)
                throw new ArgumentNullException(nameof(def));

            if (string.IsNullOrWhiteSpace(def.Id))
                throw new InvalidOperationException($"EffectDefinition '{def.name}' has empty Id.");

            float now = Time.time;
            float duration = durationOverrideSeconds ?? def.DefaultDurationSeconds;

            // Unique: refresh instead of stacking
            if (def.Unique && _byId.TryGetValue(def.Id, out var existing))
            {
                existing.Refresh(now, duration);
                EffectChanged?.Invoke(EffectChangeType.Refreshed, existing);
                return existing;
            }

            // Non-unique stacking: в MVP просто разрешаем несколько копий, но это ломает быстрый доступ по id.
            // Поэтому в базе оставляем: non-unique = всё равно хранится по id только первая, а остальное запрещаем.
            // Хочешь стаки позже: делаем Dictionary<string, List<EffectInstance>>.
            if (!def.Unique && _byId.ContainsKey(def.Id))
            {
                Debug.LogWarning($"Effect '{def.Id}' is non-unique, but stacking isn't enabled in MVP. Refreshing existing instead.");
                var existing2 = _byId[def.Id];
                existing2.Refresh(now, duration);
                EffectChanged?.Invoke(EffectChangeType.Refreshed, existing2);
                return existing2;
            }

            var inst = new EffectInstance(def, now, duration);
            _active.Add(inst);
            _byId.Add(def.Id, inst);

            EffectChanged?.Invoke(EffectChangeType.Added, inst);
            return inst;
        }

        public bool Remove(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return false;
            if (!_byId.TryGetValue(effectId, out var inst)) return false;

            _byId.Remove(effectId);
            _active.Remove(inst);

            EffectChanged?.Invoke(EffectChangeType.Removed, inst);
            return true;
        }

        public void ClearAll()
        {
            _active.Clear();
            _byId.Clear();
            EffectChanged?.Invoke(EffectChangeType.Cleared, null);
        }

        private void Update()
        {
            float now = Time.time;

            // Expire pass (с конца, чтобы удалять безопасно)
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                if (!e.IsExpired(now)) continue;

                _active.RemoveAt(i);
                _byId.Remove(e.Def.Id);
                EffectChanged?.Invoke(EffectChangeType.Expired, e);
            }

            // UI tick
            if (_uiTickInterval <= 0f)
            {
                Tick?.Invoke(now);
                return;
            }

            _uiTickTimer += Time.deltaTime;
            if (_uiTickTimer >= _uiTickInterval)
            {
                _uiTickTimer = 0f;
                Tick?.Invoke(now);
            }
        }
    }
}
