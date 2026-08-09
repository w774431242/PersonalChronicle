namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Stable category identifiers used by <see cref="ObjectRef.CategoryKey"/> and
    /// <see cref="ArchiveObject.CategoryKey"/>. These are data keys (language
    /// independent, never translated directly); UI resolves labels through
    /// ArchiveCategoryDef defs.
    /// </summary>
    public static class ArchiveCategoryKeys
    {
        public const string Pawn = "Pawn";
        public const string Thing = "Thing";
        public const string Battle = "Battle";
        public const string Location = "Location";
    }
}
