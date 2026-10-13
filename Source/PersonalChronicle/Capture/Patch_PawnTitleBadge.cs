using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using PersonalChronicle;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// P9-TitleBadge：把已获职称作为人物「称号」显示，覆盖原版全部涉及场景：
    ///   1) 地图游戏画面 — GenMapUI.DrawPawnLabel（彩色徽章，Tier 着色）；
    ///   2) 顶栏 ColonistBar — 走 Pawn.LabelShort（文本追加）；
    ///   3) 人物档案 ITab / 检视面板 — 走 Pawn.LabelShort（文本追加）；
    ///   4) 选中信息条 — 走 Pawn.LabelShort（文本追加）。
    ///
    /// 取数规则：只取主方向（ProfessionalState.primaryDirection）对应的已授 Title 中
    /// order 最高的一档作为唯一称号，主标题（职称）+ 副标题（方向）。
    ///
    /// 冲突消解：地图场景由 DrawPawnLabel 自绘彩色徽章，故 LabelShort 在地图绘制期间
    /// 通过线程静态标记 DrawingMapLabel 跳过文本追加，避免双重显示。
    ///
    /// 治理：本文件 2 个 [HarmonyPatch]（Patch_PawnTitleBadge_Map / _Label）于 2026-08-22 补登
    /// EXC-2026-001（清单 #15/#16），符合 COMP-004「原生机制无完整捕获/装饰接口」前提。
    /// </summary>
    [HarmonyPatch(typeof(Verse.GenMapUI))]
    [HarmonyPatch("DrawPawnLabel")]
    [HarmonyPatch(new Type[] {
        typeof(Pawn), typeof(Vector2), typeof(float), typeof(float),
        typeof(System.Collections.Generic.Dictionary<string, string>), typeof(GameFont), typeof(bool), typeof(bool)
    })]
    public static class Patch_PawnTitleBadge_Map
    {
        [ThreadStatic]
        internal static bool DrawingMapLabel;

        public static void Postfix(Pawn pawn, Vector2 pos, float alpha)
        {
            // 标记当前正处于地图名牌绘制中，供 get_LabelShort Postfix 跳过文本追加，
            // 避免与下方彩色徽章双重显示。进入置 true，finally 置回 false。
            DrawingMapLabel = true;
            try
            {
                if (pawn == null || alpha <= 0.01f)
                {
                    return;
                }
                TitleBadgeHelper.TitleBadgeInfo badge = TitleBadgeHelper.Resolve(pawn);
                if (badge == null)
                {
                    return;
                }

                Color tierColor = Archive.UI.UITheme.TitleTierColor(badge.Tier);
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                Color prevColor = GUI.color;
                float prevAlpha = GUI.color.a;
                try
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    GUI.color = new Color(tierColor.r, tierColor.g, tierColor.b, alpha);

                    float titleW = Text.CalcSize(badge.Title).x;
                    float dirW = string.IsNullOrEmpty(badge.Direction) ? 0f : Text.CalcSize(badge.Direction).x;
                    float width = Mathf.Max(titleW, dirW, 60f);
                    Rect titleRect = new Rect(pos.x - width / 2f, pos.y + 22f, width, 18f);
                    Widgets.Label(titleRect, badge.Title);

                    if (!string.IsNullOrEmpty(badge.Direction))
                    {
                        Rect dirRect = new Rect(pos.x - width / 2f, pos.y + 38f, width, 16f);
                        Widgets.Label(dirRect, badge.Direction);
                    }
                }
                finally
                {
                    Text.Font = prevFont;
                    Text.Anchor = prevAnchor;
                    GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, prevAlpha);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[PersonalChronicle] Patch_PawnTitleBadge_Map failed: " + ex);
            }
            finally
            {
                DrawingMapLabel = false;
            }
        }
    }

    /// <summary>
    /// 顶栏 / 档案 / 选中条等走 LabelShort 的场景：追加称号文本（原版自动布局，无重叠）。
    /// 地图绘制期间（DrawingMapLabel=true）跳过，避免与彩色徽章双重显示。
    /// 注意：Pawn.get_LabelShort 是超高频热路径，被 ITab/档案/选中条/ColonistBar 大量调用。
    /// 仅在 ArchiveService 已就绪（游戏载入后）才尝试解析，避免加载期异常拖垮 UI。
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "get_LabelShort")]
    public static class Patch_PawnTitleBadge_Label
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            try
            {
                // 关键守卫：地图绘制期由彩色徽章负责，跳过文本追加避免双重；
                // 且仅当游戏已载入（Current.Game 存在）时解析，防止加载期/主菜单期
                // 访问 ArchiveService / DefDatabase 触发异常，进而拖垮依赖 LabelShort 的 ITab。
                if (Patch_PawnTitleBadge_Map.DrawingMapLabel || Current.Game == null)
                {
                    return;
                }
                TitleBadgeHelper.TitleBadgeInfo badge = TitleBadgeHelper.Resolve(__instance);
                if (badge == null || string.IsNullOrEmpty(badge.Title))
                {
                    return;
                }
                // 殖民者栏按 LabelShort 单行排版：禁止插入换行，否则多条称号会叠在相邻头像上。
                if (__result.IndexOf(badge.Title, StringComparison.Ordinal) < 0)
                {
                    __result = __result + " · " + badge.Title;
                }
            }
            catch (Exception)
            {
                // get_LabelShort is hot path — never break the call site.
            }
        }
    }

    /// <summary>
    /// 共享取数：从 Pawn 解析主方向最高档已授 Title 的称号信息。
    /// </summary>
    internal static class TitleBadgeHelper
    {
        internal class TitleBadgeInfo
        {
            public string Title;
            public string Direction;
            public int Tier;
        }

        internal static TitleBadgeInfo Resolve(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null)
            {
                return null;
            }
            ArchiveObject obj = service.GetObject(pawn.GetUniqueLoadID());
            PawnObject pawnObj = obj as PawnObject;
            if (pawnObj == null || pawnObj.CareerData == null)
            {
                return null;
            }
            CareerData cd = pawnObj.CareerData;
            if (HonorDisplayKeys.IsNone(cd.DisplayedHonorKey))
            {
                return null;
            }

            string titleDefName;
            string medalDefName;
            if (HonorDisplayKeys.TryParseTitle(cd.DisplayedHonorKey, out titleDefName))
            {
                return FromTitleDefName(cd, titleDefName);
            }
            if (HonorDisplayKeys.TryParseMedal(cd.DisplayedHonorKey, out medalDefName))
            {
                return FromMedalDefName(pawnObj, medalDefName);
            }

            TitleBadgeInfo autoTitle = BestTitleForPrimary(cd);
            if (autoTitle != null)
            {
                return autoTitle;
            }
            return BestMedal(pawnObj);
        }

        private static TitleBadgeInfo FromTitleDefName(CareerData cd, string titleDefName)
        {
            if (cd == null || cd.GrantedTitles == null || string.IsNullOrEmpty(titleDefName))
            {
                return null;
            }
            bool granted = false;
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                GrantedTitle g = cd.GrantedTitles[i];
                if (g != null && string.Equals(g.TitleDefName, titleDefName, StringComparison.Ordinal))
                {
                    granted = true;
                    break;
                }
            }
            if (!granted)
            {
                return null;
            }
            ProfessionalTitleDef def = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(titleDefName);
            return def == null ? null : FromTitleDef(def);
        }

        private static TitleBadgeInfo FromMedalDefName(PawnObject pawnObj, string medalDefName)
        {
            if (pawnObj == null || pawnObj.GrantedMedals == null || string.IsNullOrEmpty(medalDefName))
            {
                return null;
            }
            bool granted = false;
            for (int i = 0; i < pawnObj.GrantedMedals.Count; i++)
            {
                if (string.Equals(pawnObj.GrantedMedals[i], medalDefName, StringComparison.Ordinal))
                {
                    granted = true;
                    break;
                }
            }
            if (!granted)
            {
                return null;
            }
            MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(medalDefName);
            return FromMedalDef(def, medalDefName);
        }

        private static TitleBadgeInfo BestTitleForPrimary(CareerData cd)
        {
            if (cd == null || cd.GrantedTitles == null || cd.GrantedTitles.Count == 0)
            {
                return null;
            }
            string primary = cd.Professional != null ? cd.Professional.primaryDirection : null;
            ProfessionalTitleDef bestMatch = null;
            ProfessionalTitleDef bestAny = null;
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                GrantedTitle g = cd.GrantedTitles[i];
                if (g == null || string.IsNullOrEmpty(g.TitleDefName))
                {
                    continue;
                }
                ProfessionalTitleDef def = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(g.TitleDefName);
                if (def == null)
                {
                    continue;
                }
                if (bestAny == null || def.order > bestAny.order)
                {
                    bestAny = def;
                }
                if (TitleMatchesPrimary(def, primary)
                    && (bestMatch == null || def.order > bestMatch.order))
                {
                    bestMatch = def;
                }
            }
            ProfessionalTitleDef chosen = bestMatch != null ? bestMatch : bestAny;
            return chosen == null ? null : FromTitleDef(chosen);
        }

        private static bool TitleMatchesPrimary(ProfessionalTitleDef def, string primary)
        {
            if (def == null || string.IsNullOrEmpty(primary))
            {
                return false;
            }
            if (string.Equals(def.professionalSkillDefName, primary, StringComparison.Ordinal))
            {
                return true;
            }
            ProfessionalSkillDef skill = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(def.professionalSkillDefName);
            if (skill != null && string.Equals(skill.direction, primary, StringComparison.Ordinal))
            {
                return true;
            }
            ProfessionalDirectionDef dir = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(primary);
            if (dir != null && dir.skillDefNames != null)
            {
                for (int i = 0; i < dir.skillDefNames.Count; i++)
                {
                    if (string.Equals(dir.skillDefNames[i], def.professionalSkillDefName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static TitleBadgeInfo BestMedal(PawnObject pawnObj)
        {
            if (pawnObj == null || pawnObj.GrantedMedals == null || pawnObj.GrantedMedals.Count == 0)
            {
                return null;
            }
            MedalDef best = null;
            string bestName = null;
            for (int i = 0; i < pawnObj.GrantedMedals.Count; i++)
            {
                string name = pawnObj.GrantedMedals[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(name);
                if (def == null)
                {
                    continue;
                }
                if (best == null
                    || (int)def.tier > (int)best.tier
                    || ((int)def.tier == (int)best.tier && def.order > best.order))
                {
                    best = def;
                    bestName = name;
                }
            }
            return FromMedalDef(best, bestName);
        }

        private static TitleBadgeInfo FromTitleDef(ProfessionalTitleDef def)
        {
            if (def == null)
            {
                return null;
            }
            TitleBadgeInfo info = new TitleBadgeInfo();
            info.Title = TranslateOrFallback("PersonalChronicle.Professional.Title." + def.defName + ".Label", def.LabelCap);
            ProfessionalSkillDef skillDef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(def.professionalSkillDefName);
            // CHG-20261006-TRKEY · LOC-02：原先直取 skillDef.LabelCap，而 12 个 ProfessionalSkillDef
            // 的 Def 未写 label ⇒ 徽章方向位会显示裸 defName。改为与 342 行同口径：Keyed 优先、Def 兜底。
            info.Direction = skillDef != null
                ? TranslateOrFallback("PersonalChronicle.Professional.Skill." + skillDef.defName + ".Label", skillDef.LabelCap)
                : null;
            info.Tier = def.order;
            return info;
        }

        private static TitleBadgeInfo FromMedalDef(MedalDef def, string defName)
        {
            if (def == null && string.IsNullOrEmpty(defName))
            {
                return null;
            }
            string name = def != null ? def.defName : defName;
            TitleBadgeInfo info = new TitleBadgeInfo();
            string key = PersonalChronicle.Data.MedalTranslationKeys.Label(name);
            info.Title = TranslateOrFallback(key, def != null ? def.LabelCap : name);
            info.Direction = null;
            info.Tier = def != null ? (int)def.tier : 0;
            return info;
        }

        private static string TranslateOrFallback(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key) && key.CanTranslate())
            {
                return key.Translate();
            }
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }
    }
}
