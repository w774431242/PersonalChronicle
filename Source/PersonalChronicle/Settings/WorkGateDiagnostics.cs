using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Automation;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Settings
{
    /// <summary>工作合同闸门 · **一键诊断落 Player.log**（CHG-20261012-WORKGATE · W3）。
    ///
    /// <para>用户点名要的"快速检测代码"按钮：把闸门的**全部输入**一次性打进日志 —— 开关 / 白名单 /
    /// 每张地图每个自由殖民者的"已授权工种集合" / 车间班次托管集合 / 该 pawn 的合同覆盖集合。
    /// 承接链排查（"派发了却没人接"）与"勾了不干活"排查都靠这份快照定位。</para>
    ///
    /// <para>只读：不写任何状态、不改格位、不触发结算。日志用 <c>[PC][WORKGATE]</c> 前缀，便于 grep。</para></summary>
    public static class WorkGateDiagnostics
    {
        public const string Tag = "[PC][WORKGATE]";

        public static void DumpToLog()
        {
            var sb = new StringBuilder();
            ChronicleSettings settings = PersonalChronicleMod.Settings;
            sb.AppendLine(Tag + " ==== 诊断快照 tick=" + (GenTicks.TicksGame.ToString()));
            sb.AppendLine(Tag + " 闸门开关=" + (settings != null && settings.EnableWorkContractGate ? "ON" : "OFF")
                + " · 白名单数=" + (settings?.WorkGateWhitelist?.Count ?? 0)
                + " [" + string.Join(",", settings?.WorkGateWhitelist ?? new List<string>()) + "]");

            List<Map> maps = Find.Maps;
            if (maps == null) { Log.Message(sb.ToString()); return; }
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || map.mapPawns == null) continue;
                HashSet<string> managed = AutoAssignService.ManagedWorkTypeDefNames(map);
                sb.AppendLine(Tag + " 地图#" + map.uniqueID + " 车间班次托管工种=" + managed.Count
                    + " [" + string.Join(",", managed) + "]");
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int j = 0; j < colonists.Count; j++)
                {
                    Pawn pawn = colonists[j];
                    if (pawn == null) continue;
                    string key = pawn.GetUniqueLoadID();
                    var contracts = new HashSet<string>();
                    GameComponent_PawnEconomy runtime = GameComponent_PawnEconomy.Current;
                    if (runtime != null)
                    {
                        try { runtime.CollectWorkAuthorityWorkTypes(key, contracts); }
                        catch (System.Exception ex) { sb.AppendLine(Tag + "   （合同读取异常：" + ex.Message + "）"); }
                    }
                    HashSet<string> allowed = WorkAuthorityService.AuthorizedWorkTypes(pawn);
                    sb.AppendLine(Tag + "   " + pawn.LabelShort + " (" + key + ")");
                    sb.AppendLine(Tag + "     合同覆盖工种=" + contracts.Count + " [" + string.Join(",", contracts) + "]");
                    sb.AppendLine(Tag + "     实际授权工种=" + (allowed == null ? "（闸门关闭⇒不过滤）" : allowed.Count.ToString())
                        + (allowed == null ? "" : " [" + string.Join(",", allowed) + "]"));
                    if (runtime != null) sb.AppendLine(Tag + "     求职规划=" + runtime.RecruitmentDiagnostic(pawn));
                }
            }
            Log.Message(sb.ToString());
            Messages.Message("PersonalChronicle.Settings.WorkGate.LogDone".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
