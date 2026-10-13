using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 农机改造地格的<b>全图归属登记表</b>（cell → 划区前原始地形）。
    ///
    /// 解决多台农机覆盖圈交叠时的地形冲突：同一格只允许被<b>先改造</b>的农机认领
    /// （登记原始地形），其它农机一律让位——否则两机交替覆写会污染还原链
    /// （B 记到的"原地形"其实是 A 铺的，A 撤机后该格永远回不到真正的原始地面，
    /// 甚至出现两台机各持一份"原值"、撤机顺序不同结果不同的隐性行为）。
    ///
    /// 登记表跨存档持久化；旧存档迁移由各农机在 PostSpawnSetup(respawn) 时
    /// 把自身已有改造记录补登记（先到先得，一台一格）。
    /// </summary>
    public class MapComponent_AutoFarmTerrain : MapComponent
    {
        private const string SaveKey = "PC_AutoFarmTerrainOriginals";

        private Dictionary<IntVec3, TerrainDef> originals = new Dictionary<IntVec3, TerrainDef>();

        /// <summary>
        /// 认领方（cell → thingIDNumber）。原先只有 originals，无法区分"谁认领"，
        /// 导致：①本机已登记过的格子（idx>=0 分支）跳过让位判断，旧档多机重复登记时
        /// 后手机会覆写先手机铺的地形；②撤机时无条件 Release，可能释放掉他机的认领，
        /// 让他机后续还原读到脏值。加归属表后两个问题一并根治。
        /// </summary>
        private Dictionary<IntVec3, int> owners = new Dictionary<IntVec3, int>();

        public MapComponent_AutoFarmTerrain(Map map) : base(map)
        {
        }

        public static MapComponent_AutoFarmTerrain Get(Map map)
        {
            MapComponent_AutoFarmTerrain comp = map.GetComponent<MapComponent_AutoFarmTerrain>();
            if (comp == null)
            {
                comp = new MapComponent_AutoFarmTerrain(map);
                map.components.Add(comp);
            }
            return comp;
        }

        /// <summary>该格是否已被任一农机认领。</summary>
        public bool IsClaimed(IntVec3 cell) => originals.ContainsKey(cell);

        /// <summary>该格登记的原始地形（未登记返回 null）。</summary>
        public TerrainDef OriginalAt(IntVec3 cell) => originals.TryGetValue(cell, out TerrainDef t) ? t : null;

        /// <summary>先到先得：已被登记的格不再改写原始地形记录。</summary>
        public void TryRecord(IntVec3 cell, TerrainDef original, Thing owner = null)
        {
            if (!originals.ContainsKey(cell))
            {
                originals.Add(cell, original);
                owners[cell] = owner != null ? owner.thingIDNumber : 0;
            }
        }

        /// <summary>该格是否已被<b>别的</b>设备认领（自己认领的返回 false）。</summary>
        public bool IsClaimedByOther(IntVec3 cell, Thing self)
        {
            if (!originals.ContainsKey(cell))
            {
                return false;
            }
            int selfId = self != null ? self.thingIDNumber : 0;
            return owners.TryGetValue(cell, out int id) && id != selfId;
        }

        /// <summary>只释放<b>本机</b>的认领，绝不释放他机的（旧档重复登记时尤要）。</summary>
        public void Release(IntVec3 cell, Thing owner = null)
        {
            if (!originals.ContainsKey(cell))
            {
                return;
            }
            int selfId = owner != null ? owner.thingIDNumber : 0;
            if (owners.TryGetValue(cell, out int id) && id != selfId)
            {
                return; // 他机认领：留给对方自己释放。
            }
            originals.Remove(cell);
            owners.Remove(cell);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // IntVec3 是值类型 → Value；TerrainDef 是 Def → Def（存 defName，读档跨引用解析）。
            // ⭐ 原先 LookMode.Reference 对 Def 元素保存必炸（非 ILoadReferenceable），
            // MapComponent 炸会让整张地图保存失败——同一颗雷（2026-09-08 定案）。
            Scribe_Collections.Look(ref originals, SaveKey, LookMode.Value, LookMode.Def);
            Scribe_Collections.Look(ref owners, SaveKey + "_owners", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (originals == null)
                {
                    originals = new Dictionary<IntVec3, TerrainDef>();
                }
                if (owners == null)
                {
                    owners = new Dictionary<IntVec3, int>();
                }
            }
        }
    }
}
