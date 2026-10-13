using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// Partial of <see cref="WorkshopFloorPlan"/> —— **工序链路层**（对应预览 <c>buildRouteLayer</c>）。
    ///
    /// <para>预览元素与本实现一一对应：</para>
    /// <list type="bullet">
    /// <item><b>工序号角标</b>：设备右上 r8 圆 + 步号；</item>
    /// <item><b>工序连线</b>：`.route` 6px/18% 底色带 + `.route-flow` 2.2px dash 8/10 流动（颜色按流量）；</item>
    /// <item><b>线边超市</b>：段中点 36×18 框 + 「WIP N」；</item>
    /// <item><b>末端箭头 + 流向标注</b>：「⑫→库 N 件」；</item>
    /// <item><b>物料光点</b>：沿折线匀速循环（预览用 SVG <c>animateMotion</c>，这里用时间相位插值）；</item>
    /// <item><b>原料入线</b>：左墙 → 首道工序。</item>
    /// </list>
    ///
    /// <para>几何由**格序号 + <see cref="WorkshopFloorLayout"/>** 推导（数据层只给槽位与流量），
    /// 折线走格内"输送线高度"（格下部），因此不会穿过设备机箱。</para>
    /// </summary>
    internal static partial class WorkshopFloorPlan
    {
        /// <summary>折线在格内的纵向位置（格高比例，落在设备下方 = 输送线高度）。</summary>
        private const float RouteLineYRatio = 0.80f;

        /// <summary>线边超市框尺寸（设计单位，预览 <c>36×18</c>）。</summary>
        private const float WipBoxW = 36f;
        private const float WipBoxH = 18f;

        /// <summary>物料光点数量（预览 6 个）与半径（预览 r3.2）。</summary>
        private const int FlowDotCount = 6;
        private const float FlowDotRadius = 3.2f;

        private static void DrawRoutes(DrawContext ctx, WorkshopFloorSnapshot snapshot,
            float time, int activeRoute)
        {
            List<FloorRouteView> routes = snapshot.Routes;
            if (routes == null || routes.Count == 0)
            {
                return;
            }
            for (int i = 0; i < routes.Count; i++)
            {
                bool dim = activeRoute >= 0 && i != activeRoute;
                DrawRoute(ctx, routes[i], time, dim);
            }
        }

        private static void DrawRoute(DrawContext ctx, FloorRouteView route, float time, bool dim)
        {
            if (route?.Steps == null || route.Steps.Count < 2)
            {
                return;
            }
            Palette palette = ctx.Palette;

            // 1) 折线点：每道工序取「格中心线」上的一个点，相邻两点之间用直角肘连接
            List<Vector2> path = BuildPath(route);
            if (path.Count < 2)
            {
                return;
            }

            // 2) 逐段画：底色带 + 流动虚线（颜色按该段流量）
            for (int i = 0; i < route.Steps.Count - 1 && i * 3 + 3 < path.Count + 1; i++)
            {
                FloorRouteStepView step = route.Steps[i + 1];
                Color color = FlowColor(step.FlowRatio01, palette, dim);
                int baseIndex = i * 3;
                if (baseIndex + 2 >= path.Count)
                {
                    break;
                }
                DrawPolyline(ctx, path, baseIndex, 3, color, Mathf.Max(2f, 6f * ctx.K), 0.18f);
                DrawFlowingPolyline(ctx, path, baseIndex, 3, color,
                    Mathf.Max(1.5f, 2.2f * ctx.K), Mathf.Max(2f, 8f * ctx.K), Mathf.Max(2f, 10f * ctx.K), time);
            }

            // 3) 工序号角标 + 线边超市（WIP）
            float badgeRadius = S(8f, ctx.K);
            for (int i = 0; i < route.Steps.Count; i++)
            {
                FloorRouteStepView step = route.Steps[i];
                if (i + 1 < route.Steps.Count)
                {
                    DrawWipBox(ctx, route.Steps[i], route.Steps[i + 1], palette, dim);
                }
                if (badgeRadius < 5f)
                {
                    continue;
                }
                Vector2 badge = BadgeCenter(ctx, step.Slot);
                Color badgeColor = dim ? UITheme.WithAlpha(palette.Busbar, 0.3f) : palette.Busbar;
                DrawDisc(badge, badgeRadius, badgeColor);
                if (badgeRadius >= 6.5f)
                {
                    UIComponents.Label(new Rect(badge.x - badgeRadius, badge.y - badgeRadius * 0.62f,
                            badgeRadius * 2f, badgeRadius * 1.4f),
                        step.StepText, GameFont.Tiny, palette.BayBadgeText, TextAnchor.MiddleCenter);
                }
            }

            // 4) 末端箭头 + 流向标注
            Vector2 end = path[path.Count - 1];
            Vector2 beforeEnd = path[path.Count - 2];
            Vector2 direction = (end - beforeEnd).sqrMagnitude > 0.01f ? (end - beforeEnd).normalized : Vector2.right;
            Color endColor = FlowColor(route.EndFlowCount > 0 ? 1f : 0f, palette, dim);
            float arrow = S(9f, ctx.K);
            Vector2 tip = end + direction * arrow * 1.6f;
            Widgets.DrawLine(tip, end + Rotate(direction, 140f) * arrow, endColor, Mathf.Max(1.5f, 2f * ctx.K));
            Widgets.DrawLine(tip, end + Rotate(direction, -140f) * arrow, endColor, Mathf.Max(1.5f, 2f * ctx.K));
            if (arrow >= 6f && !string.IsNullOrEmpty(route.EndText))
            {
                Rect label = new Rect(end.x - 150f, end.y - S(30f, ctx.K), 150f, 15f);
                UIComponents.Label(label, UIComponents.TruncateToWidth(route.EndText, label.width, GameFont.Tiny),
                    GameFont.Tiny, dim ? UITheme.WithAlpha(palette.TextDim, 0.4f) : endColor, TextAnchor.MiddleRight);
            }

            // 5) 原料入线（左墙 → 首道工序）
            if (route.HasFeed)
            {
                Vector2 first = path[0];
                Vector2 from = new Vector2(ctx.Canvas.x + WorkshopFloorLayout.Bx * ctx.K, first.y);
                Color feed = dim ? UITheme.WithAlpha(palette.TextDim, 0.25f) : UITheme.WithAlpha(palette.SupLine, 0.8f);
                Widgets.DrawLine(from, first, feed, Mathf.Max(1f, 1.6f * ctx.K));
            }

            // 6) 物料光点：沿折线匀速（相位 = 时间 / 周期）
            float cycle = Mathf.Max(0.5f, route.CycleSeconds);
            float phase = Mathf.Repeat(time / cycle, 1f);
            float totalLength = PolylineLength(path);
            if (totalLength <= 1f)
            {
                return;
            }
            for (int d = 0; d < FlowDotCount; d++)
            {
                float t = Mathf.Repeat(phase + d / (float)FlowDotCount, 1f);
                Vector2 dot = PointAt(path, t * totalLength);
                DrawDisc(dot, S(FlowDotRadius, ctx.K),
                    dim ? UITheme.WithAlpha(palette.Busbar, 0.25f) : palette.Busbar);
            }
        }

        /// <summary>折线点：每道工序 = 格中心线上的点；相邻点之间插一个直角肘（同列/同行时省掉）。</summary>
        private static List<Vector2> BuildPath(FloorRouteView route)
        {
            List<Vector2> path = new List<Vector2>(route.Steps.Count * 3);
            Vector2? previous = null;
            for (int i = 0; i < route.Steps.Count; i++)
            {
                Rect bay = WorkshopFloorLayout.BayRect(route.Steps[i].Slot);
                Vector2 point = new Vector2(bay.x + bay.width * 0.5f, bay.y + bay.height * RouteLineYRatio);
                if (previous.HasValue)
                {
                    Vector2 from = previous.Value;
                    if (!Mathf.Approximately(from.y, point.y) && !Mathf.Approximately(from.x, point.x))
                    {
                        path.Add(new Vector2(point.x, from.y)); // 直角肘：先横后竖
                    }
                }
                path.Add(point);
                previous = point;
            }
            return path;
        }

        /// <summary>角标位置：该格设备机箱右上（预览 <c>m._x+MW-9, m._y+9</c>）。</summary>
        private static Vector2 BadgeCenter(DrawContext ctx, int slot)
        {
            Rect machine = WorkshopFloorLayout.MachineRect(slot, 0);
            return P(ctx, machine.xMax - WorkshopFloorLayout.RouteBadgeInset,
                machine.y + WorkshopFloorLayout.RouteBadgeInset);
        }

        /// <summary>线边超市框：画在相邻两道工序的中点。</summary>
        private static void DrawWipBox(DrawContext ctx, FloorRouteStepView from, FloorRouteStepView to,
            Palette palette, bool dim)
        {
            Rect bayA = WorkshopFloorLayout.BayRect(from.Slot);
            Rect bayB = WorkshopFloorLayout.BayRect(to.Slot);
            float midX = (bayA.x + bayA.width * 0.5f + bayB.x + bayB.width * 0.5f) * 0.5f;
            float midY = (bayA.y + bayB.y) * 0.5f + bayA.height * RouteLineYRatio;
            if (from.WipCount <= 0 && from.FlowCount <= 0)
            {
                return; // 该工序没在制也没产出 → 不摆空箱子（LAND-009：不画无意义装饰）
            }

            Rect box = R(ctx.Canvas, ctx.K, midX - WipBoxW * 0.5f, midY - WipBoxH * 0.5f, WipBoxW, WipBoxH);
            if (box.width < 14f)
            {
                return;
            }
            Color fill = dim ? UITheme.WithAlpha(palette.LegendFill, 0.4f) : palette.LegendFill;
            Color line = dim ? UITheme.WithAlpha(palette.LegendLine, 0.4f) : palette.LegendLine;
            Widgets.DrawBoxSolid(box, fill);
            UIComponents.Border(box, line);
            if (box.height >= ctx.TinyLine - 2f)
            {
                UIComponents.Label(box, T1("PersonalChronicle.UI.Floor.Route.Wip", from.WipCount),
                    GameFont.Tiny, dim ? UITheme.WithAlpha(palette.TextDim, 0.5f) : palette.TextDim,
                    TextAnchor.MiddleCenter);
            }
        }

        // ---- 折线绘制原语 ----

        private static void DrawPolyline(DrawContext ctx, List<Vector2> designPoints, int start, int count,
            Color color, float width, float alpha)
        {
            Color tinted = UITheme.WithAlpha(color, alpha);
            for (int i = 0; i < count - 1; i++)
            {
                int index = start + i;
                if (index + 1 >= designPoints.Count || index >= designPoints.Count)
                {
                    break;
                }
                Widgets.DrawLine(P(ctx, designPoints[index]), P(ctx, designPoints[index + 1]), tinted, width);
            }
        }

        private static void DrawFlowingPolyline(DrawContext ctx, List<Vector2> designPoints, int start, int count,
            Color color, float width, float dash, float gap, float time)
        {
            // 相位随时间偏移，形成"流动"观感（预览用 CSS stroke-dashoffset 动画）
            float offset = Mathf.Repeat(time * dash * 1.4f, dash + gap);
            for (int i = 0; i < count - 1; i++)
            {
                int index = start + i;
                if (index + 1 >= designPoints.Count)
                {
                    break;
                }
                Vector2 a = P(ctx, designPoints[index]);
                Vector2 b = P(ctx, designPoints[index + 1]);
                DrawDash(a, b, color, width, dash, gap, offset);
            }
        }

        private static void DrawDash(Vector2 a, Vector2 b, Color color, float width, float dash, float gap,
            float offset)
        {
            float total = Vector2.Distance(a, b);
            if (total <= 0.5f)
            {
                return;
            }
            Vector2 direction = (b - a) / total;
            float step = dash + gap;
            for (float position = -offset; position < total; position += step)
            {
                float from = Mathf.Max(0f, position);
                float to = Mathf.Min(total, position + dash);
                if (to - from <= 0.5f)
                {
                    continue;
                }
                Widgets.DrawLine(a + direction * from, a + direction * to, color, width);
            }
        }

        private static float PolylineLength(List<Vector2> points)
        {
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                length += Vector2.Distance(points[i], points[i + 1]);
            }
            return length;
        }

        /// <summary>折线上按弧长取点（物料光点用）。</summary>
        private static Vector2 PointAt(List<Vector2> points, float distance)
        {
            float remaining = distance;
            for (int i = 0; i < points.Count - 1; i++)
            {
                float segment = Vector2.Distance(points[i], points[i + 1]);
                if (remaining <= segment)
                {
                    float t = segment <= 0.01f ? 0f : remaining / segment;
                    return Vector2.Lerp(points[i], points[i + 1], t);
                }
                remaining -= segment;
            }
            return points[points.Count - 1];
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>预览 <c>flowColor</c>：流量低=信息色、高=健康色（令牌插值，不写死色值）。</summary>
        private static Color FlowColor(float ratio, Palette palette, bool dim)
        {
            Color color = Color.Lerp(UITheme.Info, UITheme.Alive, Mathf.Clamp01(ratio));
            return dim ? UITheme.WithAlpha(color, 0.18f) : color;
        }

        /// <summary>设计坐标（Vector2）→ 屏幕坐标。</summary>
        private static Vector2 P(DrawContext ctx, Vector2 design)
        {
            return P(ctx.Canvas, ctx.K, design.x, design.y);
        }

        /// <summary>
        /// 单参格式化翻译（**本 partial 类共用一份**：链路图与两侧轨道都调它）。
        /// 1.6 必须走 <c>NamedArgument</c> 重载 —— 传 <c>object</c> 会落到已过时的
        /// <c>params object[]</c> 版本（CS0618，本工程已踩过的坑）。
        /// </summary>
        private static string T1(string key, int arg)
        {
            return key.Translate((NamedArgument)arg).ToString();
        }

        // 线尾控制台（`S-00`）绘制**已删除**（2026-09-11 基線 §5c-B/D13）：
        // 建筑 `PC_ControlTerminal` 本 mod 尚未落地，预览却给了会亮的灯 + 状态（冒充数据），
        // 且文案硬编码「线尾控制台」≠ def label「管控终端」。落地后由真实 Thing 驱动再画。
    }
}
