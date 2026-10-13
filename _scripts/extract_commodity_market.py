# -*- coding: utf-8 -*-
"""
原版商品品种全集取证（MVS 预览页数据源）
扫 Core + 全部 DLC 的 ThingDef / ThingCategoryDef，按顶层品类聚合出可用于
「市场面板 · 双进度条」的品种清单。

关键点：
  1) 继承归并 —— ParentName 指向 Name（不是 defName），正则匹配 Name= 必须避开 ParentName=，
     这里改用 ElementTree 读属性，天然规避该陷阱。
  2) 可交易判据 —— 与原版 TradeUtility 语义对齐的近似：非 abstract、有 defName、
     tradeability != Never、非建筑蓝图/未完成品、MarketValue > 0。
  3) 输出：每个顶层品类 -> ThingDef 数 / 平均 MarketValue / 覆盖 DLC
"""
import os, sys, json, re
from collections import defaultdict

ROOT = r"E:/SteamLibrary/steamapps/common/RimWorld/Data"
PACKS = ["Core", "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"]

try:
    import xml.etree.ElementTree as ET
except Exception:
    print("no xml lib"); sys.exit(1)


def strip(tag):
    return tag.split('}')[-1] if '}' in tag else tag


def read_defs(path):
    """返回 (tag, attrs, root_element) 列表"""
    try:
        tree = ET.parse(path)
    except Exception as e:
        return []
    out = []
    root = tree.getroot()
    for el in list(root):
        out.append((strip(el.tag), dict(el.attrib), el))
    return out


def find_all(files):
    for dirpath, _, names in os.walk(files):
        for n in names:
            if n.lower().endswith(".xml"):
                yield os.path.join(dirpath, n)


# ── 1. ThingCategoryDef 树 ─────────────────────────────────────────
cat_by_name = {}      # Name -> {defName, parent, label, pack}
cat_by_defname = {}
for pack in PACKS:
    p = os.path.join(ROOT, pack)
    if not os.path.isdir(p):
        continue
    for fp in find_all(p):
        for tag, at, el in read_defs(fp):
            if tag == "ThingCategoryDef":
                nm = at.get("Name")
                cdfn = (el.findtext("defName") or "").strip() or None
                key = nm or cdfn
                cat_by_name[key] = {
                    "defName": cdfn,
                    # ThingCategoryDef 既可能是 ParentName 属性，也可能用 <parent> 子元素
                    "parent": at.get("ParentName") or ((el.findtext("parent") or "").strip() or None),
                    "label": (el.findtext("label") or "").strip() if el is not None else "",
                    "pack": pack,
                }
                if cdfn:
                    cat_by_defname[cdfn] = cat_by_name[key]

# defName -> 顶层品类（沿 parent 上溯）
top_cache = {}


def path_of(key, depth=0):
    """返回从【直接品类】到 Root 的完整路径（含自身）"""
    out = []
    cur = cat_by_name.get(key)
    while cur is not None and depth < 20:
        out.append(cur.get("defName") or "?")
        p = cur.get("parent")
        if not p:
            break
        cur = cat_by_name.get(p)
        depth += 1
    return out


def top_of(key, depth=0):
    if key in top_cache:
        return top_cache[key]
    d = cat_by_name.get(key)
    if d is None or depth > 20:
        return None
    cur = d
    # 上溯到 Root 的【直接子类】为止 —— 那才是对玩家有意义的品类层级
    while (cur.get("parent") and cur["parent"] != "Root"
           and cat_by_name.get(cur["parent"])):
        cur = cat_by_name[cur["parent"]]
    top_cache[key] = cur
    return cur


