---
change_id: CHG-20261008-ECON-DEPLOY-ACCEPTANCE
state: DeployedPendingGameAcceptance
scope: local B2.4.2 deployment and acceptance preparation
---

# 部署与验收准备变更定位

只覆盖本次授权单元。既有工作区其他修改未撤销或提交；运行源码、Def、双语资源未修改。历史代码验证报告保持原生成时快照，当前状态以部署证据和批次状态为准。

| 文件 | 行号与定位 | SHA-256 |
| --- | --- | --- |
| _verify_tmp/deploy-economy-b242.ps1 | 1: $ErrorActionPreference = 'Stop' | EAA8818A9093637CE0EA8BCF6597C87E80A5A32517D003484491DBB590806BBA |
| _verify_tmp/archive-b242-retired-release-files.ps1 | 1: $ErrorActionPreference = 'Stop' | 21590267F6BB31DA1D6459DC3586BABDB12BC7944D157B4976A1EB57E97FDA72 |
| _verify_tmp/finalize-economy-b242-acceptance.ps1 | 1: $ErrorActionPreference = 'Stop'<br>55: $anchors = @(for ($i=0; $i -lt $content.Count; $i++) { if ($content[$i] -match '^#\|"deployedThisChange"\|"b242Deployment"\|"initialDeploymentBackup"\|"newlyAddedFiles"\|^> 历史\|^\$ErrorActionPreference') { [string]($i+1) + ': ' + $content[$i].Trim().Replace('\|','\\|') } }) | 775D8D5E6C85A9A105B9B9223DC2615927CEA72DC9BBBA84F01A4F12FCE8B24E |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/09-B2.4.2_实机验收清单_2026-10-08.md | 1: # B2.4.2 实机验收清单<br>5: ## 1. 先明确这次能测什么<br>19: ## 2. 部署信息与准备<br>52: ## 3. 界面入口与最短操作路径 | CE43F669288EEFFEFAC88DF6883E8FB25A4DB4B26F169946EB987028DBB1DDD1 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B2.4.2_验收结果填写.csv | 1: 文件头/结果字段 | B8654FB3F84487207665D17EF8DA6B78BD02DB40855DB9099691D82D1052687F |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B2.4.2_部署验证_2026-10-08.json | 10: "initialDeploymentBackup": "E:\\SteamLibrary\\steamapps\\common\\RimWorld\\11\\殖民地综合管理系统\\_backups\\release-deploy\\deploy_20261008_182156_226",<br>2172: "newlyAddedFiles": [ | FCD3DAB93A2F63BF23E21B2E559009DBAE65CB178C02813783C2C286E51803AE |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json | 7: "deployedThisChange": true,<br>61: "b242Deployment": "docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B2.4.2_部署验证_2026-10-08.json", | 9BAB72F6A0A8A38528B2EE39D0F2F65AE43DD7280655DD810D609C42234022E6 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/经济机构首版_操作与验收.md | 1: # 经济机构首版：操作与验收<br>3: > 历史原型文档，操作步骤已被 B2.4.2 替代。当前构建禁用旧首次拨款、工资、购物及收费体检；不要按本文启动旧闭环。2026-10-08 部署后的实际操作与预期结果请使用 [B2.4.2 实机验收清单](实装落地/09-B2.4.2_实机验收清单_2026-10-08.md)。下文保留作设计演变和旧档对账参考。<br>9: ## 一、已经接通的机构<br>21: ## 二、从零启用 | 46013DA083BFE3F0321723F1A7BAA3EFE9DF2088EDFD663D5598D7BC50E99C76 |
| docs/changelog/v1.4.x_2026-10-08_经济AI部署与实机验收准备.md | 1: # 经济 AI 本地部署与实机验收准备 | D019F1D627104B8ACC1FA746B83EB5ED638A8E7D9931175C3AFFEED13788DC20 |


## 发布区变更与保护

211 个当前运行文件按部署 JSON 的逐文件清单同步；17 个明确退役文件如下，已先归档校验，原文件仅逐项删除，未镜像清空目录：

