using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
// B2：`InstitutionModeSnapshot` / `InstitutionPhase` 属**制度阶段**（≠ 被净删的机构政策）——
// 两者不同概念，制度阶段继续保留并驱动卡片左色条。
using PersonalChronicle.Domain.PawnEconomy.Governance;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// **机构卡列表**（机构页的卡片化绘制）。
    ///
    /// <para><b>数据来源</b>：直接读结构化读模型（<c>QueryInstitutions</c> + 制度快照），
    /// <b>不</b>修改应用层、<b>不</b>解析字符串 —— 卡片上的每个字段都来自权威读数。
    /// 索引按 <c>view</c> 引用缓存（每 60 tick 或制度快照变更时才重建），不引入逐帧查询。</para>
    ///
    /// <para><b>几何纪律</b>：高度/宽度由 <c>RUIF.Measure</c> 与 <c>RUIF.Tokens</c> 派生，无手写魔数。</para>
    ///
    /// <para><b>B2（机构政策净删）</b>：卡片退为**单行只读**（`类型 · 建筑名` + 制度阶段语义色条）——
    /// 营业时段与能力徽章都建立在机构政策之上，随政策一并退役（概念无载体 ⇒ 不再展示，而不是展示一个
    /// 假读数）。卡片**没有任何可点入口**（F4），`Draw` 恒返回 <c>false</c>；行种标记动词由 `Policy`
    /// 改名 <c>InstitutionCard</c>（应用层同批改名，两侧一致）。</para>
    /// </summary>
    internal sealed class EconomyInstitutionCards
    {
        /// <summary>机构行的专属动作 verb —— B2 起它**只是行种标记**（无任何动作语义）：应用层给机构行发该
        /// 动词、本组件据此识别卡片行。B2 前该值叫 `Policy`（那时确实有政策深链）；政策退役后改名，
        /// 免得继续暗示一个不存在的目标（GOV-009：动词是稳定 ID，不参与显示）。</summary>
        internal const string InstitutionRowVerb = "InstitutionCard";

        private readonly Map map;
        private Dictionary<string, InstitutionRef> institutionIndex;
        private EconomyView institutionIndexView;
        /// <summary>P2-3：索引失效口径带上**语言**键（切换语言后卡片标题最多滞后 60 tick 才刷新）。</summary>
        private object institutionIndexLanguage;
        /// <summary>P0-5：制度快照随结构化索引一并缓存（每 view 版本取一次），
        /// 消除"每帧每张卡各查一次 <c>QueryInstitutionMode</c>"的重复开销。制度阶段**不随政策退役**
        /// （它是制度开关的读数，不是政策读数）。</summary>
        private InstitutionModeSnapshot institutionModeSnapshot;
        /// <summary>P0-6：卡片标题文本随索引快照化（"类型 · 建筑名"），消除绘制路径内的每帧字符串拼接。</summary>
        private Dictionary<string, string> nameIndex;

        public EconomyInstitutionCards(Map map) { this.map = map; }

        /// <summary>该行是否属于机构卡片（应用层给机构行发的动作固定为 <c>InstitutionCard</c> 且 Key 为 anchorKey）。
        /// **数据驱动**：宿主无需判断"当前是哪一页"—— 非机构页的行不会带这个动词组合，故组件可被无条件挂上。</summary>
        public static bool IsCardRow(EconomyRow row)
            => row != null && row.Verb == InstitutionRowVerb && !string.IsNullOrEmpty(row.Key);

        /// <summary>行族自定义行高：卡片行返回卡高，其余行返回 0（= 通用行）。</summary>
        public float HeightOf(EconomyRow row) => IsCardRow(row) ? CardHeight : 0f;

        /// <summary>卡片高度 = 单行正文 + 上下内边距（B2：能力 chip 行随政策退役 ⇒ 卡高减一行，
        /// 仍全部由度量派生）。</summary>
        private float CardHeight
            => RUIF.Measure.RowHeight(GameFont.Small) + RUIF.Tokens.CardPadY * 2f;

        /// <summary>绘制卡片。**恒返回 <c>false</c>**：B2 之后卡片是纯只读展示（机构名 + 制度阶段色条），
        /// 没有任何可点入口 —— 返回 <c>true</c> 只会让宿主去找一个不存在的导航目标（F4）。</summary>
        public bool Draw(EconomyView view, Rect content, float y, EconomyRow row)
        {
            EnsureIndex(view);
            if (institutionIndex == null || !institutionIndex.ContainsKey(row.Key)) return false;
            var card = new Rect(content.x + EconomyRowList.CardSideInset, y,
                content.width - EconomyRowList.CardSideInset * 2f, CardHeight);
            // P0-1：卡片骨架走公共件（底/描边/左语义色条/内边距唯一实现）。
            var inner = EconomyUiKit.CardFrame(card, Mouse.IsOver(card),
                EconomyUiKit.PhaseColor(institutionModeSnapshot?.Phase ?? InstitutionPhase.Off));
            // P0-6：读快照标题，不再每帧做两次字符串拼接。
            string nameLabel = nameIndex != null && nameIndex.TryGetValue(row.Key, out string cachedName) ? cachedName : row.Key;
            var nameRect = new Rect(inner.x, inner.y, inner.width, RUIF.Measure.RowHeight(GameFont.Small));
            RUIF.Draw.EllipsisLabel(nameRect, nameLabel, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            // P2-3「列表行 tooltip 全覆盖」：省略号只截显示，不丢信息（LAND-008 成对纪律）。
            if (Mouse.IsOver(nameRect)) TooltipHandler.TipRegion(nameRect, nameLabel);
            return false;
        }

        /// <summary>按 <c>view</c> 引用缓存结构化索引：view 每 60 tick 或制度快照变更才重建，卡片化不引入逐帧查询。
        /// 标题（"类型 · 建筑名"）在同一处算好，绘制只读。缓存键由**宿主传进来的 view** 决定 ——
        /// 两个宿主各持一个组件实例，互不干扰。</summary>
        private void EnsureIndex(EconomyView view)
        {
            if (ReferenceEquals(institutionIndexView, view) && institutionIndex != null
                && ReferenceEquals(institutionIndexLanguage, LanguageDatabase.activeLanguage)) return;
            institutionIndexView = view;
            institutionIndexLanguage = LanguageDatabase.activeLanguage;
            institutionIndex = new Dictionary<string, InstitutionRef>(System.StringComparer.Ordinal);
            nameIndex = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null || map == null) return;
            // P0-5：制度快照随索引一并快照化 —— 绘制路径不再出现任何 runtime.Query*。
            institutionModeSnapshot = runtime.QueryInstitutionMode();
            IReadOnlyList<InstitutionRef> refs;
            try { refs = runtime.QueryInstitutions(map.uniqueID); }
            catch (System.Exception error) { Log.ErrorOnce("[PC][UI] institution index failed: " + error.Message, 0x1EC0DE1); return; }
            for (int i = 0; i < refs.Count; i++)
            {
                var item = refs[i];
                if (item == null || string.IsNullOrEmpty(item.AnchorKey)) continue;
                institutionIndex[item.AnchorKey] = item;
                string anchorLabel = item.Anchor != null ? item.Anchor.LabelCap.ToString() : item.AnchorKey;
                nameIndex[item.AnchorKey] = EconomyLabels.KindLabel(item.Kind) + " · " + anchorLabel;
            }
        }
    }
}
