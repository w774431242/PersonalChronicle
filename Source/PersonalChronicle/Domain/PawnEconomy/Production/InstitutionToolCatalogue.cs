using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy;

namespace PersonalChronicle.Domain.PawnEconomy.Production
{
    /// <summary>M10 §2: the twelve institution tool templates. A template describes capability only —
    /// which institution kind it serves and which vanilla work types it needs. It never creates a
    /// building, never starts a bill and never competes with the workshop executor.</summary>
    internal enum InstitutionToolTemplate
    {
        LabourBoard = 1, WorkshopBench = 2, SettlementDesk = 3, ShopCounter = 4, ExamRoom = 5, BedWard = 6,
        Kitchen = 7, TailorBench = 8, Clinic = 9, Nursery = 10, School = 11, Warehouse = 12
    }
    internal sealed class ToolTemplateDefinition
    {
        public InstitutionToolTemplate Template { get; }
        public InstitutionKind ServesKind { get; }
        public IReadOnlyList<string> WorkTypeDefNames { get; }
        public bool RequiresStock { get; }
        public ToolTemplateDefinition(InstitutionToolTemplate template, InstitutionKind servesKind,
            IEnumerable<string> workTypeDefNames, bool requiresStock)
        {
            if (!Enum.IsDefined(typeof(InstitutionToolTemplate), template)) throw new ArgumentOutOfRangeException(nameof(template));
            if (!Enum.IsDefined(typeof(InstitutionKind), servesKind)) throw new ArgumentOutOfRangeException(nameof(servesKind));
            Template = template; ServesKind = servesKind; RequiresStock = requiresStock;
            var rows = new List<string>();
            foreach (var name in workTypeDefNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(name) || rows.Count == 4) continue;
                rows.Add(name);
            }
            WorkTypeDefNames = rows.AsReadOnly();
        }
    }
    internal static class InstitutionToolCatalogue
    {
        public const int TemplateCount = 12;
        private static readonly ToolTemplateDefinition[] Rows =
        {
            new ToolTemplateDefinition(InstitutionToolTemplate.LabourBoard, InstitutionKind.LaborOffice, new[] { "Crafting" }, false),
            new ToolTemplateDefinition(InstitutionToolTemplate.WorkshopBench, InstitutionKind.WorkshopOffice, new[] { "Crafting", "Construction" }, true),
            new ToolTemplateDefinition(InstitutionToolTemplate.SettlementDesk, InstitutionKind.SettlementOffice, Array.Empty<string>(), true),
            new ToolTemplateDefinition(InstitutionToolTemplate.ShopCounter, InstitutionKind.Shop, Array.Empty<string>(), true),
            new ToolTemplateDefinition(InstitutionToolTemplate.ExamRoom, InstitutionKind.ExamCenter, new[] { "Doctor" }, false),
            new ToolTemplateDefinition(InstitutionToolTemplate.BedWard, InstitutionKind.Shop, Array.Empty<string>(), false),
            new ToolTemplateDefinition(InstitutionToolTemplate.Kitchen, InstitutionKind.Shop, new[] { "Cooking" }, true),
            new ToolTemplateDefinition(InstitutionToolTemplate.TailorBench, InstitutionKind.Shop, new[] { "Tailoring" }, true),
            new ToolTemplateDefinition(InstitutionToolTemplate.Clinic, InstitutionKind.ExamCenter, new[] { "Doctor" }, true),
            new ToolTemplateDefinition(InstitutionToolTemplate.Nursery, InstitutionKind.ExamCenter, Array.Empty<string>(), false),
            new ToolTemplateDefinition(InstitutionToolTemplate.School, InstitutionKind.ExamCenter, Array.Empty<string>(), false),
            new ToolTemplateDefinition(InstitutionToolTemplate.Warehouse, InstitutionKind.SettlementOffice, new[] { "Hauling" }, true)
        };
        public static int Count => Rows.Length;
        public static ToolTemplateDefinition Of(InstitutionToolTemplate template)
        {
            for (int i = 0; i < Rows.Length; i++) if (Rows[i].Template == template) return Rows[i];
            return null;
        }
        public static IReadOnlyList<InstitutionToolTemplate> ForKind(InstitutionKind kind)
        {
            var rows = new List<InstitutionToolTemplate>();
            for (int i = 0; i < Rows.Length; i++) if (Rows[i].ServesKind == kind) rows.Add(Rows[i].Template);
            return rows.AsReadOnly();
        }
    }
}