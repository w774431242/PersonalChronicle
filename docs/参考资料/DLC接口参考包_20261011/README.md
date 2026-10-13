# DLC 遍历、解析与未来接口参考包

采集日期：2026-10-11。对象：本机 RimWorld **1.6.4871 rev590**，以及当前 PersonalChronicle 工作区源码。

这份资料用于后续设计、查证和预留接口。接口示例尚未接入生产项目；本次没有改动游戏 DLL、存档或 Mod 配置。

## 阅读顺序

1. [原版加载与解析链路](01_原版加载与解析链路.md)：从 DLC 发现到最终 Def 的调用顺序、条件与来源语义。
2. [当前项目接入地图](02_当前项目接入地图.md)：现有适配器、调用点、尚未覆盖的能力。
3. [未来接口方案](03_未来接口方案.md)：只读目录、定义索引、业务适配器的边界与状态契约。
4. [工具复现与验证](04_工具复现与验证.md)：在自己的本机重新采集、反编译和验证。
5. [接口契约示例](examples/DlcContracts.cs.txt)：独立于 Verse 的接口草案，不参与生产编译。

## 已采集结果

| 内容包 | Def/Patch XML 文件 | 原始顶层定义节点 | 条件属性节点 |
|---|---:|---:|---:|
| Core | 550 | 6,394 | 1,204 |
| Royalty | 150 | 757 | 292 |
| Ideology | 213 | 2,062 | 142 |
| Biotech | 224 | 1,440 | 81 |
| Anomaly | 198 | 1,484 | 74 |
| Odyssey | 235 | 1,658 | 185 |
| 合计 | **1,570** | **13,795** | **1,978** |

这些是未打补丁、未继承合并、未实例化的原始 XML 节点数量，包含抽象模板，**不是运行时 Def 数量**。6 个包均在磁盘存在，采集时配置均列为启用；运行时是否已加载没有在游戏中读取，记为 Unknown。官方包此次均未发现 LoadFolders.xml；原版仍有相应解析机制，供未来 Mod/DLC 使用。

## 证据与文件

- `snapshot/inventory.json`：6 个包的元数据、逐文件 SHA-256、定义结构、条件属性行号、源码调用位置、解析错误列表。
- `snapshot/raw-def-index.tsv`：可以按 `type + defName`、包名、ParentName 搜索的定义索引。
- `snapshot/runtime-api-and-il.json`：21 个原版类型的签名、字段、调用关系、字符串，以及关键方法完整 IL；含编译器生成的嵌套类型。动态生成解析器的真实调用不能仅靠静态调用边覆盖。
- `snapshot/project-source/`：本项目 3 个 DLC 门控/适配器的源码快照，以 `.cs.txt` 保存。
- `snapshot/validation.json`：工具与证据一致性检查结果；不包含游戏内验收结论。
- `manifest.json`：包内文件哈希与长度。ZIP 不包含游戏程序集、官方完整 XML、美术或音频；需要完整反编译代码时用本包工具在本机生成。

基准 `Assembly-CSharp.dll` SHA-256：

```text
5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a
```

当前版本确认存在 `ModsConfig.*Active`、`ModLister.*Installed`、`ExpansionDef/ExpansionDefOf`。探测的 `Verse.DlcManager`、`RimWorld.DlcManager`、`RimWorld.DlcDefOf` 不存在；这一结论仅针对本次 DLL，不推断历史版本。
