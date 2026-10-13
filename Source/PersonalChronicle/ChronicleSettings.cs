using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain.Meal;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PersonalChronicle
{
    /// <summary>
    /// Mod settings. Persisted via ModSettings; values are read by the service
    /// layer (recording toggle, per-pawn event cap). All labels go through
    /// translation keys.
    /// </summary>
    public sealed class ChronicleSettings : ModSettings
    {
        public bool EnableRecording = true;
        public bool EnablePawnNeedSpeech = true;
        /// <summary>Intent only; verified institution capabilities control actual economic actions.
        /// CHG-20261010-ECON-DEFAULT-ON（D2=A，用户需求「经济系统默认启用」）：默认值由 false 改为 **true**。
        /// 这是**意图**（Intent only）而非能力授予 —— 制度进入 Active 仍须走既有的 Freeze → 旧权益承接流程，
        /// 能力只由真实恢复证明开放。B2.4.1 原契约「默认 false」由本次需求变更取代（契约测试同批更新，
        /// 见 <c>Tests/PawnInstitutionModeTests.cs</c>）；契约的另一半（Scribe 保真玩家显式选择）保持不变 ——
        /// 玩家显式关闭过经济（存档里存了 false）的档不会被默认值重新打开。</summary>
        public bool EnablePawnEconomicInstitution = true;
        public int MaxEventsPerPawn = 200;
        public bool DebugLiveCount = false;
        /// <summary>
        /// [PERF] 性能探针开关：打开后每 60 秒把关键入口的实测耗时打进 Player.log
        /// （次数/总毫秒/平均/最大单次），用于 before / after 性能对比。
        /// 默认关闭；关闭时开销为零（见 <see cref="PerfProbe"/>）。
        /// </summary>
        public bool EnablePerfProbe = false;

        /// <summary>
        /// [DEV] 进游戏（存档加载完成）后自动跑一次独立自检并弹窗报告。
        /// 实际生效还需 Prefs.DevMode（正常玩家永不看到），且每会话只弹一次。
        /// </summary>
        public bool EnableStartupSelfTest = true;
        /// <summary>Active UI theme id (see <see cref="UITheme"/> presets).</summary>
        public string ThemeId = UITheme.ThemeEpic;

        // ─────────────────────────────────────────────────────────────────────────
        // CHG-20261004-WORKTAB-ORIGINAL（2026-10-04 玩家决策：**删除自绘工作面板，只用原版**）
        //
        // 已删除（连同 EXC-2026-021 一起退役）：
        //   · Capture/Patch_MainTabWindowWorkTabTakeover.cs（接管 DoWindowContents + 两个 Extra*Space 归零）
        //   · Archive/UI/WorkTab/WorkTabTakeoverPanel.cs（自绘外壳）
        //   · Archive/UI/WorkTab/WorkTabTakeoverBridge.cs（WorkTabTableAccess / WorkTabPriorityWriter / WorkTabHideService）
        //   · 设置项 takeoverWorkTab / workTabShowSearch / workTabShowHideEye /
        //     workTabShowAutoAssign / workTabShowZebra（存档键一并停止读取，旧档残留键被忽略）
        //
        // 仍在生效的独立功能：隐藏名单过滤（Capture/Patch_MainTabWindowWorkPawns.cs）。
        // 它是**独立 patch**，不依赖接管面板；但接管面板的「◉ 眼标 / 显示全部」入口已随之删除，
        // 故本节提供**唯一恢复入口**：清空隐藏名单 ⇒ 工作面板完全等同原版。
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// CHG-20261004-SHIFTGATE · 离班自动取消班组工作类型（离班停工）。<b>默认开启</b>。
        ///
        /// <para><b>产品口径（2026-10-04 玩家决策）</b>：<b>岗位由玩家手动指派，
        /// 工作类型由系统按班次自动分配，离班即停工。</b>本项是「离班停工」的实现；
        /// 而 0/3 互跳的根因已由<see cref="MapComponent_WorkshopManager"/> 的
        /// <c>ShouldSkipAutoEnableForShift</c> 门控根除（离班者不再被面板刷新提权到 3/1），
        /// 因此本项与自动分配<b>不再争抢同一格位</b>，可安全保持默认开启。</para>
        ///
        /// <para><b>为什么保留开关</b>：个别玩家可能偏好「离班仍保留工作档位、不停工」。
        /// 关闭后离班者档位稳定停在上班时的值、不会被清 0（门控仍然生效，故不会互跳）。
        /// 存档 Scribe 键 <c>enableOffShiftCancel</c> 不变 ⇒ 不 bump SchemaVersion。</para>
        /// </summary>
        public bool EnableOffShiftCancel = true;

        // ---- CHG-20261004-WSSETTINGS · 车间策略（可选化项植入 Mod 设置）----
        // ⚠ 与 MealSchedule 同纪律：每项默认值必须与 Scribe 处的 defaultValue 逐字一致。
        //
        // ① 在班自动回写 —— 解决玩家实测痛点「接管/车间工作好像每天都要手动点一次」。
        //    根因（2026-10-04 取证）：OffShift（离班）在 MapComponentTick 里**每游戏小时**
        //    把车间类工作类型写 0，而"在班回写"此前只挂在面板 Refresh / 排班变更事件上
        //    ⇒ 班次一切换，档位停在 0（连值勤 Job 都排不出来），**直到玩家开一次面板**。
        //    开启后按同一小时节拍自动补齐（只补仍为 0 的，不覆盖玩家手调档位）。
        //    默认 **true**：这是修复"写 0 / 回写"不对称的缺陷，不是新增行为偏好。
        //
        // ⚠ CHG-20261005-SETTINGS（§7-1）：该项**已默认化** —— 作为"缺陷修复"不该给玩家关闭的入口。
        //   现状：① 设置页不再有这一行；② `ExposeData` **停止读取** `autoRestoreOnShift` 键
        //   （旧 Config 残留键被 Scribe 直接忽略，无需迁移）；③ 字段保留但恒为 true，
        //   唯一消费者 `MapComponent_WorkshopManager.WorkEnable.cs:455` 因此恒走"回写"分支。
        //   字段本身与 Legacy 绘制块一并列入下一版清理（见变更标注的残留段）。
        public bool AutoRestoreOnShift = true;

        // ② 冗余员工摸鱼切分（CHG-20261004-IDLE）：关闭后全员按原样写 Work 时段
        //    （= 退回摸鱼功能之前的行为），供"我就是要所有人站岗"的玩家使用。
        public bool IdleSplitEnabled = true;

        // ③ 未指定车间类型时的自动判定（设备风格占比 → 车间类型）。关闭后必须手动在
        //    「设置」页指定类型（= 逐车间靠 autoTypeSuppressed 抑制的全局版）。
        public bool AutoDetectWorkshopType = true;

        // ④ 跨天重放作息表（ReplayTimetablesOnDayChange）：保留"修历史残留"的兜底；
        //    关闭后 PC 只在排班/设备变更事件写作息表，绝不每天覆写在编成员的手排。
        public bool ReplayTimetablesOnDayChange = true;

        // ⑤ 新车间默认作息策略（Def 名）。空 = 跟随 Def 缺省（PC_WorkshopRestPolicy_Fixed）。
        //    ⚠ 默认值刻意与 WorkshopRestPolicyDef.DefaultPolicyDefName **完全相同**
        //    ⇒ 不写入、不指定任何车间时，行为与改动前**逐字一致**（零偏移）。
        //    作用：车间面板里逐车间选策略太碎，且默认档 FixedPerShift 之外的两档
        //    （NaturalRest = 完全不写作息表 / RotateDaily = 会翻转睡眠）各有明确取舍，
        //    玩家需要一个一次配置的入口。
        //    优先级：车间自身的选择（Zone_Workshop.RestPolicyDefName）> 本设置 > Def 缺省 > 硬兜底。
        public string WorkshopDefaultRestPolicy = "PC_WorkshopRestPolicy_Fixed";

        // ---- CHG-20261004-MEALSCHEDULE · 三餐作息 ----
        // ⚠ 每项的默认值必须与 MealScheduleDefaults 里的常量**完全一致**：
        //   Scribe_Values.Look 在存档缺该键时会用 defaultValue 覆盖字段初始值，
        //   两处不一致 = 「改了默认值却没生效」（EnableOffShiftCancel 已有同款教训）。

        // ⚠ CHG-20261005-MEALBYSHIFT（2026-10-05 玩家要求「删除全局三餐 mod 设置，统一口径以
        //   班次设置中的三餐时间执行」）：原先这里有 8 个全局三餐字段
        //   （MealScheduleEnabled / MealPriority / 三餐起止 ×3）—— **已全部删除**。
        //
        //   餐点现在的唯一来源是**班次配置**（WorkshopShift 的 6 个 Meal* 字段，随排班落盘），
        //   落地链：班次餐点 → WorkshopDutyCoordinator 涂进管制表（∩ 该班次工作时段）
        //   → MapComponent_MealSchedule 按管制表时段写「用餐」工作档位。
        //
        //   ⚠ **旧档兼容**：这些键在老档里仍存在，但读档时**无对应字段 ⇒ 被 Scribe 直接忽略**
        //   （RimWorld 按键查找，多余的键无副作用）⇒ 不需要 Migration，也不影响加载。

        // ---- CHG-20261005-DEVRENAME · 车间设备自定义名 ----
        /// <summary>
        /// 是否把车间自定义名应用到**原版 UI**（选中信息面板标题、堆叠名等一切经 Thing 标签的显示）。
        /// <b>默认开启</b> —— 这本身就是玩家要的功能，关掉只是给了「万一副作用不可接受时」的止血开关。
        ///
        /// <para><b>⚠ 作用域（P0-C，Gate 预审裁决）</b>：本开关<b>只</b>控制「车间名」这一段的替换；
        /// Archive 的档案覆盖（EXC-2026-001 #14）<b>不受本开关影响</b> —— 它是既有功能，若被一并关掉
        /// 等于回归（旧版 Patch_BuildingLabel 曾独立生效）。关闭后：原版回落显示档案覆盖名 / 默认名，
        /// PC 自绘界面<b>不受影响</b>（车间名照常显示）。</para>
        ///
        /// <para>存档键 <c>deviceRenameApplyToVanilla</c>；默认值须与 <c>ExposeData</c> 的
        /// defaultValue 逐字一致（工程既有教训：两处不一致 = 「改了默认值却没生效」）。</para>
        /// </summary>
        public bool DeviceRenameApplyToVanilla = true;

        // ---- v4.x 自动归档设置 ----
        /// <summary>是否启用已死亡低价值档案自动归档（30 天后销毁）。</summary>
        public bool AutoArchiveEnabled = true;

        /// <summary>CHG-20261012-WORKGATE：**工作合同闸门**总开关（默认开）。
        /// 开 = 命令/订单/自发三类劳动都必须有授权（合同 / 车间班次 / 白名单），面板勾选改为"可被雇佣"语义；
        /// 关 = 完全原版行为（唯一回滚路径）。</summary>
        public bool EnableWorkContractGate = true;

        /// <summary>生存白名单（工种 defName 列表；默认 = 灭火 + 医疗）。**逐项可视化开关**在设置页本分节内。</summary>
        public List<string> WorkGateWhitelist = WorkAuthority.Defaults();
        /// <summary>自动归档银币价值阈值（HealthValuation 银币价值低于此值的死亡档案将被标记归档）。</summary>
        public float AutoArchiveSilverThreshold = 200f;
        /// <summary>归档保留天数（到期后 PendingPurge → 下次 purge 真正删除）。默认 30 天。</summary>
        public int AutoArchivePurgeDays = 30;

        // ---- 总览数据面板：玩家自定义字段 ----
        /// <summary>
        /// 玩家选择显示的属性字段 key 集合（见 OverviewFieldRegistry）。
        /// 为空或 null = 使用注册表默认核心字段（IsDefault=true）。
        /// </summary>
        public HashSet<string> OverviewPanelFields = null;

        private const float EventsPerPawnSliderMin = 10f;
        private const float EventsPerPawnSliderMax = 1000f;
        private const float SilverThresholdMin = 0f;
        private const float SilverThresholdMax = 5000f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableRecording, "enableRecording", true);
            Scribe_Values.Look(ref EnablePawnNeedSpeech, "enablePawnNeedSpeech", true);
            // CHG-20261010-ECON-DEFAULT-ON：读档默认值必须与字段初值逐字一致（true）—— 否则"设置页显示的
            // 值"与"读档后的实际值"会在旧档上分叉（同 DeviceRenameApplyToVanilla 的既有纪律）。
            // 旧档若显式存过 false（玩家主动关闭）则尊重其选择：Scribe 只在该键缺失时使用 defaultValue。
            Scribe_Values.Look(ref EnablePawnEconomicInstitution, "enablePawnEconomicInstitution", true);
            Scribe_Values.Look(ref MaxEventsPerPawn, "maxEventsPerPawn", 200);
            // CHG-20261005-DEVRENAME：默认值必须与字段初始值逐字一致（true）。
            Scribe_Values.Look(ref DeviceRenameApplyToVanilla, "deviceRenameApplyToVanilla", true);
            // CHG-20261012-WORKGATE：闸门开关与白名单（旧档无该键 ⇒ 默认开 + 默认白名单，行为与"新装上"一致）。
            Scribe_Values.Look(ref EnableWorkContractGate, "enableWorkContractGate", true);
            Scribe_Collections.Look(ref WorkGateWhitelist, "workGateWhitelist", LookMode.Value);
            if (WorkGateWhitelist == null) WorkGateWhitelist = WorkAuthority.Defaults();
            // ⚠ CHG-20261005-SETTINGS（§7-4 准入护栏）：设置页滑块下限已由 10 提到 72
            //   （<24 会触发 routine 预算反转；<72 会让"routine 占 1/3"的声明失真）。
            //   旧配置可能仍带 10–71 的旧值，且滑块无法表达该区间 ⇒ 在**读档单点**夹紧，
            //   保证「设置页显示值 = 实际生效值」，避免"打开设置页才被隐式改掉"的诡异行为。
            if (MaxEventsPerPawn < 72)
            {
                MaxEventsPerPawn = 72;
            }
            Scribe_Values.Look(ref DebugLiveCount, "debugLiveCount", false);
        Scribe_Values.Look(ref EnablePerfProbe, "enablePerfProbe", false);
            Scribe_Values.Look(ref EnableStartupSelfTest, "enableStartupSelfTest", true);
            Scribe_Values.Look(ref ThemeId, "themeId", UITheme.ThemeEpic);
            // CHG-20261004-WORKTAB-ORIGINAL：自绘工作面板已删除 ⇒ takeoverWorkTab 与四个
            // workTabShow* 键**停止读取**（旧档残留键直接忽略，不做迁移：它们只影响已删除的 UI）。
            // CHG-20261004-MANUALPOST：默认值必须与字段声明处一致（都 true）。
        // 若两处不一致，存档里无此键时会用 Scribe 的 defaultValue 覆盖字段初始值，
        // 表现为「改了默认却没生效」—— 这是本项最易踩的坑。
        // ⚠ CHG-20261005-SETTINGS-REMOVE2：`enableOffShiftCancel` **停止读取**（该项已默认化恒开）。
        //   旧 Config 里的残留键不再被 Scribe 查找 ⇒ 零副作用、无需 Migration（沿 MEALBYSHIFT 先例）。
        //   授权面变化已登记 EXC-2026-026。
        //   原行：Scribe_Values.Look(ref EnableOffShiftCancel, "enableOffShiftCancel", true);
        // CHG-20261004-WSSETTINGS · 车间策略（默认值与字段声明处逐项一致；旧档缺键 ⇒ 取默认）。
        // ⚠ CHG-20261005-SETTINGS（§7-1）：`autoRestoreOnShift` **停止读取**（该项已默认化恒开）。
        //   旧 Config 里的残留键不再被 Scribe 查找 ⇒ 零副作用、无需 Migration（沿 MEALBYSHIFT 先例）。
        //   原行：Scribe_Values.Look(ref AutoRestoreOnShift, "autoRestoreOnShift", true);
        Scribe_Values.Look(ref IdleSplitEnabled, "idleSplitEnabled", true);
        Scribe_Values.Look(ref AutoDetectWorkshopType, "autoDetectWorkshopType", true);
        Scribe_Values.Look(ref ReplayTimetablesOnDayChange, "replayTimetablesOnDayChange", true);
        // 默认值与字段声明处一致；旧档缺键 ⇒ 取 FixedPerShift（= Def 缺省，零行为变化）。
        Scribe_Values.Look(ref WorkshopDefaultRestPolicy, "workshopDefaultRestPolicy",
            "PC_WorkshopRestPolicy_Fixed");
        // CHG-20261005-MEALBYSHIFT：原先这里落盘 8 个全局三餐键（mealScheduleEnabled /
        // mealPriority / 三餐起止 ×3）—— **已随字段一起删除**，连同上一批的"旧默认值指纹式迁移"
        // （它服务的对象已不存在，留着反而会读到无意义的默认值）。
        // 旧档里这些键被 Scribe 忽略（键查找语义），无需 Migration —— 见字段区注释。
            // v4.x 自动归档
            Scribe_Values.Look(ref AutoArchiveEnabled, "autoArchiveEnabled", true);
            Scribe_Values.Look(ref AutoArchiveSilverThreshold, "autoArchiveSilverThreshold", 200f);
            Scribe_Values.Look(ref AutoArchivePurgeDays, "autoArchivePurgeDays", 30);
            // 总览字段自定义
            Scribe_Collections.Look(ref OverviewPanelFields, "overviewPanelFields", LookMode.Value);
        }

        /// <summary>
        /// 设置页入口。
        ///
        /// <para><b>CHG-20261005-SETTINGS-UI</b>（2026-10-04 玩家反馈「太乱了，没有模块分组，
        /// 没有信息简化。没有声明来源、用途等等，太粗糙了。且可以基于 RUIF 框架」）：
        /// 设置面重做为 RUIF 分页式 —— 4 页签（档案 / 车间 / 界面；「诊断」仅开发者模式可见）
        /// × 分节卡 × 行（标题 + 来源角标 + 控件 + 用途）× 可展开详情（默认值理由 / 影响代价）。</para>
        ///
        /// <para><b>本节制只换界面，不动数据</b>：字段、默认值、<c>ExposeData</c> 的键名一律未改
        /// ⇒ 旧档兼容、无需 Migration（见变更标注 CHG-20261005-SETTINGS-UI 的兼容性表）。</para>
        /// </summary>
        public void DoSettingsWindowContents(Rect inRect)
        {
            // 旧 Listing_Standard 实现整块退役（原实现见本文件 git 历史 / CHG-20261005 标注）。
            Settings.SettingsDrawer.Paint(inRect, this);
        }

        // ⚠ CHG-20261005-SETTINGS-UI：以下 Legacy* / Draw* 旧实现**已不参与绘制**，作为
        //    「一键回滚对照」保留一轮（万一新 UI 有观感/交互问题，把上面一行转发改回
        //    LegacySettingsContents(inRect) 即可复原）。已列入变更标注的 Remaining：
        //    下一版随该标注一并删除（它同时是 SettingsDrawer 的等价性参照）。
        private void LegacySettingsContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("PersonalChronicle.Settings.EnableRecording".Translate().ToString(), ref EnableRecording);
            listing.CheckboxLabeled("PersonalChronicle.Settings.DebugLiveCount".Translate().ToString(), ref DebugLiveCount);
            listing.CheckboxLabeled("PersonalChronicle.Settings.EnablePerfProbe".Translate().ToString(), ref EnablePerfProbe);
            // CHG-20261004-WORKTAB-ORIGINAL：自绘工作面板已删除 ⇒【工作】标签完全使用原版。
            // 这里只保留隐藏名单的恢复入口（隐藏功能本身仍生效，入口在 Pawn 检查页的 gizmo）。
            DrawWorkTabOriginalSection(listing);
            // CHG-20261012-WORKGATE：工作合同闸门（总开关 + 白名单可视化列表）。
            DrawWorkGateSection(listing);
            // CHG-20261004-SHIFTGATE：默认 true（离班停工）。关闭后离班者保留工作档位、不会被清 0。
            listing.CheckboxLabeled("PersonalChronicle.Settings.EnableOffShiftCancel".Translate().ToString(), ref EnableOffShiftCancel);

            // CHG-20261004-WSSETTINGS · 车间策略分组。
            // 为什么集中在此：这些项原先要么是"每次进面板手点一次"、要么是硬编码策略，
            // 玩家无法一次配置；提升为 Mod 设置后仍然**默认保持既有行为**（零行为变化）。
            listing.Gap(12f);
            listing.Label("PersonalChronicle.Settings.WorkshopSection".Translate().ToString(), -1f);
            listing.CheckboxLabeled(
                "PersonalChronicle.Settings.AutoRestoreOnShift".Translate().ToString(),
                ref AutoRestoreOnShift);
            listing.Label("PersonalChronicle.Settings.AutoRestoreOnShiftDesc".Translate().ToString(), -1f);
            listing.CheckboxLabeled(
                "PersonalChronicle.Settings.IdleSplitEnabled".Translate().ToString(),
                ref IdleSplitEnabled);
            listing.CheckboxLabeled(
                "PersonalChronicle.Settings.AutoDetectWorkshopType".Translate().ToString(),
                ref AutoDetectWorkshopType);
            listing.CheckboxLabeled(
                "PersonalChronicle.Settings.ReplayTimetablesOnDayChange".Translate().ToString(),
                ref ReplayTimetablesOnDayChange);

            // ⑤ 新车间默认作息策略（三选一，复用车间面板既有的 Label 键 —— Keyed 是全局的，
            //    键名不绑定用途，直接复用可保证两处口径与文案**永不漂移**）。
            //    Def 名字面量对应 Defs/WorkshopRestPolicies.xml；缺失时 PolicyOf 会逐级降级。
            listing.Gap(6f);
            listing.Label("PersonalChronicle.Automation.Tab.RestPolicy".Translate().ToString()
                + " — " + "PersonalChronicle.Settings.WorkshopRestPolicyDesc".Translate().ToString(), -1f);
            if (listing.RadioButton(
                    "PersonalChronicle.Automation.RestPolicy.Fixed.Label".Translate().ToString(),
                    WorkshopDefaultRestPolicy == "PC_WorkshopRestPolicy_Fixed"))
            {
                WorkshopDefaultRestPolicy = "PC_WorkshopRestPolicy_Fixed";
            }
            if (listing.RadioButton(
                    "PersonalChronicle.Automation.RestPolicy.Natural.Label".Translate().ToString(),
                    WorkshopDefaultRestPolicy == "PC_WorkshopRestPolicy_Natural"))
            {
                WorkshopDefaultRestPolicy = "PC_WorkshopRestPolicy_Natural";
            }
            if (listing.RadioButton(
                    "PersonalChronicle.Automation.RestPolicy.Rotate.Label".Translate().ToString(),
                    WorkshopDefaultRestPolicy == "PC_WorkshopRestPolicy_Rotate"))
            {
                WorkshopDefaultRestPolicy = "PC_WorkshopRestPolicy_Rotate";
            }
            if (WorkshopDefaultRestPolicy == "PC_WorkshopRestPolicy_Rotate")
            {
                // 与车间面板同一句警告：RotateDaily 会按天翻转睡眠节律（既有披露要求）。
                listing.Label("PersonalChronicle.Automation.RestPolicy.Rotate.Warning".Translate().ToString(), -1f);
            }
            listing.Label("PersonalChronicle.Settings.MaxEventsPerPawn".Translate().ToString() + ": " + MaxEventsPerPawn, -1f);
            MaxEventsPerPawn = (int)listing.Slider((float)MaxEventsPerPawn, EventsPerPawnSliderMin, EventsPerPawnSliderMax);

            // v1.1.4: UI theme selector (mirrors the in-window header button).
            listing.Gap(8f);
            listing.Label("PersonalChronicle.Settings.Theme".Translate().ToString(), -1f);
            int themeIdx = System.Array.IndexOf(UITheme.ThemeIds, ThemeId);
            if (themeIdx < 0) themeIdx = 0;
            for (int i = 0; i < UITheme.ThemeIds.Length; i++)
            {
                string label = UITheme.GetDisplayName(UITheme.ThemeIds[i]);
                bool chosen = listing.RadioButton(label, i == themeIdx);
                if (chosen && i != themeIdx)
                {
                    ThemeId = UITheme.ThemeIds[i];
                    UITheme.Apply(ThemeId);
                }
            }

            // v4.x 自动归档设置
            listing.Gap(12f);
            listing.Label("PersonalChronicle.Settings.AutoArchiveSection".Translate().ToString(), -1f);
            listing.CheckboxLabeled("PersonalChronicle.Settings.AutoArchiveEnabled".Translate().ToString(), ref AutoArchiveEnabled);
            if (AutoArchiveEnabled)
            {
                listing.Label("PersonalChronicle.Settings.AutoArchiveSilverThreshold".Translate().ToString()
                    + ": " + AutoArchiveSilverThreshold.ToString("F0"), -1f);
                AutoArchiveSilverThreshold = listing.Slider(AutoArchiveSilverThreshold, SilverThresholdMin, SilverThresholdMax);
                listing.Label("PersonalChronicle.Settings.AutoArchivePurgeDays".Translate().ToString()
                    + ": " + AutoArchivePurgeDays, -1f);
                AutoArchivePurgeDays = Mathf.RoundToInt(listing.Slider((float)AutoArchivePurgeDays, 7f, 365f));
            }

            // CHG-20261005-MEALBYSHIFT：本段已由"全局三餐设置"改为**纯说明**
            //（餐点唯一来源是班次配置 ⇒ 见方法注释）。
            DrawMealScheduleSection(listing);

            listing.End();
        }

        /// <summary>
        /// CHG-20261004-WORKTAB-ORIGINAL · 「恢复原版工作面板」区块。
        ///
        /// <para>【工作】标签的接管绘制已整块删除（面板 + 桥接 + Prefix + 尺寸归零 patch），
        /// 现在完全是原版界面。此处保留的唯一相关功能是<b>隐藏名单</b>的恢复入口：
        /// 隐藏本身仍生效（入口在 Pawn 检查页的 gizmo「从工作面板隐藏」），
        /// 但面板内那颗「显示全部已隐藏」按钮已随面板删除 ⇒ 需要一个落盘的恢复途径，
        /// 否则玩家隐藏过谁就只能改存档才能看回来。</para>
        /// </summary>
        /// <summary>CHG-20261012-WORKGATE：工作合同闸门分节 —— 总开关 + **白名单可视化列表**
        ///（逐工种一行：名称 · 当前状态 · 开关按钮；订单类由 Def 数据标记，不靠人工清单）。
        /// <para>口径说明：本页**只**显示"白名单 / 合同门控"两态（车间班次托管是**地图级**读数，
        /// 需要地图与车间管理者，不在此处显示 —— 避免 RootSettings→Automation 的新依赖边）；
        /// 需要完整来源（含车间托管）时用 <c>WorkAuthorityService.SourceFor</c>（Economy 层）。</para></summary>
        private void DrawWorkGateSection(Listing_Standard listing)
        {
            listing.Gap(12f);
            listing.Label("PersonalChronicle.Settings.WorkGate.Section".Translate().ToString(), -1f);
            listing.CheckboxLabeled("PersonalChronicle.Settings.WorkGate.Enable".Translate().ToString(), ref EnableWorkContractGate);
            listing.Label("PersonalChronicle.Settings.WorkGate.EnableDesc".Translate().ToString(), -1f);
            listing.Gap(4f);

            if (WorkGateWhitelist == null) WorkGateWhitelist = WorkAuthority.Defaults();
            // ⚠ 不用带参 Translate（1.6 的 params object[] 重载已 Obsolete，工程内由离线测试禁止）——
            // 计数直接拼在标签后（纯数字，无需本地化）。
            listing.Label("PersonalChronicle.Settings.WorkGate.WhitelistHint".Translate().ToString()
                + "  " + WorkGateWhitelist.Count.ToString(), -1f);
            if (listing.ButtonText("PersonalChronicle.Settings.WorkGate.ResetDefault".Translate().ToString()))
                WorkGateWhitelist = WorkAuthority.Defaults();
            if (listing.ButtonText("PersonalChronicle.Settings.WorkGate.AllGated".Translate().ToString()))
                WorkGateWhitelist = new List<string>();
            if (listing.ButtonText("PersonalChronicle.Settings.WorkGate.AllAllowed".Translate().ToString()))
                WorkGateWhitelist = WorkAuthority.Normalize(AllWorkTypeDefNames());
            listing.Gap(4f);

            List<WorkTypeDef> all = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                WorkTypeDef workType = all[i];
                if (workType == null || string.IsNullOrEmpty(workType.defName)) continue;
                bool whitelisted = WorkAuthority.IsWhitelisted(workType.defName, WorkGateWhitelist);
                bool next = whitelisted;
                listing.CheckboxLabeled(workType.LabelCap + "  ·  " + WorkGateStatusLine(workType, whitelisted), ref next);
                if (next == whitelisted) continue;
                var updated = new List<string>(WorkGateWhitelist);
                if (next) updated.Add(workType.defName);
                else updated.RemoveAll(name => name == workType.defName);
                WorkGateWhitelist = WorkAuthority.Normalize(updated);
                WorkAuthorityInvalidator.InvalidateAll();   // 改白名单 ⇒ 立即让作业表重建
            }
            listing.Gap(6f);
        }

        /// <summary>工作类型 → 逐项状态文本（白名单 / 合同门控 + 订单类标记）。</summary>
        private static string WorkGateStatusLine(WorkTypeDef workType, bool whitelisted)
        {
            string state = whitelisted
                ? "PersonalChronicle.Settings.WorkGate.State.Whitelist".Translate().ToString()
                : "PersonalChronicle.Settings.WorkGate.State.ContractOnly".Translate().ToString();
            if (IsBillDriven(workType)) state += " · " + "PersonalChronicle.Settings.WorkGate.Tag.Bill".Translate().ToString();
            return state;
        }

        /// <summary>订单类判定（Def 数据，非人工清单）：该工种的任一 giver 是 <see cref="WorkGiver_DoBill"/>。</summary>
        private static bool IsBillDriven(WorkTypeDef workType)
        {
            List<WorkGiverDef> givers = workType.workGiversByPriority;
            for (int i = 0; givers != null && i < givers.Count; i++)
            {
                WorkGiverDef def = givers[i];
                if (def != null && def.giverClass == typeof(WorkGiver_DoBill)) return true;
            }
            return false;
        }

        /// <summary>全部工种 defName（"全部放行"快捷动作用；只取稳定 ID，不做任何本地化判断）。</summary>
        private static List<string> AllWorkTypeDefNames()
        {
            var names = new List<string>();
            List<WorkTypeDef> all = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && !string.IsNullOrEmpty(all[i].defName)) names.Add(all[i].defName);
            return names;
        }

        private static void DrawWorkTabOriginalSection(Listing_Standard listing)
        {
            listing.Gap(4f);
            listing.Label("PersonalChronicle.WorkTab.Settings.OriginalPanel".Translate().ToString(), -1f);
            Data.ChronicleGameComponent component = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;
            if (component == null)
            {
                return;   // 标题栏阶段（无存档）没有名单可清
            }
            // ⚠ NODEMOTE-AUDIT：并集去重计数（与 SettingsDrawer 同一口径；原只数手动集合，
            // 分账后仅自动隐藏时恒为 0 ⇒ 恢复入口不显示）。
            int hiddenCount = component.HiddenWorkTabCount;
            if (hiddenCount <= 0)
            {
                return;
            }
            listing.Label(string.Format(
                "PersonalChronicle.WorkTab.Settings.HiddenCount".Translate().ToString(), hiddenCount), -1f);
            // ⚠ 1.6 签名（Cecil 核实）：Listing_Standard 无 Button(...)；按钮是
            //   ButtonText(string label, string highlightTag, float widthPct)；
            //   消息是 new Message(text, MessageTypeDef)（Messages.Message(string) 已不存在）。
            if (listing.ButtonText("PersonalChronicle.WorkTab.Settings.ClearHidden".Translate().ToString(), null, 1f))
            {
                int removed = component.ClearHiddenFromWorkTab();
                Find.WindowStack.Add(new Dialog_MessageBox(string.Format(
                    "PersonalChronicle.WorkTab.Settings.HiddenCleared".Translate().ToString(), removed)));
            }
        }

        /// <summary>
        /// CHG-20261005-MEALBYSHIFT · 「作息与用餐」段落。
        ///
        /// <para><b>旧版是全局三餐设置</b>（总开关 / 档位 / 三餐起止时间），
        /// 玩家要求「删除全局三餐 mod 设置，统一口径以班次设置中的三餐时间执行」
        /// ⇒ 三组控件与 8 个设置字段全部移除。</para>
        ///
        /// <para>本段保留为**纯说明**：让打开这一页的玩家知道"东西去哪了"，
        /// 而不是面对一页空白以为功能被删掉（GATE-UI 状态完备性 / UI-009 可自证）。</para>
        /// </summary>
        private void DrawMealScheduleSection(Listing_Standard listing)
        {
            listing.Gap(12f);
            listing.Label("PersonalChronicle.Settings.MealSection".Translate().ToString(), -1f);
            listing.Label("PersonalChronicle.Settings.MealMovedToShift".Translate().ToString(), -1f);
        }

        // ⚠ CHG-20261005-MEALBYSHIFT：原 `DrawMealWindowRow`（三餐起止滑条）已删除 ——
        //   它唯一的用途是编辑全局三餐窗口，而该窗口已不存在。
    }
}
