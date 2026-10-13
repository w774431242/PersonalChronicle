import os, re

ROOT = r"E:/SteamLibrary/steamapps/common/RimWorld/Data"
raw, by_name = [], {}

for dirpath, _, files in os.walk(ROOT):
    for fn in files:
        if not fn.lower().endswith(".xml"):
            continue
        p = os.path.join(dirpath, fn)
        try:
            txt = open(p, encoding="utf-8-sig", errors="ignore").read()
        except Exception:
            continue
        dlc = os.path.relpath(p, ROOT).replace("\\", "/").split("/")[0]
        for m in re.finditer(r"<FactionDef\b([^>]*)>(.*?)</FactionDef>", txt, re.S):
            attrs, blk = m.group(1), m.group(2)
            nm = re.search(r'(?<![A-Za-z])Name="([^"]+)"', attrs)
            pn = re.search(r'(?<![A-Za-z])ParentName="([^"]+)"', attrs)
            fields = {}
            for fm in re.finditer(r"<([A-Za-z_][A-Za-z0-9_]*)>(.*?)</\1>", blk, re.S):
                fields.setdefault(fm.group(1), []).append(fm.group(2).strip())
            rec = {"Name": nm.group(1) if nm else None,
                   "ParentName": pn.group(1) if pn else None,
                   "fields": fields, "dlc": dlc}
            dn = fields.get("defName")
            rec["defName"] = dn[0].strip() if dn else ""
            rec["label"] = (fields.get("label") or [""])[0].strip()
            raw.append(rec)
            if rec["Name"]:
                by_name[rec["Name"]] = rec

def res(rec, field, seen=None):
    seen = seen or set()
    if rec["fields"].get(field):
        return rec["fields"][field][0]
    pn = rec["ParentName"]
    if not pn or pn in seen or pn not in by_name:
        return None
    seen.add(pn)
    return res(by_name[pn], field, seen)

def cnt(rec, field, seen=None):
    seen = seen or set()
    if rec["fields"].get(field):
        return len(re.findall(r"<li>|<[A-Za-z_][A-Za-z0-9_]*>\d", rec["fields"][field][0]))
    pn = rec["ParentName"]
    if not pn or pn in seen or pn not in by_name:
        return 0
    seen.add(pn)
    return cnt(by_name[pn], field, seen)

# C# 构造函数体实测默认值
DEF = {"humanlikeFaction": True, "canRequestTraders": True, "hidden": False,
       "permanentEnemy": False, "displayInFactionSelection": True, "isPlayer": False,
       "permanentEnemyToEveryoneExceptPlayer": False}

rows = []
for r in raw:
    if not r["defName"]:
        continue
    def b(f):
        v = res(r, f)
        return DEF[f] if v is None else (v.strip().lower() == "true")
    def s(f, d=""):
        v = res(r, f)
        return d if v is None else v.strip()
    sgw = res(r, "settlementGenerationWeight")
    traders = {k: cnt(r, k) for k in
               ("caravanTraderKinds", "baseTraderKinds", "orbitalTraderKinds", "visitorTraderKinds")}
    has_trade = any(v > 0 for v in traders.values())
    # ── 可贸易判据（纯字段规则，因此第三方 Mod 派系自动适用） ──
    reasons = []
    if b("isPlayer"): reasons.append("玩家派系")
    if not b("humanlikeFaction"): reasons.append("非人形社会")
    if sgw is not None and float(sgw) <= 0: reasons.append("无地盘权重")
    if b("permanentEnemy"): reasons.append("永久敌对")
    if b("permanentEnemyToEveryoneExceptPlayer"): reasons.append("与全体敌对")
    if b("hidden"): reasons.append("隐藏派系")
    if not has_trade: reasons.append("无贸易接口")
    rows.append({
        "defName": r["defName"], "label": r["label"], "dlc": r["dlc"],
        "cat": s("categoryTag", "-"), "tech": s("techLevel", "-"),
        "human": b("humanlikeFaction"), "hidden": b("hidden"),
        "sgw": sgw if sgw is not None else "(默认)",
        "maxc": s("maxConfigurableAtWorldCreation", "-"),
        "req": s("requiredCountAtGameStart", "-"),
        "traders": traders, "hasTrade": has_trade,
        "tradeable": len(reasons) == 0, "why": reasons,
    })

tr = [r for r in rows if r["tradeable"]]
nt = [r for r in rows if not r["tradeable"]]
print("FactionDef 总数(具体):", len(rows), " 可贸易:", len(tr), " 不可贸易:", len(nt))
print()
print("=== 可贸易 ===")
print("%-22s %-12s %-11s %-9s %-5s %s" % ("defName", "categoryTag", "tech", "sgw", "DLC", "贸易接口 商队/基地/轨道/访客"))
print("-" * 104)
for r in sorted(tr, key=lambda x: x["defName"]):
    t = r["traders"]
    print("%-22s %-12s %-11s %-9s %-5s %d/%d/%d/%d" % (
        r["defName"], r["cat"], r["tech"], r["sgw"], r["dlc"],
        t["caravanTraderKinds"], t["baseTraderKinds"], t["orbitalTraderKinds"], t["visitorTraderKinds"]))
print()
print("=== 不可贸易（含理由） ===")
print("%-22s %-8s %s" % ("defName", "DLC", "理由"))
print("-" * 76)
for r in sorted(nt, key=lambda x: x["defName"]):
    print("%-22s %-8s %s" % (r["defName"], r["dlc"], " · ".join(r["why"])))

# ── 直出可嵌入预览页的 JS 数组 ──
import json
out = []
for r in sorted(rows, key=lambda x: (not x["tradeable"], x["defName"])):
    t = r["traders"]
    out.append({"id": r["defName"], "cat": r["cat"], "tech": r["tech"], "dlc": r["dlc"],
                "human": r["human"], "hidden": r["hidden"], "sgw": r["sgw"],
                "c": [t["caravanTraderKinds"], t["baseTraderKinds"],
                      t["orbitalTraderKinds"], t["visitorTraderKinds"]],
                "tr": r["tradeable"], "why": r["why"]})
print()
print("=== JS ===")
print(json.dumps(out, ensure_ascii=False, separators=(",", ":")))
