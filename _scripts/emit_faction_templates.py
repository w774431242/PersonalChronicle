# -*- coding: utf-8 -*-
"""把 MVS.xml 里的 MVS.Economy.Society.Empire.* / FactionDetail.Empire.* 重命名为
Faction.Royal.*（帝国 6/21/73 ③ lore 注记保留），并追加 Faction.Generic.* 通用模板
（非皇室可贸易派系的按 techLevel 反推通用画像）。中/英两份同步。"""
import io

FILES = {
    "zh": r"E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Languages\ChineseSimplified\Keyed\MVS.xml",
    "en": r"E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Languages\English\Keyed\MVS.xml",
}

ZH_SOCIETY = [
    ("Headline", "据点社会 · 生产为主 · 原料自足"),
    ("Body", "以生产人口为主、贸易补足缺口；产能随科技档位提升，消费以生存与基础制造为主。"),
    ("Tier1.Name", "上层 · 管理/技术"),
    ("Tier1.Desc", "组织 ＋ 高技能产出"),
    ("Tier1.Percent", "20%"),
    ("Tier1.Count", "—"),
    ("Tier2.Name", "底层 · 生产者"),
    ("Tier2.Desc", "劳工 ＋ 平民 ＋ 基础军"),
    ("Tier2.Percent", "80%"),
    ("Tier2.Count", "—"),
    ("Population.Label", "据点规模 × 每据点人数"),
    ("Population.Value", "运行时计数"),
    ("Population.Note", "未核实 · 方案 §7 运行时据点计数驱动"),
    ("Chip1", "产能随科技档位提升"),
    ("Chip2", "贸易补足原料缺口"),
    ("Chip3", "消费以生存为主"),
    ("Chip4", "据点规模决定总量"),
    ("Chip5", "单笔冲击随人口缩放"),
    ("Q1", "方向"),
    ("A1", "以在地生产为主；科技越高越能输出制成品、进口低级原料"),
    ("Q2", "需求"),
    ("A2", "生存物资 ＋ 基础制造原料；材质随科技档位覆盖（Neolithic 仅天然料，Spacer 含合金）"),
    ("Q3", "门槛 · 敏感度"),
    ("A3", "Silver 结算；人口越多单笔冲击越低"),
]

ZH_DETAIL = [
    ("F1.Key", "战争态势"), ("F1.Val", "随据点与边界"),
    ("F2.Key", "灾害频率"), ("F2.Val", "随生物群系"),
    ("F3.Key", "人口增减"), ("F3.Val", "据点规模驱动"),
    ("F4.Key", "贸易状态"), ("F4.Val", "开放"),
    ("F5.Key", "好感"), ("F5.Val", "中立 0"),
    ("F6.Key", "经济压力"), ("F6.Val", "随供需"),
    ("Ev1.Tag", "战争"), ("Ev1.Count", "随派系"), ("Ev1.Chips", "袭击 · 围城 · 掠夺"),
    ("Ev2.Tag", "灾害"), ("Ev2.Count", "随生物群系"), ("Ev2.Chips", "寒潮 · 热浪 · 虫巢 · 瘟疫"),
    ("Ev3.Tag", "人口"), ("Ev3.Count", "随派系"), ("Ev3.Chips", "流浪者加入 · 商队 · 野人"),
    ("EventNote", "类别＝原版 IncidentWorker 直接子类归并（①实测）；「近期具体触发」为 ③ 设计假设，原型化后读实际派发记录动态聚合。"),
]

EN_SOCIETY = [
    ("Headline", "Settlement society · production-led · self-sufficient"),
    ("Body", "Production population dominates; trade fills gaps. Output scales with tech level; consumption is survival and basic manufacturing."),
    ("Tier1.Name", "Upper · management/tech"),
    ("Tier1.Desc", "Organisation + high-skill output"),
    ("Tier1.Percent", "20%"),
    ("Tier1.Count", "-"),
    ("Tier2.Name", "Lower · producers"),
    ("Tier2.Desc", "Labour + civilians + basic military"),
    ("Tier2.Percent", "80%"),
    ("Tier2.Count", "-"),
    ("Population.Label", "settlements x per-settlement headcount"),
    ("Population.Value", "runtime count"),
    ("Population.Note", "Unverified - driven by runtime settlement count (design doc s7)"),
    ("Chip1", "Output scales with tech level"),
    ("Chip2", "Trade fills raw-material gaps"),
    ("Chip3", "Survival-led consumption"),
    ("Chip4", "Settlement size sets the total"),
    ("Chip5", "Per-deal shock scales with population"),
    ("Q1", "Direction"),
    ("A1", "Local production dominates; higher tech exports finished goods and imports low-grade raw material"),
    ("Q2", "Demand"),
    ("A2", "Survival goods + basic manufacturing input; materials follow tech level (Neolithic natural only, Spacer includes alloys)"),
    ("Q3", "Gate - sensitivity"),
    ("A3", "Silver settlement; larger population means lower per-deal shock"),
]

EN_DETAIL = [
    ("F1.Key", "War footing"), ("F1.Val", "settlement- and border-dependent"),
    ("F2.Key", "Disaster rate"), ("F2.Val", "biome-dependent"),
    ("F3.Key", "Population change"), ("F3.Val", "settlement-size driven"),
    ("F4.Key", "Trade status"), ("F4.Val", "Open"),
    ("F5.Key", "Goodwill"), ("F5.Val", "Neutral 0"),
    ("F6.Key", "Economic pressure"), ("F6.Val", "supply/demand driven"),
    ("Ev1.Tag", "War"), ("Ev1.Count", "faction-dependent"), ("Ev1.Chips", "raid - siege - looting"),
    ("Ev2.Tag", "Disaster"), ("Ev2.Count", "biome-dependent"), ("Ev2.Chips", "cold snap - heat wave - infestation - plague"),
    ("Ev3.Tag", "Population"), ("Ev3.Count", "faction-dependent"), ("Ev3.Chips", "wanderer joins - caravan - wild person"),
    ("EventNote", "Categories merge the direct subclasses of the vanilla IncidentWorkers (measured 1); \"recent actual triggers\" is a design-assumption 3, to be driven by real dispatch records once prototyped."),
]


def block(prefix, pairs):
    return "\n".join('  <{0}.{1}>{2}</{0}.{1}>'.format(prefix, k, v) for k, v in pairs)


for lang, path in FILES.items():
    with io.open(path, "r", encoding="utf-8") as f:
        text = f.read()

    # 1) Empire.* -> Royal.* （仅改键名前缀，值文本不动）
    text = text.replace("MVS.Economy.Society.Empire.", "MVS.Economy.Society.Faction.Royal.")
    text = text.replace("MVS.Economy.FactionDetail.Empire.", "MVS.Economy.FactionDetail.Faction.Royal.")

    # 2) 追加 Generic 模板
    soc = block("MVS.Economy.Society.Faction.Generic", ZH_SOCIETY if lang == "zh" else EN_SOCIETY)
    det = block("MVS.Economy.FactionDetail.Faction.Generic", ZH_DETAIL if lang == "zh" else EN_DETAIL)
    generic = "\n" + soc + "\n" + det + "\n"
    if "</LanguageData>" not in text:
        raise SystemExit("缺少 </LanguageData>：" + path)
    text = text.replace("</LanguageData>", generic + "</LanguageData>")

    with io.open(path, "w", encoding="utf-8") as f:
        f.write(text)
    print("updated:", path)
