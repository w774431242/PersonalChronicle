using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>固定布局与自由画布共用图像/等级栏；按资源路径缓存，不逐帧查找资产。</summary>
    [StaticConstructorOnStartup]
    internal static class ProductionDeviceCardArt
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <returns>已占用的左侧宽度；空间不足时返回 0，完整信息仍由悬停提示提供。</returns>
        internal static float Draw(Rect body, ProductionDeviceCardView card, float belowLamp)
        {
            if (card == null || body.width < 132f || body.height < 76f) return 0f;
            float column = Mathf.Clamp(body.width * 0.19f, 26f, 64f);
            float line = Text.LineHeightOf(GameFont.Tiny);
            float top = body.y + Mathf.Max(22f, belowLamp);
            // 预留底部进度条；图像与徽标均不得超出卡片。
            float available = body.yMax - 14f - top - line;
            if (available < 12f) return 0f;
            Rect art = new Rect(body.x + 4f, top, column, Mathf.Min(column * 0.60f, available));
            if (!Textures.TryGetValue(card.TexturePath, out Texture2D texture))
            {
                texture = ContentFinder<Texture2D>.Get(card.TexturePath, false);
                Textures[card.TexturePath] = texture;
            }
            if (texture != null)
            {
                Color previous = GUI.color;
                try
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(art, texture, ScaleMode.ScaleToFit, true);
                }
                finally { GUI.color = previous; }
            }
            Rect badge = new Rect(art.x, art.yMax + 1f, column, line);
            Widgets.DrawBoxSolid(badge, UITheme.WithAlpha(UITheme.Info, 0.16f));
            UIComponents.Label(badge, card.TierText, GameFont.Tiny, UITheme.Info, TextAnchor.MiddleCenter);
            return column + 8f;
        }
    }
}
