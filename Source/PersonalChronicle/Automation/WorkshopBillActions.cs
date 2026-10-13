using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 设备弹窗 ⑦「生产列表」的**写操作**（预览 §5c-Q L15 的落地）。
    ///
    /// <para><b>全部映射原版语义</b>（ilspycmd 实证 <c>RimWorld.BillStack</c>）：
    /// 调序 = <c>BillStack.Reorder(bill, ±1)</c>；挂起 = <c>Bill.suspended</c>（可写）；
    /// 移出 = <c>BillStack.Delete(bill)</c>；追加 = <c>BillStack.AddBill(new Bill_Production(recipe))</c>。
    /// **不引入自建模型**，也不写存档（工单本就随工作台存档）。</para>
    ///
    /// <para><b>定位方式</b>：UI 只传**稳定 ID**（<c>&lt;defName&gt;:&lt;thingIDNumber&gt;</c>，GOV-009），
    /// 本类在地图上按 ID 找 <c>Thing</c> —— 避免把 <c>Thing</c> 引用塞进只读快照（跨层/跨档不安全）。</para>
    /// </summary>
    public static class WorkshopBillActions
    {
        public const int ActMoveUp = 1;
        public const int ActMoveDown = 2;
        public const int ActToggleSuspend = 3;
        public const int ActDelete = 4;

        /// <summary>按稳定 ID 在地图上找 Thing（点击时才调用，非热路径）。</summary>
        public static Thing FindByStableId(Map map, string stableId)
        {
            if (map == null || string.IsNullOrEmpty(stableId))
            {
                return null;
            }
            int colon = stableId.IndexOf(':');
            if (colon <= 0)
            {
                return null;
            }
            string defName = stableId.Substring(0, colon);
            if (!int.TryParse(stableId.Substring(colon + 1), out int thingId))
            {
                return null;
            }
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.def != null && thing.thingIDNumber == thingId
                    && thing.def.defName == defName)
                {
                    return thing;
                }
            }
            return null;
        }

        /// <summary>可加单的配方（= 该建筑 ThingDef 的 <c>AllRecipes</c>；原版 Bill 加单同源）。</summary>
        public static void CollectRecipes(Thing bench, List<RecipeDef> into)
        {
            into.Clear();
            if (bench == null || bench.def == null || bench.def.AllRecipes == null)
            {
                return;
            }
            List<RecipeDef> all = bench.def.AllRecipes;
            for (int i = 0; i < all.Count; i++)
            {
                RecipeDef recipe = all[i];
                // 只列**有产物**的配方（拆解/卸载类原版配方无产物，加单无意义）
                if (recipe != null && recipe.products != null && recipe.products.Count > 0)
                {
                    into.Add(recipe);
                }
            }
        }

        /// <summary>对第 <paramref name="index"/> 条工单执行动作（调序 / 挂起 / 移出）。</summary>
        public static bool TryApply(string stableId, int action, int index, out string message)
        {
            message = null;
            Map map = Find.CurrentMap;
            Thing bench = FindByStableId(map, stableId);
            if (bench == null || !(bench is IBillGiver giver) || giver.BillStack == null)
            {
                message = "PersonalChronicle.UI.Floor.Detail.Queue.NoBench".Translate().ToString();
                return false;
            }
            BillStack stack = giver.BillStack;
            if (index < 0 || index >= stack.Count)
            {
                return false;
            }
            Bill bill = stack[index];
            switch (action)
            {
                case ActMoveUp:
                    if (index > 0)
                    {
                        stack.Reorder(bill, -1);
                    }
                    break;
                case ActMoveDown:
                    if (index < stack.Count - 1)
                    {
                        stack.Reorder(bill, 1);
                    }
                    break;
                case ActToggleSuspend:
                    bill.suspended = !bill.suspended;
                    break;
                case ActDelete:
                    stack.Delete(bill);
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>追加一条 <c>Bill_Production</c>（Forever 语义；保留旧签名以免破坏既有调用点）。</summary>
        public static bool TryAddBill(string stableId, int recipeIndex, out string message)
        {
            return TryAddBill(stableId, recipeIndex, 0, out message);
        }

        /// <summary>
        /// 追加一条 <c>Bill_Production</c>（批次 5：支持**指定数量**）。
        ///
        /// <para><paramref name="count"/> &gt; 0 ⇒ <c>RepeatCount</c> 模式 + <c>targetCount = count</c>
        /// （"做 N 件就停"，与原版工单配置页的"重复次数"一致）；否则 <c>Forever</c>（原版加单默认）。</para>
        /// </summary>
        public static bool TryAddBill(string stableId, int recipeIndex, int count, out string message)
        {
            message = null;
            Map map = Find.CurrentMap;
            Thing bench = FindByStableId(map, stableId);
            if (bench == null || !(bench is IBillGiver giver) || giver.BillStack == null)
            {
                message = "PersonalChronicle.UI.Floor.Detail.Queue.NoBench".Translate().ToString();
                return false;
            }
            List<RecipeDef> recipes = new List<RecipeDef>();
            CollectRecipes(bench, recipes);
            if (recipeIndex < 0 || recipeIndex >= recipes.Count)
            {
                return false;
            }
            BillStack stack = giver.BillStack;
            if (stack.Count >= BillStack.MaxCount)
            {
                message = "PersonalChronicle.UI.Floor.Detail.Queue.Full".Translate(
                    (NamedArgument)BillStack.MaxCount).ToString();
                return false;
            }
            Bill_Production bill = new Bill_Production(recipes[recipeIndex]);
            if (count > 0)
            {
                // 批次 5（2026-09-23）：指定数量 ⇒ `RepeatCount` 模式 + `targetCount`（"做 N 件就停"，
                //   与原版工单配置页的"重复次数"语义一致）。
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.targetCount = count;
            }
            else
            {
                bill.repeatMode = BillRepeatModeDefOf.Forever;
            }
            stack.AddBill(bill);
            return true;
        }

        /// <summary>
        /// M10 再生产（2026-10-10）：在**指定工作台**上按配方追加一条带数量约束的 bill，并把新 bill 交给调用方
        /// （授权账需要它的稳定键来跟踪真实交付）。与 UI 版 <see cref="TryAddBill(string,int,int,out string)"/> 的差别仅在：
        /// 目标由调用方显式给出（UI 版依赖 <c>Find.CurrentMap</c>），且返回新 bill（UI 版不需要回执）。
        /// 仍只走原版 <c>BillStack.AddBill</c> —— 无第二个 runner，工单照常随工作台存档。
        /// </summary>
        public static Bill_Production TryAddBillOn(Thing bench, RecipeDef recipe, int count, out string message)
        {
            message = null;
            if (bench == null || recipe == null)
            {
                message = "InvalidTarget";
                return null;
            }
            if (!(bench is IBillGiver giver) || giver.BillStack == null)
            {
                message = "NoBillStack";
                return null;
            }
            BillStack stack = giver.BillStack;
            if (stack.Count >= BillStack.MaxCount)
            {
                message = "BillStackFull";
                return null;
            }
            Bill_Production bill = new Bill_Production(recipe);
            if (count > 0)
            {
                // 与 UI 版同语义：指定数量 ⇒ RepeatCount + targetCount（"做 N 件就停"）。
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.targetCount = count;
            }
            else
            {
                bill.repeatMode = BillRepeatModeDefOf.Forever;
            }
            stack.AddBill(bill);
            return bill;
        }

        /// <summary>
        /// 派工 / 取消派工（S3-5，2026-09-23）：把 <paramref name="pawnThingId"/> 指定为第
        /// <paramref name="billIndex"/> 条工单的生产人员；<c>pawnThingId &lt;= 0</c> ⇒ **取消指定**。
        ///
        /// <para><b>只走原版公开入口</b> <c>RimWorld.Bill.SetPawnRestriction(Pawn)</c>
        /// —— 已 Cecil 核实（Assembly-CSharp，Build 1.6.4871 rev590）：</para>
        /// <list type="bullet">
        /// <item>签名 <c>public void SetPawnRestriction(Pawn pawn)</c>（属性 <c>PawnRestriction</c>
        /// **只读**，私有字段 <c>pawnRestriction</c> 不可直写）；</item>
        /// <item>IL = <c>pawnRestriction = pawn</c> + 清空 <c>slavesOnly</c> / <c>mechsOnly</c> /
        /// <c>nonMechsOnly</c> —— 这是**原版互斥语义**（指定具体工人即撤销那三类过滤器），照用不改；</item>
        /// <item>调用者 = <c>BillDialogUtility.GetPawnRestrictionOptionsForBill</c> 的选项回调
        /// ⇒ **原版工单配置页的「工人」下拉走的就是这条路径**（活入口，非死钩子）。</item>
        /// </list>
        /// <para>不写存档（工单随工作台存档，与原版一致）。</para>
        /// </summary>
        public static bool TrySetPawnRestriction(string stableId, int billIndex, int pawnThingId,
            out string message)
        {
            message = null;
            Map map = Find.CurrentMap;
            Thing bench = FindByStableId(map, stableId);
            if (bench == null || !(bench is IBillGiver giver) || giver.BillStack == null)
            {
                message = "PersonalChronicle.UI.Floor.Detail.Queue.NoBench".Translate().ToString();
                return false;
            }
            BillStack stack = giver.BillStack;
            if (billIndex < 0 || billIndex >= stack.Count)
            {
                return false;
            }
            Pawn pawn = pawnThingId > 0 ? WorkshopPawnAccess.FindByThingId(map, pawnThingId) : null;
            if (pawnThingId > 0 && pawn == null)
            {
                // 人已不在地图上（死亡 / 离图）：不写入，避免把失效 Pawn 存进工单
                return false;
            }
            stack[billIndex].SetPawnRestriction(pawn);
            return true;
        }
    }
}
