using System;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    internal enum FactState
    {
        Unknown = 0,
        Known = 1,
        NotApplicable = 2,
        Unsupported = 3,
        Error = 4
    }

    /// <summary>A knowledge value, independent of freshness, scope and permission to act.
    /// T must be a scalar, stable ID or immutable domain value; never a game object.</summary>
    internal readonly struct Fact<T>
    {
        private readonly T value;
        private readonly string reasonCode;
        public FactState State { get; }
        public string ReasonCode => reasonCode ?? PerceptionReasons.Uninitialized;

        private Fact(FactState state, T value, string reason)
        {
            State = state;
            this.value = value;
            reasonCode = reason;
        }

        public bool TryGetValue(out T result)
        {
            result = State == FactState.Known ? value : default;
            return State == FactState.Known;
        }

        public static Fact<T> Known(T value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            // Numeric validity is checked by the projector; retain original readings for diagnostics.
            return new Fact<T>(FactState.Known, value, PerceptionReasons.Observed);
        }

        public static Fact<T> Unknown(string reason) => Unavailable(FactState.Unknown, reason);
        public static Fact<T> NotApplicable(string reason) => Unavailable(FactState.NotApplicable, reason);
        public static Fact<T> Unsupported(string reason) => Unavailable(FactState.Unsupported, reason);
        public static Fact<T> Error(string reason) => Unavailable(FactState.Error, reason);

        public static Fact<T> Unavailable(FactState state, string reason)
        {
            if (state != FactState.Unknown && state != FactState.NotApplicable
                && state != FactState.Unsupported && state != FactState.Error)
                throw new ArgumentOutOfRangeException(nameof(state));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason code is required.", nameof(reason));
            return new Fact<T>(state, default, reason);
        }
    }

    /// <summary>Stable diagnostic IDs. Presentation maps these to localized text.</summary>
    internal static class PerceptionReasons
    {
        public const string Uninitialized = "Uninitialized";
        public const string Observed = "Observed";
        public const string TargetMissing = "TargetMissing";
        public const string TargetDisabled = "TargetDisabled";
        public const string TargetNotApplicable = "TargetNotApplicable";
        public const string InvalidTargetState = "InvalidTargetState";
        public const string InvalidTargetRatio = "InvalidTargetRatio";
        public const string NonFiniteReading = "NonFiniteReading";
        public const string NonPositiveMaximum = "NonPositiveMaximum";
        public const string UnsupportedNumericSemantics = "UnsupportedNumericSemantics";
        public const string OutsideStandardRange = "OutsideStandardRange";
        public const string RawUnitUnknown = "RawUnitUnknown";
    }
}
