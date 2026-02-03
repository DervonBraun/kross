using UnityEngine;

namespace Player.EffectSystem
{
    public enum EffectCategory
    {
        System,
        Buff,
        Debuff,
        Quest,
        Misc
    }

    [CreateAssetMenu(fileName = "EffectDefinition", menuName = "ES")]
    public sealed class EffectDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id = "effect_id";
        [SerializeField] private string _displayName = "Effect";

        [Header("Presentation")]
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private Sprite _icon;

        [Header("Rules")]
        [Tooltip("0 = бесконечный эффект (пока не снимут вручную)")]
        [Min(0f)]
        [SerializeField] private float _defaultDurationSeconds = 0f;

        [Tooltip("Если true, эффект не может иметь несколько копий. Повторное добавление обновляет таймер.")]
        [SerializeField] private bool _unique = true;

        [SerializeField] private EffectCategory _category = EffectCategory.System;

        public string Id => _id;
        public string DisplayName => _displayName;
        public Color Color => _color;
        public Sprite Icon => _icon;
        public float DefaultDurationSeconds => _defaultDurationSeconds;
        public bool Unique => _unique;
        public EffectCategory Category => _category;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Минимальная защита от пустых id (а то потом весело искать, почему всё ломается).
            if (string.IsNullOrWhiteSpace(_id))
                _id = name.Trim().ToLowerInvariant().Replace(" ", "_");
        }
#endif
    }
}