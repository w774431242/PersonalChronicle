using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing
{
    /// <summary>
    /// 车间俯视框架用的**运行期生成贴图**（3 张，懒加载静态缓存）。
    ///
    /// <para>预览用 CSS <c>pattern</c>（网格底纹 56×56、警示斜纹 14×14 rotate45）与 SVG <c>circle</c>；
    /// IMGUI 没有 pattern，因此按预览参数在运行期烘一次小图，之后用
    /// <c>GUI.DrawTextureWithTexCoords</c>（Repeat）平铺 / <c>GUI.DrawTexture</c>（圆）复用 —— 每帧零生成（PERF-001）。</para>
    ///
    /// <para>网格图烘成**不透明白线**，用 <c>GUI.color</c> 连同透明度一起染色（主题切换无需重建）；
    /// 警示纹与圆点是"双色/实心"图形，按真实颜色烘死，靠 <c>GUI.color</c> 的 alpha 控制强度。</para>
    ///
    /// <para><b>[StaticConstructorOnStartup]</b>（2026-10-14 清理）：本类持有
    /// <c>Texture2D</c> 静态字段，Harmony/RimWorld 要求所有资产在<b>主线程</b>加载，
    /// 否则 Player.log 会刷
    /// "Type WorkshopFloorTextures probably needs a StaticConstructorOnStartup attribute"。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WorkshopFloorTextures
    {
        /// <summary>警示斜纹：黄（预览 <c>#c9a52c</c>）。</summary>
        private static readonly Color HazardYellow = new Color(0.788f, 0.647f, 0.173f, 1f);

        /// <summary>警示斜纹：黑（预览 <c>#181818</c>）。</summary>
        private static readonly Color HazardBlack = new Color(0.094f, 0.094f, 0.094f, 1f);

        private static Texture2D grid;
        private static Texture2D hazard;
        private static Texture2D disc;

        /// <summary>56×56 网格底纹（1px 白线，上下边 + 左边，Repeat）。</summary>
        internal static Texture2D Grid
        {
            get { return grid ?? (grid = BuildGrid()); }
        }

        /// <summary>14×14 45° 警示斜纹（黄黑各 7px，Repeat）。</summary>
        internal static Texture2D Hazard
        {
            get { return hazard ?? (hazard = BuildHazard()); }
        }

        /// <summary>64×64 白色圆盘（用 <c>GUI.color</c> 染色，供圆点/圆灯/编号底）。</summary>
        internal static Texture2D Disc
        {
            get { return disc ?? (disc = BuildDisc()); }
        }

        private static Texture2D NewTile(int width, int height, TextureWrapMode wrap)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        private static Texture2D BuildGrid()
        {
            int size = Mathf.RoundToInt(WorkshopFloorLayout.GridStep);
            Texture2D texture = NewTile(size, size, TextureWrapMode.Repeat);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = x == 0 || y == 0;
                    pixels[y * size + x] = onLine ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildHazard()
        {
            int size = Mathf.RoundToInt(WorkshopFloorLayout.HazardStep);
            float half = WorkshopFloorLayout.HazardHalf;
            Texture2D texture = NewTile(size, size, TextureWrapMode.Repeat);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // CSS: pattern rotate(45) ⇒ (x + y) 落在半周期内为黄，其余为黑。
                    bool yellow = (x + y) % size < half;
                    pixels[y * size + x] = yellow ? HazardYellow : HazardBlack;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildDisc()
        {
            const int size = 64;
            float radius = size * 0.5f - 1f;
            float center = size * 0.5f;
            Texture2D texture = NewTile(size, size, TextureWrapMode.Clamp);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // 1px 软边，避免小尺寸下锯齿
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
