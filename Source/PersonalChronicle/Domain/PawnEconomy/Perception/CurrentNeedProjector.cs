using System;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    internal static class CurrentNeedProjector
    {
        // Boundary tolerance only, not a gameplay threshold. Keep original values in NumericNeedFact.
        internal const double NumericBoundaryEpsilon = 0.00001d;

        public static CurrentNeedGap Project(string beneficiaryId, NumericNeedFact reading, NeedTarget target)
        {
            if (reading == null) throw new ArgumentNullException(nameof(reading));
            Fact<float> actual = ActualRatio(reading);
            Fact<float> desired = TargetRatio(target);
            if (!desired.TryGetValue(out float targetValue))
                return Result(beneficiaryId, reading, target, actual, desired, desired, desired, desired);
            if (!actual.TryGetValue(out float currentRatio))
                return Result(beneficiaryId, reading, target, actual, desired, actual, actual, actual);

            // Double intermediate arithmetic avoids overflow for very small/large finite float levels.
            float absolute = (float)Math.Max(0d, (double)targetValue - currentRatio);
            float relative = (float)Math.Max(0d, Math.Min(1d, ((double)targetValue - currentRatio) / targetValue));
            Fact<float> raw;
            if (reading.RawUnitId == null)
                raw = Fact<float>.Unsupported(PerceptionReasons.RawUnitUnknown);
            else
            {
                reading.CurrentLevel.TryGetValue(out float current);
                reading.MaximumLevel.TryGetValue(out float maximum);
                // Only tiny out-of-boundary noise is normalized; retain valid in-range precision.
                double normalizedCurrent = Math.Max(0d, Math.Min((double)maximum, current));
                raw = Fact<float>.Known((float)Math.Max(0d, (double)targetValue * maximum - normalizedCurrent));
            }
            return Result(beneficiaryId, reading, target, actual, desired,
                Fact<float>.Known(relative), Fact<float>.Known(absolute), raw);
        }

        private static Fact<float> ActualRatio(NumericNeedFact reading)
        {
            if (!reading.StandardRatioSupported)
                return Fact<float>.Unsupported(PerceptionReasons.UnsupportedNumericSemantics);
            if (!reading.CurrentLevel.TryGetValue(out float current)) return reading.CurrentLevel;
            if (!reading.MaximumLevel.TryGetValue(out float maximum)) return reading.MaximumLevel;
            if (!Finite(current) || !Finite(maximum)) return Fact<float>.Error(PerceptionReasons.NonFiniteReading);
            if (maximum <= 0f) return Fact<float>.Error(PerceptionReasons.NonPositiveMaximum);
            double ratio = (double)current / maximum;
            if (ratio < -NumericBoundaryEpsilon || ratio > 1d + NumericBoundaryEpsilon)
                return Fact<float>.Unsupported(PerceptionReasons.OutsideStandardRange);
            return Fact<float>.Known((float)Math.Max(0d, Math.Min(1d, ratio)));
        }

        private static Fact<float> TargetRatio(NeedTarget target)
        {
            switch (target.State)
            {
                case TargetState.Unknown: return Fact<float>.Unknown(PerceptionReasons.TargetMissing);
                case TargetState.Disabled: return Fact<float>.NotApplicable(PerceptionReasons.TargetDisabled);
                case TargetState.NotApplicable: return Fact<float>.NotApplicable(PerceptionReasons.TargetNotApplicable);
                case TargetState.Enabled:
                    if (!target.Ratio.TryGetValue(out float value)) return target.Ratio;
                    return Finite(value) && value > 0f && value <= 1f ? target.Ratio
                        : Fact<float>.Error(PerceptionReasons.InvalidTargetRatio);
                default: return Fact<float>.Error(PerceptionReasons.InvalidTargetState);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static CurrentNeedGap Result(string beneficiaryId, NumericNeedFact reading, NeedTarget target,
            Fact<float> actual, Fact<float> desired, Fact<float> relative, Fact<float> absolute, Fact<float> raw)
            => new CurrentNeedGap(beneficiaryId, reading, target, actual, desired, relative, absolute, raw);
    }
}
