namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// P2-6: navigation target kind, decoupled from the main window's private
    /// <c>NavTarget</c> enum. Read-model builders that need to express a navigation
    /// intent use this; the window maps it back to its internal navigation state.
    /// Keeps read models free of any window-specific type so they can be unit-tested
    /// in isolation. Reserved for future read-model-driven navigation.
    /// </summary>
    public enum NavTargetKind
    {
        None,
        Pawn,
        Weapon,
        Event
    }
}
