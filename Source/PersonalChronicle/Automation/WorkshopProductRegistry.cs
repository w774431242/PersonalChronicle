using System;
using System.Collections.Generic;
using PersonalChronicle.Application.Effects;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间成品加成注册表（落地管线 Step 5c：精度/耐久挂载）。
    ///
    /// <para>⚠ 2026-09-21（CHG-SHOP-AUDIT-001 · A-1）：本文件原定义的
    /// <c>WorkshopProductBonus</c> 快照已上移至
    /// <c>Application/Effects/WorkshopBonusData.cs</c>（Effects/Capture 需消费它）。</para>
    ///
    /// 为什么需要它：RimWorld 的 MaxHitPoints / Accuracy* 都是 <b>Thing 级 Stat</b>，
    /// 没有"逐实例改 statBases"的接口。可行且原生的做法是在这些 StatDef 上挂 StatPart，
    /// 由 StatPart 在求值（开火/计算耐久）时按「这件东西是不是车间产出」决定要不要乘系数。
    /// 因此需要一张 thingID → 系数的登记表。
    ///
    /// ARC-008 受控静态状态声明：本表为「进程级受控可变索引」，
    /// 生命周期 = 成品从产出到消失；<b>有界</b>（超过 MaxEntries 整表清空，见 <see cref="Mark"/>），
    /// 且只存 int/float 不存 Thing 引用（不阻止任何对象被 GC）。
    /// 因此不存在泄漏与跨存档污染风险。
    /// </summary>
    public static class WorkshopProductRegistry
    {
        /// <summary>条目上限。超出即整表清空（最老的一批加成自然失效），保证内存有界。</summary>
        private const int MaxEntries = 512;

        private static readonly Dictionary<int, WorkshopProductBonus> entries =
            new Dictionary<int, WorkshopProductBonus>();

        /// <summary>当前存档（<c>Game</c> 实例）标识 —— 换档/新开一局时自动清表，见 <see cref="EnsureGameScope"/>。</summary>
        private static object ownerGame;

        /// <summary>
        /// 作用域守卫：<c>thingIDNumber</c> 是**按存档**重新计数的，若表不清，新局里 ID 相同的新物品
        /// 会莫名继承上一局的耐久/精度加成（2026-09-11 体检 P1-5：原实现只在
        /// <c>MapComponent_WorkshopManager</c> 读档时清表，**同进程新开一局不清**）。
        /// 比较 Game 实例引用，一行成本；离线（单测/引擎未起）时跳过判断。
        /// </summary>
        private static void EnsureGameScope()
        {
            object game;
            try
            {
                game = Current.Game;
            }
            catch (Exception)
            {
                return;
            }
            if (!ReferenceEquals(ownerGame, game))
            {
                entries.Clear();
                ownerGame = game;
            }
        }

        /// <summary>
        /// 登记一件成品的加成。只在确有加成时登记（无加成不占条目）。
        /// </summary>
        public static void Mark(int thingId, float maxHpFactor, float accuracyFactor)
        {
            bool hasHp = maxHpFactor > 1.0001f;
            bool hasAcc = accuracyFactor > 1.0001f;
            if (!hasHp && !hasAcc)
            {
                return;
            }
            EnsureGameScope();
            if (entries.Count >= MaxEntries)
            {
                // 有界保护：整表清空（旧的加成自然过期），不做逐条 LRU 以省开销。
                entries.Clear();
            }
            entries[thingId] = new WorkshopProductBonus
            {
                maxHpFactor = hasHp ? maxHpFactor : 1f,
                accuracyFactor = hasAcc ? accuracyFactor : 1f
            };
        }

        /// <summary>取某件成品的加成；未登记返回 false。</summary>
        public static bool TryGet(int thingId, out WorkshopProductBonus bonus)
        {
            EnsureGameScope();
            return entries.TryGetValue(thingId, out bonus);
        }

        /// <summary>清空（Def reload / 存档切换时调用，防跨档污染）。</summary>
        public static void Clear()
        {
            entries.Clear();
        }
    }
}
