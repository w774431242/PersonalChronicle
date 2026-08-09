namespace PersonalChronicle.Api
{
    /// <summary>
    /// Outcome of a single capture write through <see cref="IArchiveEventSink"/>.
    /// Lets the caller distinguish a real persist from an idempotent drop
    /// (duplicate within the same game session) or a rejected write, so
    /// third-party mods can log at the right severity.
    /// </summary>
    public enum CaptureResult
    {
        /// <summary>Event was accepted and persisted.</summary>
        Accepted,

        /// <summary>
        /// Identical DeduplicationKey already seen this session — write was a
        /// safe no-op (idempotent). Not an error.
        /// </summary>
        Duplicate,

        /// <summary>
        /// Write rejected: recording disabled, per-pawn cap reached, or event
        /// data invalid. The archive state is unchanged.
        /// </summary>
        Rejected,

        /// <summary>
        /// API not available (no active game / component). The caller should not
        /// treat this as a permanent failure.
        /// </summary>
        Unavailable
    }
}
