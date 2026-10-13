using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// **游戏内经济诊断窗**（2026-10-10，实测反馈「无法自主」后新增）。
    ///
    /// <para><b>为什么需要它</b>：玩家在决策页看到「拟选行动 = 等待新需求事实 / 原因 = 事实缺失」，
    /// 但**看不出缺的是哪一层的事实** —— 只能去翻 <c>Player.log</c>，而日志往往已被新会话覆盖。
    /// 本窗把定位所需的内部读数直接上屏：感知捕获健康度（<b>最老到期未捕获</b>是"感知是否卡住"的唯一直接指标）、
    /// 制度门与阻塞项、每名殖民者的规划判决与年龄、以及关键对象计数。</para>
    ///
    /// <para><b>只读纪律</b>：本类不写任何经济状态、不改感知会话、不触发规划、不影响 Tick。
    /// 显示内容是**技术读数**（稳定 ID / 枚举名 / 计数），刻意不经本地化管道 —— 它们是诊断标识符；
    /// 但**分区标题与全部提示文本走 Keyed**（P2-2，2026-10-10：此前 12 处中文为硬编码，GOV-010 缺口）。</para>
    ///
    /// <para><b>P2-2 绘制纪律</b>（2026-10-10）：① 内容改由 RUIF 绘制（卡框 / 按钮原子 / 细滚动条）；
    /// ② **绘制期零测量** —— 段落高度只在快照重建或可用宽变化时重算（此前是每帧 <c>Text.CalcHeight</c>）；
    /// ③ 滚动入栈/出栈 <c>try/finally</c> 成对，异常只记一次日志（组件绘制规范铁律 3）。</para>
    /// </summary>
    public sealed class Window_PawnEconomyDiagnostics : Window
    {
        private const int RefreshTicks = 30;
        private const int MaximumPawns = 4;
        /// <summary>可用宽的浮点容差：小于它不触发重测（避免亚像素抖动导致每帧测量）。</summary>
        private const float MeasuredWidthEpsilon = 0.5f;
        private readonly int mapId;
        private Vector2 scroll;
        private string cached;
        private int cachedTick = -99999;
        /// <summary>快照文本 + 其段落高度：只在快照重建 / 可用宽变化 / 语言切换时重算。</summary>
        private float cachedTextHeight;
        private float cachedMeasuredWidth = -1f;
        /// <summary>语言相关的文案与按钮宽（随语言切换失效，不每帧 <c>Translate</c>/<c>Measure</c>）。</summary>
        private object cachedLanguage;
        private string titleText, writeButtonText, writtenText;
        private float writeButtonWidth = -1f;

        public Window_PawnEconomyDiagnostics(int mapId)
        {
            this.mapId = mapId;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnCancel = true;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(760f, 580f);

        /// <summary>诊断文案的**整键回落**入口：缺键显示标识本身，绝不把 <c>PC_PEA.Diag.*</c> 裸键打上屏（P0-4 纪律）。</summary>
        private static string D(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Diag." + suffix, suffix);

        private void EnsureTexts()
        {
            if (ReferenceEquals(cachedLanguage, LanguageDatabase.activeLanguage)) return;
            cachedLanguage = LanguageDatabase.activeLanguage;
            titleText = D("Title");
            writeButtonText = D("WriteLog");
            writtenText = D("Written");
            writeButtonWidth = Mathf.Max(
                RUIF.Measure.Width(writeButtonText, GameFont.Small) + RUIF.Tokens.SpaceMd * 2f,
                RUIF.Tokens.SpaceLg * 4f);
            cachedMeasuredWidth = -1f;   // 语言变了 ⇒ 段落高度必须重测
        }

        public override void DoWindowContents(Rect inRect)
        {
            var runtime = GameComponent_PawnEconomy.Current;
            EnsureTexts();
            float controlHeight = RUIF.Measure.ControlHeight(GameFont.Small);
            var header = new Rect(inRect.x, inRect.y, inRect.width, controlHeight);
            var writeButton = new Rect(header.xMax - writeButtonWidth, header.y, writeButtonWidth, controlHeight);
            RUIF.Draw.Label(
                new Rect(header.x, header.y, Mathf.Max(0f, header.width - writeButtonWidth - RUIF.Tokens.SpaceSm), controlHeight),
                titleText, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            if (RUIF.ButtonAtoms.PrimaryButton(writeButton, writeButtonText))
            {
                Log.Message("[PC][Diag]\n" + Snapshot(runtime, true));
                Messages.Message(writtenText, MessageTypeDefOf.TaskCompletion, false);
            }

            float bodyTop = header.yMax + RUIF.Tokens.SpaceSm;
            var card = new Rect(inRect.x, bodyTop, inRect.width, Mathf.Max(1f, inRect.yMax - bodyTop));
            // 层次 2：卡片承载读数（层次 1 = 窗口底，层次 3 = 卡内文本/滚动条）。
            var inner = EconomyUiKit.CardFrame(card, false, null);
            string text = Snapshot(runtime, false);
            float viewWidth = Mathf.Max(1f, inner.width - RUIF.Tokens.ScrollbarThickness);
            float textHeight = MeasuredTextHeight(text, viewWidth);
            if (inner.height < 1f) return;

            RUIF.Layout.BeginScroll(inner, textHeight, ref scroll, false);
            try
            {
                // 读数段是**多行段落**：RUIF.Draw.Label 是单行语义（内部把 WordWrap 置 false），
                // 段落必须自持换行作用域 + 逐项还原全局字体态（否则泄漏到后续原生 UI）。
                bool oldWrap = Text.WordWrap;
                Text.WordWrap = true;
                try
                {
                    using (RUIF.Draw.Font(GameFont.Tiny))
                    using (RUIF.Draw.Anchor(TextAnchor.UpperLeft))
                    using (RUIF.Draw.Color(RUIF.Tokens.Muted))
                    {
                        Widgets.Label(new Rect(0f, 0f, viewWidth, Mathf.Max(textHeight, inner.height)), text);
                    }
                }
                finally
                {
                    Text.WordWrap = oldWrap;
                }
            }
            catch (System.Exception error)
            {
                // 异常隔离：诊断窗是排查工具，绝不能自己成为 GUI 卡死的源头。
                Log.ErrorOnce("[PC][Diag] paint failed: " + error, 0x1EC0DE6);
            }
            finally
            {
                RUIF.Layout.EndScroll();
            }
            RUIF.MiscAtoms.ThinScrollBar(inner, textHeight, scroll.y);
        }

        /// <summary>段落高度（**绘制期零测量**的唯一实现）：宽度未变则复用上一次结果。</summary>
        private float MeasuredTextHeight(string text, float width)
        {
            if (cachedMeasuredWidth >= 0f && Mathf.Abs(cachedMeasuredWidth - width) < MeasuredWidthEpsilon)
            {
                return cachedTextHeight;
            }
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            try
            {
                using (RUIF.Draw.Font(GameFont.Tiny))
                {
                    cachedTextHeight = Mathf.Max(RUIF.Measure.RowHeight(GameFont.Tiny),
                        Text.CalcHeight(text ?? string.Empty, width));
                }
            }
            finally
            {
                Text.WordWrap = oldWrap;
            }
            cachedMeasuredWidth = width;
            return cachedTextHeight;
        }

        /// <summary>组装诊断文本（每 <see cref="RefreshTicks"/> 重建一次，避免每帧拼字符串）。
        /// 分区标题与提示走 Keyed；技术读数（稳定 ID / 枚举名 / 计数）保持英文原样。</summary>
        private string Snapshot(GameComponent_PawnEconomy runtime, bool force)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!force && cached != null && now - cachedTick < RefreshTicks) return cached;
            cachedTick = now;
            var sb = new StringBuilder();
            sb.Append("tick=").Append(now).Append("   map=").Append(mapId).AppendLine();
            if (runtime == null) { sb.AppendLine(D("RuntimeNull")); return cached = sb.ToString(); }

            AppendPerception(sb, runtime);
            AppendInstitution(sb, runtime);
            AppendPawns(sb, runtime, now);
            // 文本变了 ⇒ 段落高度必须重测（宽度不变也不能复用旧高）。
            cachedMeasuredWidth = -1f;
            return cached = sb.ToString();
        }

        private static void Section(StringBuilder sb, string keySuffix)
            => sb.Append("── ").Append(D(keySuffix)).AppendLine(" ──");

        private static void AppendPerception(StringBuilder sb, GameComponent_PawnEconomy runtime)
        {
            Section(sb, "Section.Perception");
            var d = runtime.PerceptionDiagnostics;
            if (d == null)
            {
                // 感知未初始化 / 初始化失败 —— 这正是"事实永远缺失"的一类根因。
                sb.Append("  unavailable: ").AppendLine(D("PerceptionUnavailable"));
                return;
            }
            sb.Append("  subjects: active=").Append(d.ActiveSubjects).Append(" retired=").Append(d.RetiredSubjects)
              .Append(" omitted=").Append(d.OmittedSubjects).AppendLine();
            sb.Append("  captures=").Append(d.Captures).Append("  errors=").Append(d.CaptureErrors)
              .Append("  budgetExceededTicks=").Append(d.BudgetExceededTicks).AppendLine();
            sb.Append("  lastCaptureMs=").Append(d.LastCaptureMilliseconds.ToString("F2"))
              .Append("  lastPopulationMs=").Append(d.LastPopulationMilliseconds.ToString("F2")).AppendLine();
            sb.Append("  ").Append(D("Population")).Append(": tick=").Append(d.PopulationTick)
              .Append("  ").Append(D("PopulationComplete")).Append('=').Append(d.PopulationComplete).AppendLine();
            // 唯一直接指标：最老的"已到期但未被捕获"的年龄。持续性 > 300 即解释 WaitingFacts。
            sb.Append("  ★ oldestDueAgeTicks=").Append(d.OldestDueAgeTicks)
              .Append(d.OldestDueAgeTicks > 300 ? "  ← " + D("OldestDueNote") : string.Empty)
              .AppendLine();
        }

        private static void AppendInstitution(StringBuilder sb, GameComponent_PawnEconomy runtime)
        {
            Section(sb, "Section.Institution");
            var mode = runtime.QueryInstitutionMode();
            if (mode == null) { sb.AppendLine("  snapshot = null"); return; }
            sb.Append("  phase=").Append(mode.Phase).Append("  desired=").Append(mode.Desired)
              .Append("  revision=").Append(mode.Revision).AppendLine();
            sb.Append("  newCommitmentsAllowed=").Append(mode.NewCommitmentsAllowed)
              .Append("  cutoverFrozen=").Append(mode.CutoverFrozen)
              .Append("  outstandingRights=").Append(mode.HasOutstandingRights).AppendLine();
            if (!string.IsNullOrEmpty(mode.Fault)) sb.Append("  fault=").Append(mode.Fault).AppendLine();
            var blockers = mode.Blockers;
            sb.Append("  blockers=").Append(blockers == null || blockers.Count == 0 ? D("None") : string.Join(", ", blockers)).AppendLine();
        }

        private void AppendPawns(StringBuilder sb, GameComponent_PawnEconomy runtime, int now)
        {
            Section(sb, "Section.PawnPlans");
            Map map = null;
            var maps = Find.Maps;
            if (maps != null)
                for (int i = 0; i < maps.Count; i++) if (maps[i] != null && maps[i].uniqueID == mapId) { map = maps[i]; break; }
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) { sb.Append("  ").AppendLine(D("NoColonists")); return; }
            for (int i = 0; i < colonists.Count && i < MaximumPawns; i++)
            {
                var pawn = colonists[i];
                if (pawn == null) continue;
                string key = pawn.GetUniqueLoadID();
                sb.Append("  ").Append(pawn.LabelShortCap).Append(" (").Append(key).AppendLine(")");
                var plan = runtime.QueryShadowPlan(key, map);
                if (plan == null) { sb.Append("    plan = null (").Append(D("PlanMissing")).AppendLine(")"); continue; }
                sb.Append("    decision=").Append(plan.Decision).Append("  reason=").Append(plan.Reason ?? "-").AppendLine();
                sb.Append("    builtTick=").Append(plan.BuiltTick).Append("  age=").Append(now - plan.BuiltTick).AppendLine();
                int opportunities = plan.Opportunities == null ? 0 : plan.Opportunities.Count;
                sb.Append("    opportunities=").Append(opportunities)
                  .Append("  selected=").Append(string.IsNullOrEmpty(plan.SelectedOpportunity) ? "-" : plan.SelectedOpportunity).AppendLine();
                sb.Append("    employmentApplicable=").Append(plan.EmploymentApplicable).AppendLine();
                // 2026-10-10 追加（实测日志驱动）：**判据层**明细。规划只请求 Context|Needs（阈值 300，
                // 而采集周期是 150）⇒ 只要某一组的 State 不是 Complete，WaitingFacts 就会**永久**成立，
                // 而采集侧指标（oldestDueAgeTicks / omitted / errors）完全可以一切正常 —— 实测即如此。
                // 两组最可能的成因：Needs 恒 Unknown（"NeedTrackerUnavailable"：pawn.needs 为空）
                // 或戳校验失败被降级为 Error（"InvalidSourceStamp"：来源 CapturedTick/Revision 不符）。
                var facts = runtime.QueryPawnFacts(
                    new PersonalChronicle.Domain.PawnEconomy.Perception.PerceptionReadRequest(
                        key, runtime.PerceptionSessionEpoch, map.uniqueID,
                        PersonalChronicle.Domain.PawnEconomy.Perception.PerceptionPurpose.Planning,
                        PersonalChronicle.Domain.PawnEconomy.Perception.PerceptionGroups.Context
                            | PersonalChronicle.Domain.PawnEconomy.Perception.PerceptionGroups.Needs,
                        300, 500, 1200));
                if (facts == null) sb.AppendLine("    facts = null");
                else
                {
                    sb.Append("    facts: readiness=").Append(facts.Readiness)
                      .Append("  reason=").Append(facts.ReasonCode)
                      .Append("  usable=").Append(facts.RequiredGroupsUsable)
                      .AppendLine(facts.RequiredGroupsUsable ? string.Empty : "  ⚠ " + D("WillFailFacts"));
                    var groups = facts.Groups;
                    if (groups != null)
                        for (int g = 0; g < groups.Count; g++)
                        {
                            var status = groups[g];
                            // 只有"必需且未达 Ready"的组才是真正的问题源，其余降为次级显示（减少噪音）。
                            string flag = !status.Required ? "  ·"
                                : status.Readiness == PersonalChronicle.Domain.PawnEconomy.Perception.PerceptionReadiness.Ready ? "  ✓" : "  ⚠";
                            sb.Append("     ").Append(flag).Append(' ').Append(status.Group)
                              .Append(": state=").Append(status.CaptureState)
                              .Append(" age=").Append(status.AgeTicks)
                              .Append(status.Required ? " required" : " optional")
                              .Append(" → ").Append(status.Readiness)
                              .Append(" (").Append(status.ReasonCode).Append(')').AppendLine();
                        }
                }
                var blocked = plan.InstitutionallyBlocked;
                if (blocked != null && blocked.Count > 0)
                    sb.Append("    institutionallyBlocked=").Append(string.Join(", ", blocked)).AppendLine();
            }
        }
    }
}
