using System.Collections.Generic;

namespace PersonalChronicle
{
    /// <summary>工作授权来源（**四态**，判定用）。CHG-20261012-WORKGATE。
    /// <para>优先级仅用于**呈现**（UI 显示"为什么这格是开的"）；判定本身是**并集**（任一来源成立即可）。</para></summary>
    public enum WorkAuthoritySource
    {
        /// <summary>无授权 ⇒ 该工种的所有 giver 不入作业表（面板照旧勾选，显示合同锁）。</summary>
        None = 0,
        /// <summary>劳务站工作合同覆盖（粒度 = pawn × 工种；已承接的 Active 合同）。</summary>
        Contract = 1,
        /// <summary>车间班次托管（值勤 + 班次岗位映射）—— 视作**内部已发出的命令**。</summary>
        Shift = 2,
        /// <summary>生存白名单（灭火 / 对他人的医疗等；玩家可在设置里逐项开关）。</summary>
        Whitelist = 3,
    }

    /// <summary>
    /// 工作合同闸门的**纯判定核**（零引擎依赖 ⇒ 可离线断言；放在根命名空间 = 各层都可引用而不新增依赖边）。
    ///
    /// <para><b>为什么需要它</b>：本工程要让"劳务站合同"成为劳动的**唯一授权**，同时**不碰**原版工作面板的
    /// 勾选状态（勾选改为"可被雇佣"语义）。落点是唯一的 —— <c>Pawn_WorkSettings.CacheWorkGiversInOrder</c>
    /// 重建有序作业表时按工种过滤（已取证：两张表 Normal/Emergency 都只从 <c>wtsByPrio</c> 构建，
    /// 而 <c>wtsByPrio</c> 只收 <c>GetPriority &gt; 0</c> 的工种）。因此"授权"这一层判断必须是**可离线证伪**
    /// 的纯逻辑，而不是散在补丁里的 if。</para>
    ///
    /// <para><b>结构性例外（不在管辖内，已取证）</b>：进食 <c>JobGiver_GetFood</c>、自救 <c>JobGiver_SelfTend</c>、
    /// 卧床 <c>JobGiver_PatientGoToBed</c>、战斗/征召 <c>JobGiver_AIFightEnemy</c> 全是 <c>ThinkNode_JobGiver</c>，
    /// 以及 <c>workType</c> 为空的 giver（原版 3 个：自我急救 / 赶动物入栏 / 栏内再平衡）——它们**根本不进**
    /// 有序作业表，勾不勾、授权与否都管不到。</para>
    /// </summary>
    public static class WorkAuthority
    {
        /// <summary>主动维护槽的纯判定：首调用、时间回退或满 250 tick 才重建。</summary>
        public static bool RefreshDue(int now, int previous)
            => previous < 0 || now < previous || (long)now - previous >= 250;

        /// <summary>白名单默认值（生存底线）：灭火 + 医疗（含抢救 / 救治他人 / 喂食病人）。
        /// <para>依据：这两族走 <c>WorkGiver</c>（会被闸门拦），但不拦的替代路径不存在 —— 灭火没有 ThinkNode 版本；
        /// 对他人治疗也没有（只有**自我**急救是 <c>workType</c> 为空的 giver）。关掉白名单 = 纯拦截，
        /// 代价是"着火没人灭、伤者只能自救"（设置里可关，需玩家自担）。</para></summary>
        public static readonly string[] DefaultWhitelist = { "Firefighter", "Doctor" };

        /// <summary>该 giver 是否受闸门管辖：<c>workType</c> 为空者**不属于任何工种**（原版 3 个）⇒ 不纳管。</summary>
        public static bool ShouldGate(string workTypeDefName) => !string.IsNullOrEmpty(workTypeDefName);

        /// <summary>三源并集判定（判定即真值；优先级见 <see cref="Resolve"/>）。</summary>
        public static bool IsAllowed(bool contract, bool shift, bool whitelist) => contract || shift || whitelist;

        /// <summary>呈现用优先级：白名单 &gt; 车间托管 &gt; 合同 &gt; 无。</summary>
        public static WorkAuthoritySource Resolve(bool contract, bool shift, bool whitelist)
            => whitelist ? WorkAuthoritySource.Whitelist
                : shift ? WorkAuthoritySource.Shift
                : contract ? WorkAuthoritySource.Contract
                : WorkAuthoritySource.None;

        /// <summary>白名单成员判定（**精确 defName，大小写敏感** —— 稳定 ID，绝不做本地化/忽略大小写匹配）。</summary>
        public static bool IsWhitelisted(string workTypeDefName, IEnumerable<string> whitelist)
        {
            if (string.IsNullOrEmpty(workTypeDefName) || whitelist == null) return false;
            foreach (string name in whitelist)
                if (name == workTypeDefName) return true;
            return false;
        }

        /// <summary>把白名单整体写回（去空、去重、保持稳定顺序）—— 设置页与迁移共用同一实现。</summary>
        public static List<string> Normalize(IEnumerable<string> whitelist)
        {
            var result = new List<string>();
            if (whitelist == null) return result;
            foreach (string name in whitelist)
            {
                if (string.IsNullOrEmpty(name) || result.Contains(name)) continue;
                result.Add(name);
            }
            return result;
        }

        /// <summary>默认白名单的副本（设置页"恢复默认"与首次初始化共用）。</summary>
        public static List<string> Defaults() => new List<string>(DefaultWhitelist);
    }

    /// <summary>
    /// 闸门的**失效入口**（唯一实现）：让所有自由殖民者的有序作业表下次取用时重建。
    /// <para>放在根层：只依赖 <c>Find</c>/<c>Pawn</c>（引擎），不依赖任何业务层 —— 于是设置页（根层）与
    /// Economy 服务都能调同一份实现，而不是各写一遍（也避免 RootSettings→Economy 的新依赖边）。</para>
    /// <para>代价有界：每张地图的自由殖民者各打一次 dirty 标记；真正的重建发生在下一次取表。</para>
    /// </summary>
    public static class WorkAuthorityInvalidator
    {
        /// <summary>可重建缓存的版本，不写档；设置在暂停同 tick 修改时也能让 UI 失效。</summary>
        public static int Revision { get; private set; }
        public static void InvalidateAll()
        {
            unchecked { Revision++; }
            try
            {
                List<Verse.Map> maps = Verse.Find.Maps;
                if (maps == null) return;
                for (int i = 0; i < maps.Count; i++)
                {
                    Verse.Map map = maps[i];
                    if (map == null || map.mapPawns == null) continue;
                    var colonists = map.mapPawns.FreeColonistsSpawned;
                    for (int j = 0; j < colonists.Count; j++)
                    {
                        Verse.Pawn pawn = colonists[j];
                        if (pawn == null || pawn.workSettings == null) continue;
                        pawn.workSettings.Notify_UseWorkPrioritiesChanged();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Verse.Log.ErrorOnce("[PC] 工作授权失效失败：" + ex.Message, 0x5A17E2);
            }
        }
    }
}
