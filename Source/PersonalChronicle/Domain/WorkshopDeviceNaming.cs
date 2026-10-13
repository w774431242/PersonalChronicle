using System.Text;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 车间设备「自定义名」的规则层 —— <b>纯函数，可离线单测</b>（沿 <see cref="WorkshopDeviceRules"/> 先例）。
    ///
    /// <para><b>为什么抽到 Domain</b>：归一化与重名判定是<b>无引擎依赖的字符串规则</b>，而它的消费者分处两层
    /// —— Automation（写入校验）与 Capture（读路径兜底）。两处各写一份必然漂移（本工程历史教训：
    /// 同一口径三处实现，两处漏换算）。</para>
    ///
    /// <para><b>⚠ 必须清洗 XML 非法字符（P0-A，2026-10-05 Gate 预审）</b>：自定义名是玩家自由文本，
    /// 而它会被 Scribe 写进存档 XML。若名里含 U+0000–U+0008 / U+000B / U+000C / U+000E–U+001F 这类
    /// 控制字符，保存时 XML 解析失败 ⇒ <b>整档保存失败</b>（本库 2026-10-02 已有同族事故，
    /// 故既有全部台账都在保存前清洗）。清洗放在<b>唯一写口</b>之前完成，保证「内存值 == 存档值」；
    /// 读路径不再二次清洗。</para>
    /// </summary>
    public static class WorkshopDeviceNaming
    {
        /// <summary>
        /// 自定义名长度上限（字符）。超长名会把设备页名称列与悬停提示撑坏（UI-009 定宽列必须预截断），
        /// 且会进入 RUIF / IMGUI 的文本度量缓存 —— 故在入口拒绝，而不是事后截断（不悄悄改短玩家输入）。
        /// </summary>
        public const int MaxLength = 24;

        /// <summary>XML 1.0 不允许、且会被 XmlWriter 拒绝的控制字符（制表/换行/回车例外，它们是合法的）。</summary>
        private static bool IsInvalidXmlChar(char c)
        {
            return c < 0x20 && c != '\t' && c != '\n' && c != '\r';
        }

        /// <summary>
        /// 归一化：去首尾空白 → 空/纯空白转 <c>null</c> → 剔除 XML 非法字符 → 再次去首尾空白。
        ///
        /// <para><b>返回 null 的语义 = 清除自定义名</b>，显示回落链（车间名 ?? 档案覆盖 ?? 原版默认名，
        /// REQ-WORKSHOP-005）。</para>
        /// <para>仅清洗、<b>不截断</b>：超长由调用方按 <see cref="MaxLength"/> 拒绝。</para>
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }
            string trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }
            // 快路径：绝大多数名字不含非法字符 ⇒ 不分配 StringBuilder。
            int firstInvalid = -1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (IsInvalidXmlChar(trimmed[i]))
                {
                    firstInvalid = i;
                    break;
                }
            }
            if (firstInvalid < 0)
            {
                return trimmed;
            }
            StringBuilder sb = new StringBuilder(trimmed.Length);
            sb.Append(trimmed, 0, firstInvalid);
            for (int i = firstInvalid + 1; i < trimmed.Length; i++)
            {
                if (!IsInvalidXmlChar(trimmed[i]))
                {
                    sb.Append(trimmed[i]);
                }
            }
            string cleaned = sb.ToString().Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        /// <summary>长度是否在限内（入参须已归一化）。null/空视为合法（= 清除操作）。</summary>
        public static bool IsWithinLength(string normalized)
        {
            return string.IsNullOrEmpty(normalized) || normalized.Length <= MaxLength;
        }

        /// <summary>重名比较（O-2 裁决）：Trim + <see cref="System.StringComparison.OrdinalIgnoreCase"/>。</summary>
        public static bool SameName(string a, string b)
        {
            return string.Equals(
                a == null ? null : a.Trim(),
                b == null ? null : b.Trim(),
                System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 两个名是否构成重名冲突。
        /// <para><b>空名不参与判重</b>：清空（→ null）不受"禁止重名"限制（O-2 裁决）。</para>
        /// </summary>
        public static bool Conflicts(string candidate, string existing)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(existing))
            {
                return false;
            }
            return SameName(candidate, existing);
        }
    }
}
