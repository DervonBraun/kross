using System;

namespace Player.EffectSystem
{
    /// <summary>
    /// Живой экземпляр эффекта. Не MonoBehaviour.
    /// </summary>
    [Serializable]
    public sealed class EffectInstance
    {
        public EffectDefinition Def { get; }
        public float StartTime { get; private set; }
        public float DurationSeconds { get; private set; } // 0 = бесконечный

        public bool IsInfinite => DurationSeconds <= 0f;

        public EffectInstance(EffectDefinition def, float startTime, float durationSeconds)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            StartTime = startTime;
            DurationSeconds = Math.Max(0f, durationSeconds);
        }

        public float EndTime => IsInfinite ? float.PositiveInfinity : (StartTime + DurationSeconds);

        public bool IsExpired(float now) => !IsInfinite && now >= EndTime;

        public float Remaining(float now)
        {
            if (IsInfinite) return float.PositiveInfinity;
            return Math.Max(0f, EndTime - now);
        }

        public float NormalizedRemaining(float now)
        {
            if (IsInfinite) return 1f;
            if (DurationSeconds <= 0f) return 1f;
            return Math.Clamp(Remaining(now) / DurationSeconds, 0f, 1f);
        }

        public void Refresh(float now, float newDurationSeconds)
        {
            StartTime = now;
            DurationSeconds = Math.Max(0f, newDurationSeconds);
        }
    }
}