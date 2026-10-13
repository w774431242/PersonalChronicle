# 交付报告 — CHG-20261002-CANVAS-PERSIST

> 制造系统中心「自由编排画布」：设备卡持久化 + 放置入口修复
> 日期：2026-10-02 ｜ 版本 v1.4.0 → **v1.4.1** ｜ 编译 **0 错 0 警** ｜ GATE-DELTA **PASS**
> 状态：**已部署到 `Mods\PersonalChronicleVer0.1`，等待重启 RimWorld 实机验收**

---

## 一、用户报告的现象 → 根因 → 修复

| 现象 | 真实根因 | 修复 |
|---|---|---|
| **非编排模式无法从设备库把设备放到画布** | 代码路径其实**是通的**（`HandleDeviceDrop` 不受 `interactive` 门禁），但叠了 5 条可证阻断：① 设备库是**隐藏页签**（左轨默认停在「原料库」）；② 视口 <420 或高 <120 时左轨**整条静默消失**；③ 设备库超容量**静默截断**；④ 放置**零反馈**（不写 `LastAction`，查看态又没有 `#lbar`/指标行）；⑤ 放完**不能移动、没有单卡收回** | 新增**点选 → 点画布**等价路径（两态可用）；`#lbar` 状态提示行；空态文案按模式分派；窄视口明示提示；设备库溢出「还有 N 台」；设备卡可拖动 + 编排态单卡 × 收回 |
| **设备卡保存不住** | 写入链是通的，坏在**会话态与存档态生命周期脱钩**：`PreClose` 清空内存清单，而窗口是 `MainButtonDef.tabWindowInt` **缓存单例**、重读被永不复位的闩锁挡住；随后一次「撤销」或「再关一次窗」触发**差集反删**把存档条目整体抹掉且无法回滚 | `ClearSession` 置位 `detachedReloadPending` 强制重读；作用域键升级为 **(Game, Map) 复合键**；**移除差集反删**，删除只由显式动作驱动；撤销改**显式有效位** + 读档后基线快照；Save 预览**同时列出两个持久化容器** |

## 二、修复清单（17 项）

### P0（数据丢失级）
1. **空清单删档** — `MirrorDetachedToSave` 移除差集反删；`detachedLoaded` 守卫
2. **重开窗口不重读** — `detachedReloadPending` + 复合作用域键（换档/换地图/关窗重开全覆盖）
3. **撤销 = 全量删档** — `UndoValid` 有效位 + `EstablishBaseline` 基线快照 + 两处门禁统一为 `CanUndo`
4. **视图与存档口径分裂** — Save 预览含 `mfgLayout` + `mfgDeviceLayout`；`#lbar` 悬停复用同一份明细（顺带修掉 `{0}` 字面量 bug）

### P1（可用性级）
5. **设备卡三态全无拖动命中** — 新增 `HandleDeviceCardDrag`（车间内卡首拖即转独立摆放）
6. **隐形 200×120 拖拽热区** — 车间块命中区改为「实际设备卡的并集」
7. **下标即身份** — `PlacedOrder`（托盘下标）→ `PlacedZoneIds`（ZoneId），内存侧与存档侧对齐
8. **画布外仍可命中** — 命中矩形改 `卡 ∩ 视口`（tooltip / 点击 / 悬停同步）
9. **同局换地图不清会话** — 见 #2
10. **节流时钟可被回拨冻结** — 改单调时钟；并修「新增车间 cache miss ⇒ 车间整体消失」
11. **放置零反馈 / 入口不可发现** — 见现象一
12. **页签溢出静默截断** — 等比压缩 + 行尾「+N」（「编排」是唯一托盘入口）
13. **编排页签页内滚动破坏坐标** — 禁用滚动 + 不参与高度压缩
14. **宽轨侵入画布** — 轨道宽上限与视口联动
15. **大列表静默截断** — 「还有 N 台未显示」
16. **轨道会话态跨档残留** — `ResetRailSession()`（关窗复位）
17. **坐标系混用** — 全部收敛到 GUI 空间单一换算点 `DesignAt`

## 三、验证结果

