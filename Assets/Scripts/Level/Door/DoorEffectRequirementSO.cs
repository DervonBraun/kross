using System;
using EffectSystem;
using Player.EffectSystem;
using UnityEngine;

namespace Level
{
    public enum EffectRequirementMode
    {
        /// <summary>Игрок должен иметь ВСЕ из перечисленного.</summary>
        All,
        /// <summary>Достаточно любого одного.</summary>
        Any
    }

    /// <summary>
    /// ScriptableObject с требованиями к эффектам для прохода через дверь.
    /// Можно комбинировать теги и конкретные EffectDefinition.
    /// Создаётся через: Create → Level → Door Effect Requirement
    /// </summary>
    [CreateAssetMenu(fileName = "DoorEffectReq", menuName = "Level/Door Effect Requirement")]
    public sealed class DoorEffectRequirementSO : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("All — нужны все условия. Any — достаточно одного.")]
        [SerializeField] private EffectRequirementMode _mode = EffectRequirementMode.All;

        [Header("By Tag")]
        [Tooltip("Игрок должен иметь активный эффект с каждым из этих тегов (или хотя бы одним при Any).")]
        [SerializeField] private string[] _requiredTags = Array.Empty<string>();

        [Header("By Definition")]
        [Tooltip("Игрок должен иметь активные эффекты по этим конкретным SO.")]
        [SerializeField] private EffectDefinition[] _requiredEffects = Array.Empty<EffectDefinition>();

        public EffectRequirementMode Mode => _mode;
        public string[] RequiredTags => _requiredTags;
        public EffectDefinition[] RequiredEffects => _requiredEffects;

        /// <summary>
        /// Проверяет, удовлетворяет ли менеджер эффектов условиям этого SO.
        /// </summary>
        public bool IsSatisfied(EffectManager manager)
        {
            if (manager == null) return false;

            return _mode == EffectRequirementMode.All
                ? CheckAll(manager)
                : CheckAny(manager);
        }

        private bool CheckAll(EffectManager manager)
        {
            foreach (var tag in _requiredTags)
                if (!string.IsNullOrWhiteSpace(tag) && !manager.HasTag(tag))
                    return false;

            foreach (var def in _requiredEffects)
                if (def != null && !manager.Has(def.Id))
                    return false;

            return true;
        }

        private bool CheckAny(EffectManager manager)
        {
            foreach (var tag in _requiredTags)
                if (!string.IsNullOrWhiteSpace(tag) && manager.HasTag(tag))
                    return true;

            foreach (var def in _requiredEffects)
                if (def != null && manager.Has(def.Id))
                    return true;

            return false;
        }
    }
}