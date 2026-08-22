using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// 勋章授予服务（阶段一：阈值类 Pawn 勋章写入链路）。
    ///
    /// 判定引擎 <see cref="MedalAwardEvaluator"/> 是纯逻辑、零副作用；本服务是
    /// 它唯一的"写入方"——把 NewAwards 持久化到 <see cref="PawnObject.GrantedMedals"/>
    /// （append-only，去重），并触发 <see cref="ChronicleGameComponent.MarkChanged"/>
    /// 使 Read Model 失效重建。
    ///
    /// 只对活着的归档殖民者判定（对齐 reconcile 活读语义）；死亡者不追授，
    /// 生涯台账冻结语义一致。触发频率由调用方（reconcile 节流）控制。
    /// </summary>
    public static class MedalAwardService
    {
        /// <summary>
        /// 对当前存活归档殖民者执行一次授勋判定与写入。
        /// </summary>
        /// <returns>本次新授予的勋章 Def（按授予顺序），调用方可据此弹公告；无新授予返回空列表。</returns>
        public static List<MedalDef> Run(ChronicleGameComponent component)
        {
            List<MedalDef> newAwards = new List<MedalDef>();
            if (component == null || component.Objects == null)
            {
                return newAwards;
            }

            // Recording gate 与工作采样/reconcile 共享同一开关。
            if (PersonalChronicleMod.Settings == null || !PersonalChronicleMod.Settings.EnableRecording)
            {
                return newAwards;
            }

            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember member = live[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                Pawn pawn = member.Pawn;
                if (pawn.GetUniqueLoadID() == null)
                {
                    continue;
                }

                IReadOnlyList<PawnRecord> records = component.GetRecordsFor(pawn);
                if (records == null || records.Count == 0)
                {
                    // 尚未建档（reconcile 确认窗口未关），下次再判。
                    continue;
                }
                PawnObject pawnObject = records[0] as PawnObject;
                if (pawnObject == null || pawnObject.IsArchived)
                {
                    continue;
                }

                MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawnObject);
                List<MedalEvaluation> pending = result.NewAwards;
                if (pending == null || pending.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < pending.Count; j++)
                {
                    MedalEvaluation eval = pending[j];
                    if (eval == null || eval.Def == null)
                    {
                        continue;
                    }
                    pawnObject.AddGrantedMedal(eval.Def.defName);
                    newAwards.Add(eval.Def);
                    // T3 金质公告：NewAwards 仅含"未授予"判定，写入即首次，恰好弹一次。
                    AnnounceGold(pawn, eval.Def);
                }
                component.MarkChanged();
            }

            return newAwards;
        }

        /// <summary>
        /// 金质勋章授勋公告（架构方案 §6.9 公告 Letter，仅首次、恰好一次）。
        /// 标题 = 勋章 Label 翻译（如「劳动模范·金质」）；正文 = UI.Medal.Gold.Letter
        /// （{0} = 人物名，{1} = 无材质称号名）；LookTargets 指向受勋者。
        /// 签名经引擎反射核验（1.6：ReceiveLetter(TaggedString, TaggedString, LetterDef,
        /// LookTargets, Faction, Quest, List&lt;ThingDef&gt;, string, int, bool)）。
        /// </summary>
        private static void AnnounceGold(Pawn pawn, MedalDef def)
        {
            if (def == null || def.tier != MedalTier.Gold || pawn == null)
            {
                return;
            }

            string label = MedalTranslationKeys.Label(def.defName).Translate().ToString();
            string seriesName = MedalTranslationKeys.SeriesName(MedalDef.SeriesKeyOf(def.defName)).Translate();
            string text = MedalTranslationKeys.GoldLetter().Translate(pawn.LabelShort, seriesName).ToString();

            Find.LetterStack.ReceiveLetter(
                label,
                text,
                LetterDefOf.PositiveEvent,
                new LookTargets(pawn),
                null,
                null,
                null,
                null,
                0,
                true);
        }
    }
}
