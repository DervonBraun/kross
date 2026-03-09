using System;
using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Описывает heat-поведение задания.
    ///
    /// Модель:
    ///   reward = baseReward * (1 - heat / maxHeat)
    ///
    ///   При выполнении:
    ///     • своё задание нагревается   → heat = Lerp(heat, maxHeat, selfHeatRate) → reward ↓
    ///     • задания из influences охлаждаются → их reward растёт на заданный коэффициент
    ///
    ///   Пассивно каждый кадр:
    ///     • heat = Lerp(heat, 0, cooldownRate * dt) → задание медленно возвращается к полной награде
    /// </summary>
    [CreateAssetMenu(menuName = "KROSS/Tasks/Heat Config")]
    public sealed class TaskHeatConfig : ScriptableObject
    {
        [Header("Self Heat — нагрев при выполнении")]
        [Tooltip("Насколько сильно задание нагревается при каждом выполнении.\n" +
                 "heat = Lerp(currentHeat, maxHeat, selfHeatRate)\n\n" +
                 "0.0 → не нагревается (reward всегда 100%)\n" +
                 "0.3 → умеренный нагрев\n" +
                 "1.0 → мгновенно перегревается (следующая награда = 0)")]
        [Range(0f, 1f)] public float selfHeatRate = 0.4f;

        [Tooltip("Максимальный heat. При heat == maxHeat → reward = 0.")]
        [Min(1f)] public float maxHeat = 100f;

        [Tooltip("Начальный heat при старте сессии.\n" +
                 "0 = холодное (полная награда с первого раза).")]
        [Range(0f, 100f)] public float initialHeat = 0f;

        [Header("Natural Cooldown — пассивное остывание")]
        [Tooltip("Скорость пассивного остывания к нулю.\n" +
                 "heat = Lerp(currentHeat, 0, cooldownRate * dt)\n\n" +
                 "0.0 → не остывает\n" +
                 "0.05 → медленное остывание\n" +
                 "0.5 → быстрое остывание")]
        [Range(0f, 1f)] public float passiveCooldownRate = 0.05f;

        [Header("Influences — бонус соседним заданиям")]
        [Tooltip("Задания из этого списка охлаждаются при выполнении данного.\n" +
                 "Их reward множитель вырастет на указанный коэффициент.\n\n" +
                 "Пример: multiplier = 1.1 → задание B станет давать на 10% больше.")]
        public HeatInfluence[] influences = Array.Empty<HeatInfluence>();

        [Header("Completion Limits")]
        [Tooltip("Максимальное число выполнений за сессию. 0 = неограниченно.")]
        [Min(0)] public int maxCompletionsPerSession = 0;
    }

    [Serializable]
    public struct HeatInfluence
    {
        [Tooltip("ID задания, которое получает бонус (охлаждается).")]
        public string targetTaskId;

        [Tooltip("Коэффициент роста reward целевого задания.\n" +
                 "Например 1.1 → reward вырастет на 10%.\n\n" +
                 "Под капотом: targetHeat снижается так, чтобы новый reward = oldReward * multiplier.\n" +
                 "Значение должно быть > 1.0 для бонуса.\n" +
                 "1.0 → нет эффекта, < 1.0 → штраф соседу.")]
        [Min(0f)] public float rewardMultiplier;

        public HeatInfluence(string targetTaskId, float rewardMultiplier)
        {
            this.targetTaskId     = targetTaskId;
            this.rewardMultiplier = rewardMultiplier;
        }
    }
}