using System.Collections.Generic;
using Player;
using Player.EffectSystem;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// Зона, которая выдаёт эффекты игроку при входе.
    /// После выдачи уходит на КД — повторная выдача возможна только по его истечении.
    /// Вешай на GameObject с Collider (Is Trigger = true).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class EffectGrantZone : MonoBehaviour
    {
        [System.Serializable]
        private struct EffectGrant
        {
            [Tooltip("Какой эффект выдать.")]
            public EffectDefinition Definition;

            [Tooltip("Переопределить длительность. 0 = использовать дефолт из SO.")]
            [Min(0f)]
            public float DurationOverride;
        }

        [Header("Effects to Grant")]
        [SerializeField] private EffectGrant[] _grants = System.Array.Empty<EffectGrant>();

        [Header("Cooldown")]
        [Tooltip("КД между выдачами одному игроку (сек). 0 = выдать только один раз навсегда.")]
        [Min(0f)]
        [SerializeField] private float _cooldownSeconds = 30f;

        [Header("Debug")]
        [SerializeField] private bool _logGrants;

        // Время следующей разрешённой выдачи для каждого игрока
        // (ключ — instanceID PlayerContext, значение — Time.time когда снова можно)
        private readonly Dictionary<int, float> _cooldowns = new();

        private void Reset()
        {
            if (TryGetComponent<Collider>(out var col))
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<PlayerContext>(out var player)) return;

            int id = player.GetInstanceID();

            // Проверяем КД
            if (_cooldowns.TryGetValue(id, out float readyAt) && Time.time < readyAt)
            {
                if (_logGrants)
                    Debug.Log($"[EffectGrantZone] {name} → player on cooldown ({readyAt - Time.time:0.0}s left)");
                return;
            }

            GrantEffects(player);

            // Ставим КД. 0 = бесконечный (float.MaxValue как "никогда").
            _cooldowns[id] = _cooldownSeconds > 0f
                ? Time.time + _cooldownSeconds
                : float.MaxValue;
        }

        private void GrantEffects(PlayerContext player)
        {
            var manager = player.EffectManager;
            if (manager == null)
            {
                Debug.LogWarning($"[EffectGrantZone] {name}: PlayerContext has no EffectManager.");
                return;
            }

            foreach (var grant in _grants)
            {
                if (grant.Definition == null) continue;

                float? duration = grant.DurationOverride > 0f ? grant.DurationOverride : null;
                manager.Add(grant.Definition, duration);

                if (_logGrants)
                    Debug.Log($"[EffectGrantZone] {name} → granted '{grant.Definition.Id}' to {player.name}");
            }
        }

        /// <summary>
        /// Сброс КД для конкретного игрока (например, при смерти/респауне).
        /// </summary>
        public void ResetCooldown(PlayerContext player)
        {
            if (player != null)
                _cooldowns.Remove(player.GetInstanceID());
        }

        /// <summary>
        /// Сброс всех КД.
        /// </summary>
        public void ResetAllCooldowns() => _cooldowns.Clear();

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Подсвечиваем зону в редакторе
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (col)
            {
                case BoxCollider box:
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
                    Gizmos.DrawWireCube(box.center, box.size);
                    break;

                case SphereCollider sphere:
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
                    Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                    break;

                default:
                    Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                    break;
            }
        }
#endif
    }
}