# ── 2. ThingDef 收集（含继承归并）──────────────────────────────────
thing_by_name = {}
for pack in PACKS:
    p = os.path.join(ROOT, pack)
    if not os.path.isdir(p):
        continue
    for fp in find_all(p):
        for tag, at, el in read_defs(fp):
            if tag != "ThingDef":
                continue
            # 关键陷阱：RimWorld 的 XML 里 defName 是【子元素】，Name / ParentName 才是【属性】
            dfn = (el.findtext("defName") or "").strip() or None
            key = at.get("Name") or dfn
            if not key:
                continue
            cats = [x.text.strip() for x in el.findall("thingCategories/li") if x.text]
            stuff = [x.text.strip() for x in el.findall("stuffCategories/li") if x.text]
            mv = None
            sb = el.find("statBases/MarketValue")
            if sb is not None and sb.text:
                try:
                    mv = float(sb.text.strip())
                except ValueError:
                    mv = None
            abst = el.find("abstract")
            abst = True if (abst is not None and (abst.text or "").strip().lower() == "true") else False
            out = {
                "parent": at.get("ParentName"),
                "defName": dfn,
                "pack": pack,
                "cats": cats,
                "stuff": stuff,
                "marketValue": mv,
                "abstract": abst,
                "tradeability": (el.findtext("tradeability") or "").strip() or None,
                "stackLimit": (el.findtext("stackLimit") or "").strip() or None,
                "mass": (el.findtext("mass") or "").strip() or None,
                "file": os.path.basename(fp),
            }
            thing_by_name[key] = out


def merged(key, depth=0):
    """沿 ParentName 归并（子覆盖父）"""
    d = thing_by_name.get(key)
    if d is None or depth > 20:
        return d
    if "_merged" in d:
        return d["_merged"]
    base = {}
    p = d.get("parent")
    if p and p in thing_by_name:
        base = dict(merged(p, depth + 1) or {})
    m = dict(base)
    for k, v in d.items():
        if k in ("cats", "stuff"):
            if v:
                m[k] = v
        elif v not in (None, ""):
            m[k] = v
    m["_merged"] = None
    d["_merged"] = m
    return m


# ── 3. 可交易判定 + 聚合 ──────────────────────────────────────────
BAN_SUFFIX = ("Blueprint", "Frame", "Corpse", "Skeleton", "Mote", "Filth", "Sparks")

rows = []
for key, raw in thing_by_name.items():
    m = merged(key)
    if not m:
        continue
    if m.get("abstract"):
        continue
    defn = m.get("defName")
    if not defn:
        continue
    if defn.endswith(BAN_SUFFIX):
        continue
    if (m.get("tradeability") or "").lower() == "never":
        continue
    cats = m.get("cats") or []
    if not cats:
        continue
    mv = m.get("marketValue")
    if mv is None:
        mv = 0.0
    if mv <= 0:
        continue
    tops = []
    for c in cats:
        t = top_of(c)
        if t and t.get("defName"):
            tops.append(t["defName"])
    if not tops:
        tops = [cats[0]]
    rows.append({"defName": defn, "pack": m.get("pack"), "top": tops[0],
                 "marketValue": mv, "cats": cats,
                 "path": path_of(cats[0])})

agg = defaultdict(lambda: {"n": 0, "mv_sum": 0.0, "packs": set(), "sample": []})
for r in rows:
    a = agg[r["top"]]
    a["n"] += 1
    a["mv_sum"] += r["marketValue"]
    a["packs"].add(r["pack"])
    if len(a["sample"]) < 8:
        a["sample"].append((r["defName"], r["marketValue"]))

result = []
for k, v in sorted(agg.items(), key=lambda x: -x[1]["n"]):
    result.append({
        "category": k,
        "label": (cat_by_defname.get(k) or {}).get("label", ""),
        "thingDefCount": v["n"],
        "avgMarketValue": round(v["mv_sum"] / v["n"], 2),
        "packs": sorted(v["packs"]),
        "samples": v["sample"],
    })

out = {
    "totalTradeableThingDefs": len(rows),
    "topCategories": len(result),
    "categories": result,
    "allRows": [{"defName": r["defName"], "top": r["top"], "mv": r["marketValue"],
                 "pack": r["pack"], "cats": r["cats"], "path": r["path"]} for r in rows],
}
with open(r"E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/_scripts/_commodity_market.json",
          "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)

print("total tradeable:", len(rows), "| top categories:", len(result))
for r in result:
    print(f'{r["category"]:<34} n={r["thingDefCount"]:<4} avgMV={r["avgMarketValue"]:<8} packs={",".join(r["packs"])}')
