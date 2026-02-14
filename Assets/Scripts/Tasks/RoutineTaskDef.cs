using UnityEngine;

namespace Tasks
{
    [CreateAssetMenu(menuName = "KROSS/Tasks/Routine Task")]
    public class RoutineTaskDef : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("Classification")]
        public TaskColor color;
        [Min(0)] public int accessLevel;

        [Header("Rewards")]
        public TokenAmount tokenReward;

        [Header("Requirements")]
        public Requirement[] requirements;

        [Header("Repeat")]
        public bool repeatable = true;
        [Min(0f)] public float cooldownSeconds = 0f;
    }
}