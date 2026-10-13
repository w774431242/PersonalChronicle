# CHG-20261008-PAWN-ECONOMY

用户授权经济需求驱动殖民者AI与微观机构实装。交付五类地图柜台、账户和订单、经济合同排班、真实工单计件工资、真实衣物库存/销售/穿戴、双人有限体检和RUIF管理窗口。

Domain/Application保持纯业务规则；Data负责版本化Scribe保存；Economy负责游戏对象、Job和UI适配。未知存档数据保留原始payload并停止操作。旧档不自动发钱；首次明确启用分配1000内部单位。生产完成复用既有Postfix；自然决策使用精确ThinkTree XML插入。

基线编译修复：纳入遗漏的Settings编译目录，替换岗位页net48不支持的字典API。不清理或回退工作区其他改动。

代码入口：`Source/PersonalChronicle/{Domain,Application}/PawnEconomy`、`Data/PawnEconomySaveData.cs`、`Economy/PawnEconomy`。资源：`Defs/PawnEconomy_Institutions.xml`、`Patches/PawnEconomy_ThinkTree.xml`、中英文语言文件。

验证与部署证据：[实装记录](docs/问题跟踪/经济机构_实装记录_2026-10-08.md)。使用与限制：[操作与验收](docs/设计文档/功能模块/经济体系/经济需求驱动AI/经济机构首版_操作与验收.md)。游戏内验收独立于编译及离线验证。
