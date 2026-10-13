using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Partial of ArchiveService - skill archive (join vs end snapshot delta).
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {
        /// <summary>
        /// 生成技能档案：入队快照与当前（或死亡时）快照的对照及增量。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID。</param>
        /// <returns>按增量降序排列的技能行；无归档对象时返回空集合。</returns>
        /// <remarks>
        /// 终值取值有优先级：死亡快照（已归档且有数据）优先，其次存活 pawn 的实时技能，最后用入队快照兜底。
        /// 兜底分支保证在没有任何终值来源时仍产出"增量为 0"的行，而不是让 UI 出现空白。
        /// 键集合取两份快照的并集，避免只出现在其中一侧的技能被漏掉。
        /// </remarks>
        public IReadOnlyList<SkillArchiveView> GetSkillArchive(string stableId)
        {
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject == null)
            {
                return new List<SkillArchiveView>();
            }
            Dictionary<string, int> join = pawnObject.SkillSnapshot ?? new Dictionary<string, int>();
            Dictionary<string, int> end = new Dictionary<string, int>();
            if (pawnObject.IsArchived && pawnObject.SkillSnapshotOnDeath != null && pawnObject.SkillSnapshotOnDeath.Count > 0)
            {
                foreach (KeyValuePair<string, int> pair in pawnObject.SkillSnapshotOnDeath)
                {
                    end[pair.Key] = pair.Value;
                }
            }
            else
            {
                Pawn live = GetLivePawn(stableId);
                if (live != null && live.skills != null && live.skills.skills != null)
                {
                    List<SkillRecord> skills = live.skills.skills;
                    for (int i = 0; i < skills.Count; i++)
                    {
                        SkillRecord skill = skills[i];
                        if (skill != null && skill.def != null)
                        {
                            end[skill.def.defName] = skill.Level;
                        }
                    }
                }
                else if (join.Count > 0)
                {
                    foreach (KeyValuePair<string, int> pair in join)
                    {
                        end[pair.Key] = pair.Value;
                    }
                }
            }

            HashSet<string> keys = new HashSet<string>();
            foreach (string k in join.Keys) keys.Add(k);
            foreach (string k in end.Keys) keys.Add(k);
            List<SkillArchiveView> result = new List<SkillArchiveView>();
            foreach (string key in keys)
            {
                int j = 0;
                int e = 0;
                join.TryGetValue(key, out j);
                end.TryGetValue(key, out e);
                result.Add(new SkillArchiveView(key, j, e));
            }
            result.Sort((a, b) => b.Delta.CompareTo(a.Delta));
            return result;
        }
    }
}
