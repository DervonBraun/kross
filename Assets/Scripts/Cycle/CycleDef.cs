using AN_;
using Player.EffectSystem;
using UnityEngine;

namespace Cycle
{
    [CreateAssetMenu(menuName = "KROSS/Cycle/Cycle Def")]
    public sealed class CycleDef : ScriptableObject
    {
        [Header("Identity")]
        public string cycleId;

        [Header("Defend Effect")]
        [Tooltip("EffectDefinition с тегом 'defend'. Длительность берётся из самого SO-эффекта.")]
        public EffectDefinition defendEffect;

        [Tooltip("Тег, по которому проверяется наличие защиты при завершении цикла.")]
        public string defendEffectTag = "defend";

        [Header("Timers")]
        [Tooltip("КД сейфзоны: сколько секунд после BeginCycle() нельзя завершить цикл.")]
        [Min(0f)] public float exitCooldownDuration = 120f;   // 2 минуты

        [Tooltip("Сколько секунд даётся на сдачу кода. После — точка блокируется.")]
        [Min(1f)] public float submitWindowDuration = 300f;   // 5 минут

        [Header("OKTS")]
        [Min(1)] public int maxOktsStage = 5;

        [Header("Code Request (справка)")]
        public ANRequestDef generateCodeRequest;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(cycleId))
                cycleId = name;
        }
#endif
    }
}