// 总览数据面板 · 字段注册表「启动期自动扫描」部分（partial）。
//
// 铁律 6.3 · 预划分 partial：扫描是独立内聚区块（排重集 / 分类白名单 / StatDef+NeedDef 扫描 /
// 来源推断），与主注册表（字段定义 + 来源分组）解耦，各自 < 400 行（铁律 6.2）。
//
// 设计：每次游戏启动 StaticConstructorOnStartup 时重新构建 All（静态列表），
// 天然支持"增删 mod 后下次启动自动刷新"，无需持久化缓存。
// 自动注册的第三方字段 Label 直接用 Def.LabelCap（游戏当前语言，无中文翻译键）。
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class OverviewFieldRegistry
    {
        /// <summary>硬编码 vanilla 字段已覆盖（精选展示）的原版 StatDef defName，用于自动扫描排重，避免与精选卡重复。</summary>
        private static readonly HashSet<string> KnownVanillaStatDefs = new HashSet<string>
        {
            "WorkSpeedGlobal", "ConstructionSpeed", "PlantWorkSpeed", "ResearchSpeed", "MiningSpeed",
            "CookSpeed", "SmeltingSpeed", "SmoothingSpeed", "ReadingSpeed",
            "MoveSpeed", "CarryingCapacity",
            "MeleeDPS", "ShootingAccuracyPawn", "ArmorRating_Sharp", "ArmorRating_Blunt", "MeleeDodgeChance", "PainShockThreshold",
            "SocialImpact", "TradePriceImprovement", "NegotiationAbility", "MedicalTendQuality", "MedicalSurgerySuccessChance",
            "ImmunityGainSpeed",
        };

        /// <summary>硬编码 vanilla 字段已覆盖（精选展示）的原版 NeedDef defName，用于自动扫描排重。</summary>
        private static readonly HashSet<string> KnownVanillaNeedDefs = new HashSet<string>
        {
            "Mood", "Rest", "Food", "Joy",
        };

        /// <summary>
        /// 适用于 pawn 的 StatCategoryDef 白名单。
        /// 1.6 事实：pawn 相关 stat 并非都在 Pawn* 前缀分类里——解析 Data/*/Defs/Stats 全量
        /// StatDef（含 ParentName 继承链）后可见：BasicsPawn（MoveSpeed/CarryingCapacity/CrawlSpeed…）、
        /// Mechanitor（机械师）、Mechanoid（EMPResistance 等）、Animals（Wildness/MinimumHandlingSkill）、
        /// Meditation（MeditationFocusStrength）等均为真实作用于 pawn 的字段。
        /// 旧的 StartsWith("Pawn") 前缀判定会漏掉上述全部（33 个），故改为显式白名单。
        /// </summary>
        private static readonly HashSet<string> PawnStatCategories = new HashSet<string>
        {
            // 主 Pawn 系列
            "PawnWork", "PawnCombat", "PawnHealth", "PawnSocial",
            "PawnResistances", "PawnFood", "PawnPsyfocus", "PawnMisc",
            // 非 Pawn 前缀但真实作用于 pawn
            "BasicsPawn", "BasicsPawnImportant",
            "Mechanitor", "Mechanoid", "Animals", "Meditation", "Genetics",
        };

        /// <summary>
        /// 分类不在白名单内、但确认作用于 pawn 的零散 StatDef（分类为 &lt;none&gt; / Basics 等）。
        /// </summary>
        private static readonly HashSet<string> ExtraPawnStatDefs = new HashSet<string>
        {
            "MaxInstallCount",      // Biotech：可植入体上限
            "BedHungerRateFactor",  // 卧床时饥饿速率（分类为 none）
            "ArmorRating_Heat",     // 护甲-高温（Sharp/Blunt 已硬编码，Heat 在 Apparel 分类）
            // 以下均为 Pawn* 文件中的真实 pawn stat，但 <category> 缺省（继承的 ParentName 也无 category），
            // IsPawnStat 仅靠 category 白名单会漏掉它们，导致原生属性面板有、本面板扫不到。补齐后一致。
            "ButcheryFleshSpeed", "ButcheryFleshEfficiency",              // 屠宰血肉 速度/效率
            "ButcheryMechanoidSpeed", "ButcheryMechanoidEfficiency",      // 屠宰机械族 速度/效率
            "ShootingAccuracyFactor_Long", "ShootingAccuracyFactor_Medium",
            "ShootingAccuracyFactor_Short", "ShootingAccuracyFactor_Touch", // 各射程射击精度因子
            "EMPResistance",                                         // 机械族 EMP 抗性
            // 注：ResearchSpeed 已在 KnownVanillaStatDefs 精选覆盖，无需此处重复。
        };

        /// <summary>判断 StatDef 是否适用于 pawn（仅注册 pawn 适用 stat，避免 Thing-only stat 在 pawn 上取数抛错）。</summary>
        private static bool IsPawnStat(StatDef s)
        {
            if (s == null) return false;
            if (!string.IsNullOrEmpty(s.defName) && ExtraPawnStatDefs.Contains(s.defName)) return true;
            return s.category != null && s.category.defName != null
                && PawnStatCategories.Contains(s.category.defName);
        }

        /// <summary>启动期调用一次：扫描全部已加载 mod 贡献的 StatDef / NeedDef，自动注册为字段。</summary>
        public static void ScanExternal()
        {
            try { ScanStatDefs(); } catch (Exception ex) { Log.Error("[PersonalChronicle] ScanStatDefs failed: " + ex); }
            try { ScanNeedDefs(); } catch (Exception ex) { Log.Error("[PersonalChronicle] ScanNeedDefs failed: " + ex); }
        }

        private static void ScanStatDefs()
        {
            foreach (var statDef in DefDatabase<StatDef>.AllDefs)
            {
                if (statDef == null) continue;
                string defName = statDef.defName;
                // 跳过本 mod 已硬编码精选覆盖的原版 StatDef（避免与精选卡重复）
                if (KnownVanillaStatDefs.Contains(defName)) continue;
                string key = "stat:" + defName;
                if (ByKey(key) != null) continue;

                // 仅注册"适用于 pawn"的原版/第三方 stat：Thing-only stat（市场价值、质量、原料护甲等）
                // 在 pawn 上取数会抛错或返回无意义值，必须排除。
                if (!IsPawnStat(statDef)) continue;

                string sourceId = ResolveSourceId(statDef.modContentPack);
                // 原版核心 → 归 SourceVanilla（"原版属性"组）；第三方 mod → 各自 PackageId 组。
                //
                // Label：传「实时解析器」而非快照字符串。
                // statDef.LabelCap 读取的是 Def.label，由原版 DefInjected 机制注入当前语言
                // （Languages/<lang>/DefInjected/StatDef/*.xml）——第三方 mod 的翻译由原版自动完成，
                // 无需本 mod 检测或介入。延迟到每次显示时求值，玩家切语言后即自动跟随。
                RegisterExternal(key, null, () => statDef.LabelCap, sourceId,
                    (p, o, v) =>
                    {
                        if (p == null) return null;
                        try { return (float?)p.GetStatValue(statDef); }
                        catch { return null; }   // 极少数 stat 在特定上下文取数异常，静默跳过
                    },
                    "", 0f, false, statDef);
            }
        }

        private static void ScanNeedDefs()
        {
            foreach (var needDef in DefDatabase<NeedDef>.AllDefs)
            {
                if (needDef == null) continue;
                string defName = needDef.defName;
                // 跳过本 mod 已硬编码精选覆盖的原版 NeedDef（避免与精选卡重复）
                if (KnownVanillaNeedDefs.Contains(defName)) continue;
                string key = "need:" + defName;
                if (ByKey(key) != null) continue;

                string sourceId = ResolveSourceId(needDef.modContentPack);
                // 原版核心需求（美观/舒适等）→ 归 SourceVanilla（"原版属性"组）。
                // Label 同样传实时解析器（同 ScanStatDefs：跟随 DefInjected 语言注入）。
                RegisterExternal(key, null, () => needDef.LabelCap, sourceId,
                    (p, o, v) => p?.needs?.TryGetNeed(needDef) != null
                        ? (float?)p.needs.TryGetNeed(needDef).CurLevelPercentage : null,
                    "%", 1f, false);
            }
        }

        /// <summary>由 Def 的 ContentPack 推断来源 Id：无 pack / 核心 → SourceVanilla；否则 PackageId。</summary>
        private static string ResolveSourceId(ModContentPack pack)
        {
            if (pack == null) return SourceVanilla;
            string pid = pack.PackageId;
            if (string.IsNullOrEmpty(pid)) return SourceVanilla;
            // 核心包（Ludeon.RimWorld / rimworld）视为原版
            if (pid == "Ludeon.RimWorld" || pid == "rimworld" || pid == "Ludeon.RimWorld.Royalty"
                || pid == "Ludeon.RimWorld.Ideology" || pid == "Ludeon.RimWorld.Biotech"
                || pid == "Ludeon.RimWorld.Anomaly") return SourceVanilla;
            return pid;
        }
    }
}
