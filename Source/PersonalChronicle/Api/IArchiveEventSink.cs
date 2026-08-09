namespace PersonalChronicle.Api
{
    /// <summary>
    /// Unified write entry point for archive events. External mods and (eventually)
    /// the internal capture layer use this single contract instead of the
    /// scattered legacy <c>On*</c> methods. Idempotent within a game session via
    /// <see cref="ArchiveEventInput.DeduplicationKey"/>.
    /// </summary>
    public interface IArchiveEventSink
    {
        /// <summary>
        /// Attempts to record an event. Never throws on bad input — returns a
        /// <see cref="CaptureResult"/> describing the outcome. The archive state
        /// is only mutated on <see cref="CaptureResult.Accepted"/>.
        /// </summary>
        CaptureResult TryRecord(ArchiveEventInput input);
    }
}
