using System;

namespace Tasks
{
    public enum RequirementType
    {
        None,
        HasEffectId,      // пока заглушка (подключим позже)
        HasEffectTag,     // пока заглушка
        MinAccessLevel,
        OktsStageAtMost,
        OktsStageAtLeast,
        InConnectionWindow // пока заглушка
    }

    [Serializable]
    public struct Requirement
    {
        public RequirementType type;

        // payload (используются по ситуации)
        public string stringValue; // effect id/tag
        public int intValue;       // access/stage
    }
}