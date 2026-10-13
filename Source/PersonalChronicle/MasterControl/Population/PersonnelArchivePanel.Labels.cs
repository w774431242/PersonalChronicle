using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> — 展示文案与标记的解析层。
    /// </summary>
    /// <remarks>
    /// · 职责：把存档里的"键"翻译成界面上的"文案"——标签页名、分类名、对象显示名、事件名与描述、
    ///   种类/派系/事物/事件 def 标签、事件字形与配色、日期格式化。
    /// · 调用方：PersonnelArchivePanel 的各绘制 partial（卡片、时间轴、详情页）。
    /// · 铁律（本文件最重要的一条，勿改）：
    ///   - 任何 Def 查找失败都不得抛异常、不得产生红字错误。统一降级为原始 defName，
    ///     并只经 LogMissingDefOnce 警告一次。第三方 mod 被卸载属预期场景，不是缺陷。
    ///   - 新增标签解析必须复用这条降级链，不要在各绘制文件里直接写 DefDatabase 查询。
    /// · 约定：
    ///   - 文案一律走 Translate()，键前缀见 PersonalChronicle.UI.*；禁止在此硬编码中文文案。
    ///   - 事件字形与配色的分支依据是 ChronicleEventDef.kind，
    ///     新增事件种类时必须同步补 EventTypeToGlyph 与 EventTypeToColor 两处 switch。
    ///   - FormatDate 只有 tick 小于 0 才算"未知"；0 是合法的新档第 1 天，勿改成小于等于 0。
    /// </remarks>
    public sealed partial class PersonnelArchivePanel
    {

        /// <summary>
        /// 取标签页显示名。
        /// </summary>
        /// <param name="tabKey">标签页键，与 <c>PersonalChronicle.UI.Tab.</c> 拼接成翻译键。</param>
        /// <returns>翻译后的标签页名。</returns>
        private static string TabLabel(string tabKey)
        {
            return ("PersonalChronicle.UI.Tab." + tabKey).Translate().ToString();
        }

        /// <summary>
        /// 取分类显示名：优先用 ArchiveCategoryDef.label，缺失时回退到翻译键。
        /// </summary>
        /// <param name="categoryKey">分类键。</param>
        /// <returns>分类名；Def 未定义或无 label 时返回 <c>PersonalChronicle.UI.Category.*</c> 的翻译结果。</returns>
        private static string CategoryLabel(string categoryKey)
        {
            ArchiveCategoryDef def = DefDatabase<ArchiveCategoryDef>.AllDefs
                .FirstOrDefault(d => d.categoryKey == categoryKey);
            if (def != null && !def.label.NullOrEmpty())
            {
                return def.label;
            }
            return ("PersonalChronicle.UI.Category." + categoryKey).Translate().ToString();
        }

        /// <summary>
        /// 取归档对象的主显示名，按对象类型分派到对应的 label 解析。
        /// </summary>
        /// <param name="obj">归档对象；为 null 时返回空串。</param>
        /// <returns>显示名；兜底顺序为 LabelSnapshot 再到 StableId，保证永不返回 null。</returns>
        /// <remarks>
        /// LocationObject 优先 CellLabel 再 LabelSnapshot：单元格标签更具体，
        /// 只有缺失时才退化到归档快照名。新增对象类型需在此补充分支，否则会落到通用兜底。
        /// </remarks>
        private static string ObjectDisplayLabel(ArchiveObject obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }
            if (obj is PawnObject pawn)
            {
                return string.IsNullOrEmpty(pawn.LabelShort) ? pawn.StableId : pawn.LabelShort;
            }
            if (obj is ThingObject thing)
            {
                return ThingDefLabel(thing.ThingDefName);
            }
            if (obj is BattleObject battle)
            {
                return IncidentDefLabel(battle.IncidentDefName);
            }
            if (obj is LocationObject location)
            {
                if (!string.IsNullOrEmpty(location.CellLabel))
                {
                    return location.CellLabel;
                }
                if (!string.IsNullOrEmpty(location.LabelSnapshot))
                {
                    return location.LabelSnapshot;
                }
                return location.StableId;
            }
            return !string.IsNullOrEmpty(obj.LabelSnapshot) ? obj.LabelSnapshot : obj.StableId;
        }

        /// <summary>
        /// 取归档对象的副标题（第二行小字）。
        /// </summary>
        /// <param name="obj">归档对象。</param>
        /// <returns>pawn 返回"存活/死亡 · 角色"，地点返回 MapId，其余类型返回空串。</returns>
        /// <remarks>返回空串而非 null：绘制层直接拼接即可，无需再判空。</remarks>
        private static string ObjectSubLabel(ArchiveObject obj)
        {
            if (obj is PawnObject pawn)
            {
                string life = pawn.IsArchived
                    ? "PersonalChronicle.UI.Dead".Translate().ToString()
                    : "PersonalChronicle.UI.Alive".Translate().ToString();
                return life + " · " + RoleLabel(pawn.Role);
            }
            if (obj is LocationObject location)
            {
                return location.MapId;
            }
            return string.Empty;
        }

        /// <summary>
        /// 取 pawn 角色的显示名（奴隶 / 囚犯 / 自由殖民者）。
        /// </summary>
        /// <param name="role">角色枚举。</param>
        /// <returns>对应翻译文案。</returns>
        /// <remarks>default 分支落到自由殖民者：未知枚举值按自由人处理，避免显示空白。</remarks>
        private static string RoleLabel(PawnRole role)
        {
            switch (role)
            {
                case PawnRole.Slave:
                    return "PersonalChronicle.UI.RoleSlave".Translate().ToString();
                case PawnRole.Prisoner:
                    return "PersonalChronicle.UI.RolePrisoner".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.RoleFreeColonist".Translate().ToString();
            }
        }

        /// <summary>
        /// 取角色徽标的配色。
        /// </summary>
        /// <param name="role">角色枚举。</param>
        /// <returns>奴隶为金色、囚犯为红色，其余（自由殖民者）用 AlivePill。</returns>
        /// <remarks>颜色一律取自 UITheme / ArchiveUiStyle 令牌，禁止在本文件内写 new Color。</remarks>
        private static Color RolePillColor(PawnRole role)
        {
            switch (role)
            {
                case PawnRole.Slave:
                    return UITheme.PillGold;
                case PawnRole.Prisoner:
                    return UITheme.PillRed;
                default:
                    return AlivePill;
            }
        }

        /// <summary>
        /// 取归档卡片的强调色。
        /// </summary>
        /// <param name="obj">归档对象；可为 null。</param>
        /// <returns>pawn 用角色色；属 pawn 分类但非 PawnObject 时用 Info 色；其余用 Accent 色。</returns>
        /// <remarks>先判 PawnObject 再判分类键是有意的：前者能给出角色语义，后者只是兜底。</remarks>
        private static Color ArchiveCardAccent(ArchiveObject obj)
        {
            PawnObject pawn = obj as PawnObject;
            if (pawn != null)
            {
                return RolePillColor(pawn.Role);
            }
            return obj != null && obj.CategoryKey == ArchiveCategoryKeys.Pawn
                ? ArchiveUiStyle.Info
                : ArchiveUiStyle.Accent;
        }

        /// <summary>
        /// 解析对象引用（ObjectRef）的显示名。
        /// </summary>
        /// <param name="r">对象引用；为 null 时返回空串。</param>
        /// <param name="service">归档服务，用于按 StableId 反查对象；可为 null。</param>
        /// <returns>优先 LabelSnapshot，其次反查对象的显示名，最后退到 StableId。</returns>
        /// <remarks>
        /// 快照优先是有意的：引用指向的对象可能已经不存在（跨存档或 mod 卸载），
        /// 此时反查必然失败，快照是唯一能还原"当时名称"的数据，不要调换优先级。
        /// </remarks>
        private static string ResolveRefLabel(ObjectRef r, IArchiveService service)
        {
            if (r == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(r.LabelSnapshot))
            {
                return r.LabelSnapshot;
            }
            if (service != null && !string.IsNullOrEmpty(r.StableId))
            {
                ArchiveObject o = service.GetObject(r.StableId);
                if (o != null)
                {
                    return ObjectDisplayLabel(o);
                }
            }
            return r.StableId ?? string.Empty;
        }

        /// <summary>
        /// 取事件的显示名。
        /// </summary>
        /// <param name="ev">编年史事件；为 null 时返回空串。</param>
        /// <returns>Def 存在且配了 labelKey 时返回其翻译，否则返回原始 TypeKey。</returns>
        internal static string EventName(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.labelKey))
            {
                return def.labelKey.Translate().ToString();
            }
            return ev.TypeKey;
        }

        /// <summary>
        /// 取事件的描述文案。
        /// </summary>
        /// <param name="ev">编年史事件；为 null 时返回空串。</param>
        /// <returns>Def 存在且配了 descriptionKey 时返回其翻译；否则返回空串。</returns>
        /// <remarks>与 EventName 的兜底策略不同：这里缺失时返回空串而不是 TypeKey，
        /// 因为向玩家展示原始键没有意义，留白更友好。</remarks>
        internal static string EventDescription(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def == null || string.IsNullOrEmpty(def.descriptionKey))
            {
                return string.Empty;
            }
            return def.descriptionKey.Translate().ToString();
        }

        /// <summary>
        /// 取事件类型的显示名。
        /// </summary>
        /// <param name="typeKey">事件类型键；为空时按"其他"处理。</param>
        /// <returns>Def.LabelCap；Def 缺失或标签为空时统一回退到"其他"的翻译文案。</returns>
        private static string ChronicleEventTypeLabel(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey))
            {
                return "PersonalChronicle.UI.EvOther".Translate().ToString();
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def != null && !string.IsNullOrEmpty(def.LabelCap))
            {
                return def.LabelCap;
            }
            return "PersonalChronicle.UI.EvOther".Translate().ToString();
        }

        /// <summary>
        /// 取事件类型对应的时间轴字形符号。
        /// </summary>
        /// <param name="typeKey">事件类型键。</param>
        /// <returns>对应 kind 的符号；Def 缺失或未知 kind 时返回圆点。</returns>
        /// <remarks>
        /// 新增 ChronicleEventKind 时必须同时在此补符号、在 EventTypeToColor 补配色；
        /// 否则新类型会静默退化成"圆点 + 默认色"，从界面上很难发现问题。
        /// </remarks>
        internal static string EventTypeToGlyph(string typeKey)
        {
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def == null)
            {
                return "•";
            }
            switch (def.kind)
            {
                case ChronicleEventKind.Join:
                    return "✚";
                case ChronicleEventKind.Death:
                    return "✝";
                case ChronicleEventKind.Battle:
                    return "⚔";
                case ChronicleEventKind.Social:
                    return "❖";
                case ChronicleEventKind.Craft:
                    return "⚒";
                case ChronicleEventKind.Built:
                    return "▣";
                // ⚠ CHG-20261006-EVKEY：补齐 Leave / RoleChanged（原落 default ⇒ 与"其他"同为「•」）。
                //   二者**有活消费者**：⑤ 事件流（ManufacturingEventsPanel 经 EventTypeToGlyph/Color 取字形与色）。
                //   符号取**基础箭头**（←/↔，Arial 覆盖率高于 ⇥/⇄ 一类的 Arrows 扩展区）。
                case ChronicleEventKind.Leave:
                    return "←";
                case ChronicleEventKind.RoleChanged:
                    return "↔";
                case ChronicleEventKind.Other:
                default:
                    return "•";
            }
        }

        /// <summary>
        /// 取事件类型对应的时间轴配色。
        /// </summary>
        /// <param name="typeKey">事件类型键。</param>
        /// <returns>ArchiveUiStyle.Timeline* 令牌色；Def 缺失或未知 kind 时返回 TimelineOther。</returns>
        /// <remarks>与 EventTypeToGlyph 共用 kind 分支，两者必须成对维护。</remarks>
        internal static Color EventTypeToColor(string typeKey)
        {
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def == null)
            {
                return ArchiveUiStyle.TimelineOther;
            }
            switch (def.kind)
            {
                case ChronicleEventKind.Join:
                    return ArchiveUiStyle.TimelineJoin;
                case ChronicleEventKind.Death:
                    return ArchiveUiStyle.TimelineDeath;
                case ChronicleEventKind.Battle:
                    return ArchiveUiStyle.TimelineBattle;
                case ChronicleEventKind.Social:
                    return ArchiveUiStyle.TimelineSocial;
                case ChronicleEventKind.Craft:
                    return ArchiveUiStyle.TimelineCraft;
                case ChronicleEventKind.Built:
                    return ArchiveUiStyle.TimelineBuilt;
                // ⚠ CHG-20261006-EVKEY：Leave / RoleChanged **暂无专用令牌** —— 新增 TimelineLeave /
                //   TimelineRoleChanged 需同步改动主题系统（Palette + 三套主题预设），属设计决策，
                //   另批处理。此处**显式列出**并落 Other 色（靠上方 glyph 区分），
                //   以维持本文件"两函数共用 kind 分支、必须成对维护"的契约。
                case ChronicleEventKind.Leave:
                case ChronicleEventKind.RoleChanged:
                    return ArchiveUiStyle.TimelineOther;
                case ChronicleEventKind.Other:
                default:
                    return ArchiveUiStyle.TimelineOther;
            }
        }

        /// <summary>
        /// 取 pawn 种类（PawnKindDef）的显示名。
        /// </summary>
        /// <param name="record">pawn 记录；为 null 或无 KindDefName 时返回空串。</param>
        /// <returns>Def.label；Def 缺失时记录一次警告并返回原始 KindDefName。</returns>
        private static string KindLabel(PawnRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.KindDefName))
            {
                return string.Empty;
            }
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(record.KindDefName);
            if (kindDef != null && !string.IsNullOrEmpty(kindDef.label))
            {
                return kindDef.label;
            }
            if (kindDef == null)
            {
                LogMissingDefOnce(record.KindDefName);
            }
            return record.KindDefName;
        }

        /// <summary>
        /// 取 pawn 所属派系（FactionDef）的显示名。
        /// </summary>
        /// <param name="record">pawn 记录；为 null 或无 FactionDefName 时返回空串。</param>
        /// <returns>Def.label；Def 缺失时记录一次警告并返回原始 FactionDefName。</returns>
        private static string FactionLabel(PawnRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.FactionDefName))
            {
                return string.Empty;
            }
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(record.FactionDefName);
            if (factionDef != null && !string.IsNullOrEmpty(factionDef.label))
            {
                return factionDef.label;
            }
            if (factionDef == null)
            {
                LogMissingDefOnce(record.FactionDefName);
            }
            return record.FactionDefName;
        }

        /// <summary>
        /// 取事物（ThingDef）的显示名。
        /// </summary>
        /// <param name="defName">ThingDef 的 defName。</param>
        /// <returns>Def.label；Def 缺失时降级为原始 defName。</returns>
        /// <remarks>降级是刻意设计：归档对象可能引用已被移除的第三方 mod 的 Def，
        /// 此时显示原始键远好于抛异常或刷红字错误，切勿改成抛异常。</remarks>
        private static string ThingDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            // Missing Def (e.g. third-party mod removed after the object was
            // archived): fall back to the raw defName — never crash, never red-text.
            return defName;
        }

        /// <summary>
        /// 取产物名称：先按 ThingDef 解析，失败再按 ThingCategoryDef 解析。
        /// </summary>
        /// <param name="defName">产物键，可能是具体事物也可能是事物类别。</param>
        /// <returns>任一级解析成功即返回其 label；都失败则记录一次警告并返回原始键。</returns>
        /// <remarks>两级查找是必要的：生产记录里既可能存具体 ThingDef，也可能存 ThingCategoryDef
        /// （例如按类别汇总的产物）。只查一层会让类别型产物大面积降级成原始键。</remarks>
        private static string ProductionDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
            if (cat != null && !string.IsNullOrEmpty(cat.label))
            {
                return cat.label;
            }
            // Neither resolved: it is a genuine missing def (third-party mod
            // uninstalled) — log once and degrade to the raw key.
            LogMissingDefOnce(defName);
            return defName;
        }

        /// <summary>
        /// 取事件（IncidentDef）的显示名。
        /// </summary>
        /// <param name="defName">IncidentDef 的 defName。</param>
        /// <returns>Def.label；Def 缺失时记录一次警告并返回原始 defName。</returns>
        private static string IncidentDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        /// <summary>
        /// 取派系（FactionDef）的显示名，按 defName 直接查询。
        /// </summary>
        /// <param name="defName">FactionDef 的 defName。</param>
        /// <returns>Def.label；Def 缺失时返回原始 defName。</returns>
        /// <remarks>
        /// 与 FactionLabel 的关键差别：这里刻意不写缺失日志。
        /// 本方法用于高频调用路径，若在这里记日志，同一个缺失派系会在每帧刷新时重复刷屏；
        /// 缺失统计统一交给带日志的 FactionLabel 负责。
        /// </remarks>
        private static string FactionDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }

        /// <summary>
        /// 记录一次缺失 Def 的警告，同一 defName 只记一次。
        /// </summary>
        /// <param name="defName">缺失的 defName；为空时直接返回。</param>
        /// <remarks>
        /// 级别用 Warning 而不是 Error：第三方 mod 卸载后残留引用属预期情况，
        /// 升到 Error 会在正常游戏中刷红字，属于误报。
        /// 去重依赖静态集合 loggedMissingDefs，量级很小，无需清理策略。
        /// </remarks>
        private static void LogMissingDefOnce(string defName)
        {
            if (string.IsNullOrEmpty(defName) || loggedMissingDefs.Contains(defName))
            {
                return;
            }
            loggedMissingDefs.Add(defName);
            // Warning (not Error): expected after mod removal; no red-text.
            ChronicleLog.Warning(ChronicleLog.Category.Ui, "missing def for display: " + defName);
        }

        /// <summary>
        /// 把绝对 tick 格式化为可读日期。
        /// </summary>
        /// <param name="tick">绝对 tick；负数是"未知"哨兵值。</param>
        /// <returns>日期字符串；tick 为负时返回"未知日期"的翻译文案。</returns>
        /// <remarks>
        /// 只有 tick 小于 0 才算未知 —— tick 等于 0 是新档第 1 天，
        /// 开局殖民者的 JoinTick 就是 0，属于合法日期。
        /// 若把条件改成"小于等于 0"，所有开局殖民者的入队日期都会错误地显示成"未知"。
        /// </remarks>
        internal static string FormatDate(long tick)
        {
            // tick 0 是新档第 1 天（开局殖民者 JoinTick=0 即此），是合法日期；
            // 仅 -1（未知哨兵）才显示"未知"。
            if (tick < 0L)
            {
                return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            }
            return GenDate.DateReadoutStringAt(tick, Vector2.zero);
        }

        /// <summary>
        /// 取死因的显示名。
        /// </summary>
        /// <param name="deathCauseKey">死因翻译键；为空时返回空串。</param>
        /// <returns>翻译后的死因文案。</returns>
        private static string CauseLabel(string deathCauseKey)
        {
            if (string.IsNullOrEmpty(deathCauseKey))
            {
                return string.Empty;
            }
            return deathCauseKey.Translate().ToString();
        }

        /// <summary>
        /// 判断事件是否为社交类（用于时间轴筛选与分组）。
        /// </summary>
        /// <param name="ev">编年史事件。</param>
        /// <returns>Def 存在且 kind 为 Social 时返回 true。</returns>
        /// <remarks>判定依据是 ChronicleEventDef.kind 而非硬编码 TypeKey：
        /// 第三方 mod 新增的事件只要标了 kind 就能被正确归类，无需改动本文件。</remarks>
        private static bool IsSocialEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Social;
        }

        /// <summary>判断事件是否为死亡类（kind 为 Death）。</summary>
        /// <param name="ev">编年史事件。</param>
        /// <returns>Def 存在且 kind 为 Death 时返回 true。</returns>
        private static bool IsDeathEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Death;
        }

        /// <summary>判断事件是否为战斗类（kind 为 Battle）。</summary>
        /// <param name="ev">编年史事件。</param>
        /// <returns>Def 存在且 kind 为 Battle 时返回 true。</returns>
        private static bool IsBattleEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Battle;
        }

        /// <summary>判断事件是否为制作类（kind 为 Craft）。</summary>
        /// <param name="ev">编年史事件。</param>
        /// <returns>Def 存在且 kind 为 Craft 时返回 true。</returns>
        private static bool IsCraftEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Craft;
        }

        /// <summary>判断事件是否为建造类（kind 为 Built）。</summary>
        /// <param name="ev">编年史事件。</param>
        /// <returns>Def 存在且 kind 为 Built 时返回 true。</returns>
        private static bool IsBuiltEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Built;
        }
    }

    /// <summary>
    /// 归档缓存字典的扩展方法，供绘制层安全地取列表长度。
    /// </summary>
    internal static class ArchiveCacheExtensions
    {
        /// <summary>
        /// 安全获取缓存字典中某个分类键对应的列表数量。
        /// </summary>
        /// <param name="dict">分类到对象列表的缓存字典；可为 null。</param>
        /// <param name="key">分类键。</param>
        /// <returns>列表元素数；字典为 null、键不存在或列表为 null 时一律返回 0。</returns>
        /// <remarks>绘制层在标签页尚未初始化时也会调用本方法，因此必须容忍 null。
        /// 不要改成索引器访问，那会在键缺失时抛 KeyNotFoundException 并打断整帧绘制。</remarks>
        public static int GetCount(this Dictionary<string, List<ArchiveObject>> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out List<ArchiveObject> list) && list != null)
            {
                return list.Count;
            }
            return 0;
        }
    }
}
