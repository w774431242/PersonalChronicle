# Steam 创意工坊更新说明 · 2026-08-10

可直接复制到 Steam Workshop 更新说明框。以简体中文为主，英文附后。

---

## 简体中文

```
Personal Chronicle - Archive v1.0.0 更新（2026-08-10）

【新增 · 地图志】
- 地点不再是空壳！现在会自动归档殖民地的每张地图、任务地点与可交易城市。
- 每张地图记录：身份（地图类型/尺寸）、归属派系、地理环境（地貌/海拔/海岸/污染/温度）、生命周期（落成/荒废/摧毁）、商贸信息（可交易否/主要售卖类型/许可需求）。

【新增 · 战役三要素】
- 战役卡现在显示：触发日期、来袭规模（敌人数量）、击退历时（自动降级为天/小时/分钟）。
- 基于 RimWorld 1.6 真实回调精确归档，零轮询，只在完整流程过后记录一次。

【重构 · 社会关系网络图】
- 统一节点尺寸，按关系重要性分层定位（夫妻对称核心、父母上排、子女下排、平辈左右、朋友/宿敌外圈）。
- 连线改为从卡片中心出发/收尾的 Z 形正交线，带圆角过渡；初次打开自动 fit，滚轮缩放、左键拖拽平移。
- 最多展示 24 个关系节点，放大时卡片/圆角/字号三重联动，避免重叠。

【新增 · 装备传承拓展】
- 武器/装备详情新增「溯源」「同袍共用」「退役仪式」三个页签。
- 溯源：追踪装备是殖民地制造还是战场剥取，工坊署名链可识别“匠人被自己打造的武器所杀”。
- 同袍：显示装备被哪些殖民者共同使用过。
- 退役：记录装备销毁时的最后持有者、服役天数与归宿。

【优化 · 装备捕捉范围】
- 不再把防尘衣、工作服等无战斗价值服装收入档案。
- 武器恒捕捉；护甲仅当 Sharp/Blunt 防护达到阈值才捕捉，全部数据驱动，可在 Def 中调整。

【修复 · 社会关系】
- 开局殖民者的初始社交关系（夫妻、父母、子女等）现在能正确捕捉并显示。
- 新增“朋友/宿敌”合成关系（基于好感度），与原版 direct 关系合并展示。

【修复 · 传承击杀归属】
- 修复历任持有者击杀计数双计 bug：接班 tick 不再被两边重复计入；legacy 存档无 startTick 时也能正确分配。

【其他】
- 全面统一 UI Design System：所有颜色、字体、间距走 UITheme/UIComponents 令牌，修复多处中文行高不足。
- Pawn 检视面板「档案」页签已注入全部 humanlike 种族。
- API 契约版本与发布版本统一为 1.0.0，第三方可用 PersonalChronicleApi.TryGet 接入。
- 修复 About.xml 误加 <tags> 导致的 XML error；创意工坊标签请在上传页面手动填写。

已知兼容：RimWorld 1.6 + Harmony。本 mod 为档案馆定位，不修改游戏机制，只读记录。
```

---

## English

```
Personal Chronicle - Archive v1.0.0 Update (2026-08-10)

[New · Location Atlas]
- Locations are no longer a placeholder. The mod now auto-archives every colony map, quest site, and tradeable settlement.
- Each map records: identity (map type/size), owning faction, geography (biome/elevation/coastal/pollution/temperature), lifecycle (established/ruined/destroyed), and trade info (tradeable / sale categories / permit required).

[New · Battle Three Elements]
- Battle cards now show: trigger date, raid force size (enemy count), and time to repel (auto-downgrades to days/hours/minutes).
- Precise archival based on RimWorld 1.6 native callbacks; zero polling, recorded once the whole engagement completes.

[Revamped · Social Relation Network]
- Unified node size with importance-based grid layout (spouses as symmetric core, parents above, children below, siblings left/right, friends/rivals outer ring).
- Links are now Z-shaped orthogonal lines from card center to card center with rounded corners; auto-fit on first open, mouse-wheel zoom, left-drag pan.
- Up to 24 relation nodes; card size, corner radius, and font scale together to prevent overlap at any zoom.

[New · Equipment Legacy Extension]
- Thing detail tabs added: Origin, Co-use, Decommission.
- Origin: trace whether gear was colony-crafted or battlefield-looted; maker chain can detect "maker killed by their own creation".
- Co-use: shows which colonists shared the same equipment.
- Decommission: records final holder, service days, and resting place when gear is destroyed.

[Improved · Thing Capture Scope]
- Dusters, workwear, and other non-combat apparel are no longer archived.
- Weapons always captured; apparel only captured when Sharp/Blunt armor reaches threshold. Fully data-driven via Def.

[Fix · Social Relations]
- Starting colonists' initial social relations (spouse, parent, child, etc.) are now captured correctly.
- Synthetic Friend/Rival relations based on opinion are merged with native direct relations.

[Fix · Legacy Kill Attribution]
- Fixed double-counting kills across holders; handover tick is no longer counted twice. Legacy saves without start ticks also allocate kills correctly.

[Other]
- Fully unified UI Design System: all colors, fonts, and spacing go through UITheme/UIComponents tokens; fixed multiple CJK line-height issues.
- Pawn inspect panel "Archive" tab injected for all humanlike races.
- API contract version aligned with release version 1.0.0; third-party mods can integrate via PersonalChronicleApi.TryGet.
- Fixed XML errors caused by an invalid <tags> block in About.xml; Workshop tags must be set manually on the upload page.

Compatibility: RimWorld 1.6 + Harmony. This mod is an archive: it reads and records only, without altering game mechanics.
```

---

## 使用建议

- Steam 工坊更新说明框建议只放「简体中文」或「English」其中一段，或分段粘贴。
- 若目标受众以中文玩家为主，优先粘贴简体中文段；英文段可作为补充放在下方。
