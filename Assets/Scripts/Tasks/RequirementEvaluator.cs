using Tasks;
public static class RequirementEvaluator
{
    public static bool AreAllMet(Requirement[] reqs, GameState state)
    {
        if (reqs == null || reqs.Length == 0) return true;

        for (int i = 0; i < reqs.Length; i++)
            if (!IsMet(reqs[i], state)) return false;

        return true;
    }

    public static bool IsMet(in Requirement req, GameState s)
    {
        switch (req.type)
        {
            case RequirementType.None:
                return true;

            case RequirementType.MinAccessLevel:
                return s.AccessLevel >= req.intValue;

            case RequirementType.OktsStageAtMost:
                return s.OktsStage <= req.intValue;

            case RequirementType.OktsStageAtLeast:
                return s.OktsStage >= req.intValue;

            // Заглушки под будущее:
            case RequirementType.HasEffectId:
                return s.HasEffectId(req.stringValue);

            case RequirementType.HasEffectTag:
                return s.HasEffectTag(req.stringValue);

            default:
                return false;
        }
    }
}