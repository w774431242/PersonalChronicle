using System;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Settings
{
    /// <summary>设置页的页签（信息架构的顶层：按"你在干什么"分组，不按代码历史）。</summary>
    internal enum SettingsTab
    {
        Archive = 0,
        Workshop = 1,
        Ui = 2,
        Diag = 3,
        Economy = 4
        // ⚠ CHG-20261005-SETTINGS-REMOVE2：原 `Meal = 2`（「作息与三餐」页）已**整体删除** ——
        //   该页经 CHG-20261005-MEALBYSHIFT 后只剩 1 行只读指路；第一版把它并入车间页，
        //   本次按 PM 裁决连那行指路也一并移除 ⇒ 枚举值随之删除（本枚举只在 Settings 内部使用，
        //   且页签下标已改走 `VisibleTabs` 映射、不再做 int 转换 ⇒ 重编号安全）。
    }

    /// <summary>设置行的控件形态。每一种都必须有"命中检测 + 返回值"（组件不是展品）。</summary>
    internal enum SettingKind
    {
        /// <summary>开关（<c>InputAtoms.ToggleSwitch</c>）。</summary>
        Toggle = 0,
        /// <summary>整数滑块（原版 <c>Widgets.HorizontalSlider</c> + 自画当前值）。</summary>
        IntSlider = 1,
        /// <summary>浮点滑块。</summary>
        FloatSlider = 2,
        /// <summary>一行两个整型步进器（起 / 止 小时）。</summary>
        DualIntStep = 3,
        /// <summary>分段单选（<c>InputAtoms.Segmented</c>）。</summary>
        Segmented = 4,
        /// <summary>动作按钮。</summary>
        Action = 5,
        /// <summary>只读说明行（无控件，用于给玩家指路）。</summary>
        Note = 6
    }

    /// <summary>
    /// 来源角标的性质 —— 决定角标配色（**不是**"所有卡片长一个样"：角标是这套 UI 的语义色差）。
    /// </summary>
    internal enum SettingTone
    {
        Core = 0,
        New = 1,
        Fix = 2,
        Experimental = 3
    }

    /// <summary>
    /// 一行的完整元数据。**全部在构建期构造一次**（静态表），绘制期只读 ⇒ 零每帧分配。
    ///
    /// <para><b>文案键约定</b>：<see cref="TitleKey"/> / <see cref="UseKey"/> / <see cref="DetailKey"/>
    /// 都是**后缀**，实际键 = <c>PersonalChronicle.Settings.</c> + 后缀。</para>
    ///
    /// <para><b>角标两段式</b>（实机截图修正，2026-10-05）：Mod 设置窗口只有约 590px 宽，
    /// 完整编号 <c>CHG-20261004-MEALSCHEDULE</c>（约 210px）会把标题挤到截断
    /// ⇒ 列表里显示 <b>短形式</b>（<paramref name="Badge"/>，如 <c>1004·MEAL</c>），
    /// 悬停显示 <paramref name="BadgeFull"/>（完整编号，是稳定标识不是可译文本 ⇒ 不进 Keyed）。</para>
    /// </summary>
    internal sealed class SettingDef
    {
        public readonly SettingId Id;
        public readonly SettingKind Kind;

        /// <summary>所属分节标题键后缀（同页内相邻同分节的行归到同一个 SectionFrame）。</summary>
        public readonly string SectionKey;

        /// <summary>标题键后缀（**本页专用短标题**：长标题在 590px 下必然截断）。</summary>
        public readonly string TitleKey;

        /// <summary>角标短形式（列表显示；空 = 不显示角标）。</summary>
        public readonly string Badge;

        /// <summary>角标完整形式（悬停提示；空 = 不显示提示）。</summary>
        public readonly string BadgeFull;

        public readonly SettingTone Tone;

        /// <summary>「用途」一句话键后缀。</summary>
        public readonly string UseKey;

        /// <summary>「默认值 / 影响代价」键后缀（空 = 该行只显示用途）。</summary>
        public readonly string DetailKey;

        /// <summary>Segmented / Action 的选项文案键后缀（可空）。</summary>
        public readonly string[] OptionKeys;

        public SettingDef(SettingId id, SettingKind kind, string sectionKey, string titleKey,
            string badge, SettingTone tone, string useKey, string detailKey = null,
            string[] optionKeys = null, string badgeFull = null)
        {
            Id = id;
            Kind = kind;
            SectionKey = sectionKey;
            TitleKey = titleKey;
            Badge = badge ?? string.Empty;
            BadgeFull = badgeFull ?? (string.IsNullOrEmpty(badge) ? string.Empty : badge);
            Tone = tone;
            UseKey = useKey;
            DetailKey = detailKey;
            OptionKeys = optionKeys;
        }
    }

    /// <summary>
    /// 每行的唯一标识 —— 值读写由绘制层按它 <c>switch</c> 分派。
    /// <para><b>为什么用枚举 + switch 而不是每行一个闭包</b>：闭包在绘制路径里创建会每帧分配；
    /// 静态表 + 分派是零分配。</para>
    /// </summary>
    internal enum SettingId
    {
        Recording = 0,
        MaxEventsPerPawn = 1,
        AutoArchiveEnabled = 2,
        AutoArchiveSilver = 3,
        AutoArchivePurgeDays = 4,

        // ⚠ CHG-20261005-SETTINGS-REMOVE2：原 `OffShiftCancel = 10` 已移除 ——
        //   该项是「离班把班组工作类型取消勾选」的玩家否决权，PM 裁决「保持默认开启、不进 mod 设置」
        //   ⇒ 现恒开（`ChronicleSettings.EnableOffShiftCancel` 保留字段但不再暴露、不再 Scribe）。
        //   ⚠ 该默认化**扩大**了「写 0」的授权面（取消玩家退出权），已登记 **EXC-2026-026**。
        //   编号 10 作空档保留，不重排（沿既有惯例）。
        // ⚠ CHG-20261005-SETTINGS（§7-1）：原 `AutoRestoreOnShift = 11` 已移除 ——
        //   该项是「写 0 / 回写不对称」的**缺陷修复**而非行为偏好，不该让玩家关掉。
        //   现恒开（`ChronicleSettings.AutoRestoreOnShift` 保留字段但不再暴露、不再 Scribe）。
        //   编号 11 作空档保留，不重排（沿 20–24 的既有惯例）。
        IdleSplitEnabled = 12,
        AutoDetectWorkshopType = 13,
        ReplayTimetables = 14,
        DefaultRestPolicy = 15,

        // ⚠ CHG-20261005-MEALBYSHIFT：原先 20-24 是五个全局三餐设置项
        //   （MealEnabled / MealPriority / MealBreakfast / MealLunch / MealDinner），
        //   已随"删除全局三餐 mod 设置"一并移除（编号保留空档，不重排以免影响其它引用）。
        // ⚠ CHG-20261005-SETTINGS-REMOVE2：原 `ShiftMealPoint = 25`（「三餐已移到班次里配置」
        //   只读指路行）也已移除 —— PM 裁决连该指路行一并删。编号 25 作空档保留。

        Theme = 30,
        HiddenWorkTab = 31,
        PawnNeedSpeech = 32,

        PerfProbe = 40,
        DebugLiveCount = 41,
        StartupSelfTest = 42,
        EconomicInstitution = 50,
        // CHG-20261012-WORKGATE · W3：工作合同闸门（开关 / 白名单窗口 / 诊断落日志）。
        WorkContractGate = 51,
        WorkGateWhitelist = 52,
        WorkGateLog = 53,

    }

    /// <summary>
    /// 设置目录：**3 页 × 分节 × 行** 的静态声明表（顺序 = 界面顺序）。
    /// <para>CHG-20261005-SETTINGS / -REMOVE2：原「作息与三餐」页整页取消（含其只读指路行）；
    /// 「诊断」页仅在开发者模式下出现（见 <see cref="VisibleTabs"/>）⇒ 普通玩家实际只见 3 页。</para>
    /// 只声明"有哪些项"，取值/写值集中在 <see cref="SettingsDrawer"/> 的分派里（GOV-005）。
    /// </summary>
    internal static class SettingsCatalog
    {
        /// <summary>全部页（含诊断）。</summary>
        private static readonly SettingsTab[] AllTabs =
        {
            SettingsTab.Archive, SettingsTab.Workshop, SettingsTab.Economy, SettingsTab.Ui, SettingsTab.Diag
        };

        /// <summary>非开发者可见页 —— 诊断页只在开发者模式下出现（§7-8）。</summary>
        private static readonly SettingsTab[] PlayerTabs =
        {
            SettingsTab.Archive, SettingsTab.Workshop, SettingsTab.Economy, SettingsTab.Ui
        };

        private static SettingsTab[] visibleTabs;
        private static string[] visibleTabKeys;
        private static bool visibleTabsDev;

        /// <summary>
        /// 当前可见页签（按 <c>Prefs.DevMode</c> 重建）。重建时同步让
        /// <see cref="SettingsText"/> 的页签文案数组失效 —— 两者长度必须一致，
        /// 否则 <c>NavAtoms.TabBar</c> 会按文案长度越界或漏画。
        /// </summary>
        public static SettingsTab[] VisibleTabs
        {
            get
            {
                bool dev = Prefs.DevMode;
                if (visibleTabs != null && visibleTabsDev == dev)
                {
                    return visibleTabs;
                }
                visibleTabsDev = dev;
                visibleTabs = dev ? AllTabs : PlayerTabs;
                visibleTabKeys = new string[visibleTabs.Length];
                for (int i = 0; i < visibleTabs.Length; i++)
                {
                    visibleTabKeys[i] = TabKeyOf(visibleTabs[i]);
                }
                SettingsText.InvalidateTabs();
                return visibleTabs;
            }
        }

        /// <summary>与 <see cref="VisibleTabs"/> 同序、同长的页签文案键后缀。</summary>
        public static string[] VisibleTabKeys
        {
            get
            {
                _ = VisibleTabs;      // 先触发（可能的）重建，保证两者同序同长
                return visibleTabKeys;
            }
        }

        private static string TabKeyOf(SettingsTab t)
        {
            switch (t)
            {
                case SettingsTab.Workshop: return "Tab.Workshop";
                case SettingsTab.Economy: return "Tab.Economy";
                case SettingsTab.Ui: return "Tab.Ui";
                case SettingsTab.Diag: return "Tab.Diag";
                default: return "Tab.Archive";
            }
        }

        // ---- 分节标题键后缀（复用既有键，避免同一句话两份文案）----
        public const string SecGeneral = "SectionGeneral";
        public const string SecArchive = "AutoArchiveSection";
        public const string SecWorkshop = "WorkshopSection";
        public const string SecEconomy = "SectionEconomy";
        // ⚠ CHG-20261005-SETTINGS-REMOVE2：原 `SecMeal = "MealSection"` 已删除（三餐指路行移除后无人使用）
        public const string SecUi = "SectionUi";
        /// <summary>CHG-20261012-WORKGATE · W3：工作合同闸门分节（开关 + 白名单窗口 + 诊断）。</summary>
        public const string SecWorkGate = "WorkGateSection";
        public const string SecDiag = "SectionDiag";

        public static IReadOnlyList<SettingDef> RowsOf(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Archive: return ArchiveRows;
                // CHG-20261005-SETTINGS-REMOVE2：原 `case SettingsTab.Meal`（并入车间页的别名）
                // 已随 `SettingsTab.Meal` 的删除一并移除。
                case SettingsTab.Workshop: return WorkshopRows;
                case SettingsTab.Economy: return EconomyRows;
                case SettingsTab.Ui: return UiRows;
                default: return DiagRows;
            }
        }

        // ═══════════════════════════ 档案 ═══════════════════════════

        private static readonly SettingDef[] ArchiveRows =
        {
            new SettingDef(SettingId.Recording, SettingKind.Toggle, SecGeneral, "Title.Recording",
                "v1", SettingTone.Core, "Use.Recording"),
            new SettingDef(SettingId.MaxEventsPerPawn, SettingKind.IntSlider, SecGeneral, "MaxEventsPerPawn",
                "v1", SettingTone.Core, "Use.MaxEvents"),
            new SettingDef(SettingId.AutoArchiveEnabled, SettingKind.Toggle, SecArchive, "Title.AutoArchive",
                "v4", SettingTone.Core, "Use.AutoArchive"),
            new SettingDef(SettingId.AutoArchiveSilver, SettingKind.FloatSlider, SecArchive, "AutoArchiveSilverThreshold",
                "v4", SettingTone.Core, "Use.AutoArchiveSilver"),
            new SettingDef(SettingId.AutoArchivePurgeDays, SettingKind.IntSlider, SecArchive, "AutoArchivePurgeDays",
                "v4", SettingTone.Core, "Use.AutoArchivePurgeDays")
        };

        // ═══════════════════════════ 车间 ═══════════════════════════

        private static readonly SettingDef[] WorkshopRows =
        {
            // ⚠ CHG-20261005-SETTINGS-REMOVE2：原「离班停工」开关（SettingId 10）已移除 ——
            //   PM 裁决「保持默认开启、不写入 mod 设置」⇒ 恒开、不再暴露、不再 Scribe。
            //   该项是"写 0"的玩家否决权，取消退出权**扩大**了 EXC-2026-016 声明的授权面
            //   ⇒ 已单独登记 **EXC-2026-026**（E2）。
            // ⚠ CHG-20261005-SETTINGS（§7-1）：原「在班自动回写」开关（SettingId 11）已移除 ——
            //   它修的是"写 0 / 回写不对称"的缺陷，不是行为偏好；现恒开，不再给玩家关闭的入口。
            new SettingDef(SettingId.IdleSplitEnabled, SettingKind.Toggle, SecWorkshop, "Title.IdleSplit",
                "1004·IDLE", SettingTone.New, "Use.IdleSplit", "Detail.IdleSplit",
                null, "CHG-20261004-IDLE"),
            new SettingDef(SettingId.AutoDetectWorkshopType, SettingKind.Toggle, SecWorkshop, "Title.AutoDetect",
                "1004·AUTO", SettingTone.Fix, "Use.AutoDetect", "Detail.AutoDetect",
                null, "CHG-20261004-AUTOTYPE"),
            new SettingDef(SettingId.ReplayTimetables, SettingKind.Toggle, SecWorkshop, "Title.Replay",
                "1004·WS-SET", SettingTone.Fix, "Use.ReplayTimetables", "Detail.ReplayTimetables",
                null, "CHG-20261004-WSSETTINGS"),
            new SettingDef(SettingId.DefaultRestPolicy, SettingKind.Segmented, SecWorkshop, "Title.RestPolicy",
                "", SettingTone.Core, "Use.RestPolicy", "Detail.RestPolicy",
                new[] { "RestShort.Fixed", "RestShort.Natural", "RestShort.Rotate" })
            // ⚠ CHG-20261005-SETTINGS-REMOVE2：原追加在尾部的 `SecMeal` 只读指路行
            //   （「三餐已移到班次里配置」）已按 PM 裁决**一并删除**；`SecMeal` 常量随之移除。
        };

        // ⚠ CHG-20261005-SETTINGS（§7-7）：原 `MealRows`（「作息与三餐」页）已删除 ——
        //   该页经 CHG-20261005-MEALBYSHIFT 后只剩 1 行只读指路，属空页。
        // ⚠ CHG-20261005-SETTINGS-REMOVE2：连那行只读指路也已删除（见 `WorkshopRows` 尾部注释）。

        // ═══════════════════════════ 界面 ═══════════════════════════

        private static readonly SettingDef[] EconomyRows =
        {
            new SettingDef(SettingId.EconomicInstitution, SettingKind.Toggle, SecEconomy, "Title.EconomicInstitution",
                "1008·ECON", SettingTone.Experimental, "Use.EconomicInstitution", "Detail.EconomicInstitution",
                null, "CHG-20261008-ECON-MODE-GATES")
        };
        private static readonly SettingDef[] UiRows =
        {
            new SettingDef(SettingId.PawnNeedSpeech, SettingKind.Toggle, SecUi, "Title.PawnNeedSpeech",
                "1010", SettingTone.New, "Use.PawnNeedSpeech", "Detail.PawnNeedSpeech"),
            // ⚠ CHG-20261005-SETTINGS（D2 修复）：原声明**漏传 optionKeys** ⇒
            //   SettingsText.Options 返回 null ⇒ PaintControl 的 Segmented 分支直接 break
            //   ⇒ 本行**根本不画控件**（实机表现为"只有标题与角标、无可点控件"）。
            //   复用 Archive.xml 既有的主题名键（含 PersonalChronicle. 前缀 ⇒ Options 走 Raw 路径）。
            new SettingDef(SettingId.Theme, SettingKind.Segmented, SecUi, "Title.Theme",
                "v1.1.4", SettingTone.Core, "Use.Theme", null,
                new[]
                {
                    "PersonalChronicle.UI.Theme.Epic",
                    "PersonalChronicle.UI.Theme.Gothic",
                    "PersonalChronicle.UI.Theme.Steampunk"
                }),
            new SettingDef(SettingId.HiddenWorkTab, SettingKind.Action, SecUi, "WorkTabHiddenTitle",
                "1004·WORKTAB", SettingTone.Fix, "Use.HiddenWorkTab",
                null, null, "CHG-20261004-WORKTAB-ORIGINAL"),
            // ── CHG-20261012-WORKGATE · W3：工作合同闸门（接进**活设置路径**；此前误挂进 LegacySettingsContents 死块）──
            new SettingDef(SettingId.WorkContractGate, SettingKind.Toggle, SecWorkGate, "Title.WorkGate",
                "WORKGATE", SettingTone.New, "Use.WorkGate", "Detail.WorkGate"),
            new SettingDef(SettingId.WorkGateWhitelist, SettingKind.Action, SecWorkGate, "Title.WorkGateWhitelist",
                "WORKGATE", SettingTone.New, "Use.WorkGateWhitelist", "Detail.WorkGateWhitelist"),
            new SettingDef(SettingId.WorkGateLog, SettingKind.Action, SecWorkGate, "Title.WorkGateLog",
                "DIAG", SettingTone.Experimental, "Use.WorkGateLog", "Detail.WorkGateLog")
        };

        // ═══════════════════════════ 诊断 ═══════════════════════════

        private static readonly SettingDef[] DiagRows =
        {
            new SettingDef(SettingId.PerfProbe, SettingKind.Toggle, SecDiag, "Title.PerfProbe",
                "PERF", SettingTone.Experimental, "Use.PerfProbe", "Detail.PerfProbe"),
            new SettingDef(SettingId.DebugLiveCount, SettingKind.Toggle, SecDiag, "Title.DebugLive",
                "DEV", SettingTone.Experimental, "Use.DebugLiveCount"),
            new SettingDef(SettingId.StartupSelfTest, SettingKind.Toggle, SecDiag, "Title.StartupSelfTest",
                "DEV", SettingTone.Experimental, "Use.StartupSelfTest", "Detail.StartupSelfTest")
        };
    }
}
