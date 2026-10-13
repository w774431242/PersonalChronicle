using PersonalChronicle.Data;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Partial of <see cref="ArchiveService"/> —— 物品级产消读数（`REQ-MVS-013` /
    /// `CHG-MVS-ITEMFLOW-001`，2026-09-20）。
    ///
    /// <para><b>为什么是本分部（GATE-ARCH A-01「具名门径」）</b>：物品级账本挂在
    /// <c>ChronicleGameComponent</c>（Data），市场面板的 Provider 在 Application ——
    /// 跨体系访问必须经**一个具名门径**，不得由 Provider 直取组件内部。
    /// 本分部即该门径（与 `ManufacturingLayoutService` / `MarketAccess` 同规）。</para>
    ///
    /// <para><b>依赖方向</b>：Capture（Harmony 钩子）→ <c>IArchiveService</c>（Application）
    /// → <c>ChronicleGameComponent</c>（Data）—— 与 `Patch_GenRecipe` → `RecordCareerProduced`
    /// 的既有链完全同向（Application→Data 是本工程既成方向）。</para>
    ///
    /// <para><b>失败语义</b>：写失败静默降级（catch + Warning，不影响原版流程）；
    /// 读失败返回 <c>false</c>（调用方画 `—`，不得填 0）。</para>
    /// </summary>
    public sealed partial class ArchiveService
    {
        /// <summary>
        /// 记一笔物品级产消（由 Capture 层的只读钩子调用）：
        /// <paramref name="production"/> true = 生产、false = 消费；<paramref name="qty"/> = 件数。
        /// </summary>
        public void RecordItemFlow(string defName, int qty, bool production)
        {
            if (string.IsNullOrEmpty(defName) || qty <= 0)
            {
                return;
            }
            if (!IsRecordingEnabled())
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.RecordItemFlow(defName, qty, production);
            }
            catch (System.Exception ex)
            {
                // 只读钩子内不得外抛（Prefix 外抛会中断原版方法）。
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "item flow record failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 读某物品近两季的日均产/消速率（件/日）。
        /// <c>false</c> = 无账本 / 账本未建立（**不是 0**）—— 调用方必须画 `—`。
        /// </summary>
        public bool TryGetItemFlowRate(string defName, bool production, out float perDay)
        {
            perDay = 0f;
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return false;
            }
            return component.TryGetItemFlowRate(defName, production, out perDay);
        }

        /// <summary>
        /// 记一笔原料流向（`REQ-MVS-018`）。失败静默降级 —— 调用方在只读 Prefix 内，不得外抛。
        /// </summary>
        public void RecordMaterialFlow(string material, string product, int qty)
        {
            if (string.IsNullOrEmpty(material) || string.IsNullOrEmpty(product) || qty <= 0)
            {
                return;
            }
            if (!IsRecordingEnabled())
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.RecordMaterialFlow(material, product, qty);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "material flow record failed: " + ex.Message);
            }
        }

        /// <summary>读某物品作为原料时近两季的流向份额（降序）。<c>false</c> = 无记录（非"无流向"）。</summary>
        public bool TryGetMaterialFlows(string material,
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> result)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return false;
            }
            return component.TryGetMaterialFlows(material, result);
        }
    }
}