| 项 | 结果 | 证据 |
|---|---|---|
| 编译 | ✅ **0 错 0 警** | `_scripts/compile_with_sdk.ps1` EXIT=0，产物 1,440,768 B |
| 翻译键集 | ✅ 中英各 **2481 键，完全一一对应** | `DIFF=0` |
| XML 合法性 | ✅ CN / EN 均 OK | `[xml]` 解析通过 |
| 孤儿键清理 | ✅ 4 键已删 | `CardDevices` / `CardNoDevice` / `Lbar.SaveTip` / `EmptyHint` 均 0 引用 |
| GATE-DELTA | ✅ **PASS** | 标注覆盖 10 个改动文件，无遗漏/超范围/残留 |
| 部署 | ✅ 49 文件已同步 | 目标 DLL 2026-10-02 23:50:26 / 1,407.0 KB；`modVersion=v1.4.1` |
| **实机验证** | ❌ **未执行** | 沙箱无法启动 RimWorld 进程 |

### 引擎行为取证（Mono.Cecil 静态核实，本次修复的立论基础）
- `MainButtonDef.get_TabWindow` 缓存到 `tabWindowInt` ⇒ **窗口是单例，切标签不销毁**
- `MainTabsRoot.ToggleTab` → `WindowStack.TryRemove` → **必调 `Window.PreClose()`**
- `MainButtonDef.Notify_SwitchedMap` → `TryRemove` ⇒ **换地图也会 PreClose**
- `Verse.Mouse.IsOver(Rect)` = `rect.Contains(Event.current.mousePosition) && !IsInputBlockedNow()`，**不检查 hotControl**

> 这三条推翻了「切标签不触发 PreClose ⇒ 不会丢档」的误判，是 P0 成立的直接依据。

## 四、⚠ 醒来后必须做的 5 项实机验收

1. 设备库拖放 → **切走标签 → 切回** ⇒ 设备卡**仍在**（P0-2 核心）
2. 设备库**点选** → 点画布 ⇒ 放置成功且有状态提示（P1-7）
3. 放置后**拖动改位** + 编排态点 **× 收回**（P1-1）
4. 手动存档 → 重读档 ⇒ 车间块与设备卡位置均正确回读
5. 读档后**首次点撤销**应无副作用（回退到基线），而非清空（P0-3）

**重启 RimWorld 才会生效**（运行中不重载程序集）。卡头 build 戳应显示 `2026-10-02 15:5x UTC`。

## 五、已知限制（如实登记，未修）

- 车间内设备的**默认位置**仍按区内序号推算（设备增删后同区卡片位移）；只有手动摆放过的卡按 `stableId` 稳定定位
- `schemaVersion` 仍是显示常量 `1`，未与 `ChronicleGameComponent.SchemaVersion` 绑定（字段级版本 gate 属后续批）
- 存档里的**孤儿设备条目无 GC**（设备拆除后条目静默残留）
- `FindDevice` / `IndexOfDetached` 在设备数很大时是 O(N·M) 字符串比较，未做 stableId→索引缓存
- 旧版本中已被差集反删掉的设备布局**无法找回**（数据已不在存档里）

## 六、回滚

- 版本级：回滚 v1.4.0（未改存档结构，旧 DLL 读同一存档不报错）
- DLL 级：`Assemblies/PersonalChronicle.dll.bak_20261002234922`
- 部署备份：`Mods\PersonalChronicleVer0.1\_backups\deploy_20261002_235403\`
- 代码级：见 `变更标注_CHG-20261002-CANVAS-PERSIST.md` 的 11 文件清单

## 七、交付物

| 文件 | 说明 |
|---|---|
| `变更标注_CHG-20261002-CANVAS-PERSIST.md` | 逐文件改动明细 + Gate Results + 验收清单 |
| `docs/changelog/v1.4.1-自由编排画布设备卡持久化修复-Changelog.md` | 对外 Changelog（REL-001 声明完整） |
| `About/About.xml` | v1.4.1 + 中英文能力说明 |
| `Assemblies/PersonalChronicle.dll` | 已编译并部署 |
