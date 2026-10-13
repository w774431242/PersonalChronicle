using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// [MATRIX-003] UI 层访问持久化组件的唯一门面：Archive（UI）禁止直接引用
    /// <see cref="ChronicleGameComponent"/>（分层单向铁律：UI → Application → Data）。
    ///
    /// 本类收口原先散落在 ITab / Dialog 中的「GetComponent + GetObject/GetRecordsFor
    /// + 写入 + MarkChanged」模式；只做转发与空安全，不携带业务规则，调用方以返回值
    /// 组装各自提示文案。
    ///
    /// 豁免登记（MATRIX-003 备注）：DevTestButtons*（开发调试工具，EXC-2026-002）。
    /// ReadModels Provider 已下沉 Application/ReadModels（MATRIX-003b Phase 1，2026-08-29 完成）。
    /// </summary>
    public static class ChroniclePawnAccess
    {
        /// <summary>取 pawn 的 PawnObject（经 GetRecordsFor 首条；勋章/资格流语义）。无档案返回 null。</summary>
        public static PawnObject GetPawnObject(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            if (comp == null)
            {
                return null;
            }
            var records = comp.GetRecordsFor(pawn);
            if (records == null || records.Count == 0)
            {
                return null;
            }
            return records[0] as PawnObject;
        }

        /// <summary>取 pawn 的 PawnObject（经 stableId 直查；总览/字段注册表语义）。无档案返回 null。</summary>
        public static PawnObject GetArchivedPawnObject(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            return comp != null ? comp.GetObject(pawn.GetUniqueLoadID()) as PawnObject : null;
        }

        /// <summary>手动授予已合格职称（资格页「确认授予」按钮）。成功返回 true。</summary>
        public static bool TryManualGrantQualifiedTitle(PawnObject po, QualificationDef def)
        {
            if (po == null || def == null || Current.Game == null)
            {
                return false;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            return comp != null && comp.ManualGrantQualifiedTitle(po, def);
        }

        /// <summary>设定主方向并落盘。成功（有档案）返回 true；无档案返回 false。</summary>
        public static bool SetPrimaryDirection(Pawn pawn, ProfessionalDirectionDef dir)
        {
            if (pawn == null || dir == null || Current.Game == null)
            {
                return false;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            if (comp == null)
            {
                return false;
            }
            PawnObject po = comp.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (po == null || po.CareerData == null)
            {
                return false;
            }
            if (po.CareerData.Professional == null)
            {
                po.CareerData.Professional = new ProfessionalState();
            }
            po.CareerData.Professional.primaryDirection = dir.defName;
            comp.MarkChanged();
            return true;
        }

        /// <summary>清除主方向（append-only 友好：null = 不限定方向）。有档案且落盘返回 true。</summary>
        public static bool ClearPrimaryDirection(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return false;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            if (comp == null)
            {
                return false;
            }
            PawnObject po = comp.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (po == null || po.CareerData == null || po.CareerData.Professional == null)
            {
                return false;
            }
            po.CareerData.Professional.primaryDirection = null;
            comp.MarkChanged();
            return true;
        }

        /// <summary>
        /// 设定人物显示称号（空 = 自动；HonorDisplayKeys.None = 隐藏；title:/medal: 前缀 = 指定条目）。
        /// 有档案且落盘返回 true。
        /// </summary>
        public static bool SetDisplayedHonor(Pawn pawn, string honorKey)
        {
            if (pawn == null || Current.Game == null)
            {
                return false;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            if (comp == null)
            {
                return false;
            }
            PawnObject po = comp.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (po == null || po.CareerData == null)
            {
                return false;
            }
            po.CareerData.DisplayedHonorKey = string.IsNullOrEmpty(honorKey) ? null : honorKey;
            comp.MarkChanged();
            return true;
        }
    }
}