| 已退役路径 | 归档 SHA-256 |
| --- | --- |
| Defs/PowerGrid_Priority.xml | 6AB423EE089AA26044888A72EAB96CA9ADD756097C9315E6D4E48B904DC81331 |
| Defs/PowerGrid_Protection.xml | B1C2DEFF8BD02B0DF60C7A572AB6CAC846C7417A93993A4063A05DE08C23BA1A |
| Languages/ChineseSimplified/Keyed/PowerGrid.xml | E9A8164CAFEA97396D6350D06D0C1AD92E30F4CE1E785E7F5339FB759956B561 |
| Languages/English/DefInjected/ThingDef/PowerGrid_Protection.xml | 6398127694B6A89FB408F1D8633F7DEB2EDA74C8CEC5DDFF6795FFEF71BA14A5 |
| Languages/English/Keyed/PowerGrid.xml | 0545DA2E062BAE511234019018378A1430C78902E20577F02716200BB7AC3EEA |
| Textures/Things/Building/PC_PowerCircuitBreaker_east.png | FB27D82B37C4C8922839C8689D3762F831E1678E88A3442C647F1D1B42BFE254 |
| Textures/Things/Building/PC_PowerCircuitBreaker_north.png | 1A71330FC3012AF13A8BB8E005404BCE2F8021A04CFF71CFE04AA50A904D487B |
| Textures/Things/Building/PC_PowerCircuitBreaker_south.png | B5BC8402DE687EA0945DB19142593BAB572C114DAD14C3D4B4FA8EAD3F4F4622 |
| Textures/Things/Building/PC_PowerCircuitBreaker_west.png | 7186F18A6419C9AED6766611CD1E8AD014595E2CEABFA6671784D8D6331FBC49 |
| Textures/Things/Building/PC_PowerIsolator_east.png | DC540F71154867C9A778B9D12BC96F080E6C11D5C1FB9FC93806E240D705E59A |
| Textures/Things/Building/PC_PowerIsolator_north.png | 23BEFC5A70DB37BC0FCFA12B82B9C8AC221D51D2E30CDBE5FFFB17400714CF66 |
| Textures/Things/Building/PC_PowerIsolator_Off_east.png | 0F3E73D5158776B42FC3C0B8C7C3B4BD7B396C4874A7DC9F345BBE7A2D4DF9BC |
| Textures/Things/Building/PC_PowerIsolator_Off_north.png | 8226D0678D7940E787B7D4F569A03D4A2E4DCEF1F9C993F74720AEC82D622EB4 |
| Textures/Things/Building/PC_PowerIsolator_Off_south.png | 62188F853A54C0FC22B96096748DCA488988346242D5DA0CBE1BA3D01EC5E47E |
| Textures/Things/Building/PC_PowerIsolator_Off_west.png | EC49C43A51A49C9B18CFCA2DECB39A19363E617038D3A227FF62F0C191C9FE58 |
| Textures/Things/Building/PC_PowerIsolator_south.png | 9142C9B16F33909905E88CC2A9EE2FC308ACDD9EE5DECFFBFF42AB1AD259082D |
| Textures/Things/Building/PC_PowerIsolator_west.png | D7FECCA2D17239D55AF95D66A6F227C5626E90123B2D93D0D5A1B7F8F7635152 |


9 个首次部署新增文件在部署 JSON 的 newlyAddedFiles 中列明；首次旧文件备份 202 个，退役资源归档 17 个，二次同版快照 211 个，三者不可混用。工坊 ID 未改；未改玩家存档、未运行游戏、未上传工坊。

验证：全部 211 个源码运行资源/部署资源仍匹配已记录 SHA-256；80 个用例 ID 唯一，CSV UTF-8 BOM 往返 80 行且全部未测；部署状态已同步，GameVerified=false。复用先前同一 DLL 的 904/0/1 测试，不声称本次重跑。无全局 gate-runner，正式发布仍阻塞。
