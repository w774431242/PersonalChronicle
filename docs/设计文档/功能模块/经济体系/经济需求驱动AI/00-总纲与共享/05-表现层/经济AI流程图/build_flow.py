"""Build the current implementation flow, editable model, and graph checks."""
import json
from pathlib import Path

OUT = Path(__file__).resolve().parent
nodes, edges, notes, panels = [], [], [], []
def node(id, x, y, w, h, text, tone='blue', shape='rect', size=31):
    nodes.append(dict(id=id,x=x,y=y,w=w,h=h,text=text,tone=tone,shape=shape,size=size))
def edge(a,b,points=None,label='',lx=0,ly=0,tone='line',virtual=False):
    edges.append(dict(a=a,b=b,points=points or [],label=label,lx=lx,ly=ly,tone=tone,virtual=virtual))
def port(id,x,y,label='R',tone='green'):
    node(id,x-33,y-33,66,66,label,tone,'circle',37)
def text(x,y,t,size=27,tone='muted',align='center',bold=False):
    notes.append(dict(x=x,y=y,text=t,size=size,tone=tone,align=align,bold=bold))
def down(a,b,x,y1,y2,label=''):
    edge(a,b,[[x,y1],[x,y2]],label,x+58,(y1+y2)/2)

panels.extend([
    dict(x=60,y=190,w=960,h=2620,title='01  原版调度与经济决策',tone='blue'),
    dict(x=1070,y=190,w=1000,h=2620,title='02  购买、产权与穿戴',tone='teal'),
    dict(x=2120,y=190,w=1120,h=2620,title='03  就业、体检与收入',tone='orange')])
text(100,70,'Pawn 经济 AI · 完整闭环',62,'ink','left',True)
text(103,137,'当前实装逻辑｜原版 ThinkTree + 自研经济决策 + 原版 Job 执行',30,'muted','left')

# The left lane follows the actual priority: pending orders, autonomy/cooldown,
# already acquired/private apparel, suitable supply, then affordability.
node('state',200,310,640,110,'更新 Pawn 事实\n需求 · 健康 · 衣物 · 账户 · 合同')
node('vanilla',200,480,640,110,'原版自然决策点\nThinkTree 判断需求与工作')
node('gate',200,650,640,150,'经济活动可参与？\n系统有效 · 自由时段 · 安全','purple','diamond',30)
node('pending',200,870,640,150,'有未结订单？','purple','diamond')
node('new',200,1090,640,110,'允许发起新决策？\n自主开启 · 冷却结束','purple')
node('owned',200,1270,640,150,'有已购待穿或\n合适的自有衣物？','purple','diamond')
node('supply',200,1490,640,150,'营业商店有\n合适且可穿的衣物？','purple','diamond')
node('money',200,1710,640,150,'个人可用余额足够？','purple','diamond')
port('A_out',335,2010,'A','teal'); port('B_out',705,2010,'B','orange')
port('C_out',925,1345,'C','teal')
node('fallback',200,2120,640,105,'交回原版 / 等待条件变化\n需求、缺货或冷却后再评估','blue',size=29)
node('resume',200,2280,640,110,'续办原订单：购物 / 体检 / 签约\n结果回写；不重复计费','blue',size=29)
port('R_fallback',925,2173); port('R_resume',925,2335)
text(335,2070,'购买',29,'teal'); text(705,2070,'就业与收入',29,'orange')
text(925,1405,'先用',25,'teal')
text(520,2530,'准入与中断',32,'ink',bold=True)
text(520,2600,'自由时间 · 食物≥50% · 休息≥40%\n征召、失能、火灾及救治需求等阻止介入\n服务中食物<35%或休息<25%则退出',27)
text(520,2730,'原版寻路、需求与强制任务继续生效',26)
down('state','vanilla',520,420,480)
down('vanilla','gate',520,590,650)
down('gate','pending',520,800,870,'是')
edge('gate','fallback',[[200,725],[100,725],[100,2173],[200,2173]],'否',143,685)
down('pending','new',520,1020,1090,'否')
edge('pending','resume',[[840,945],[970,945],[970,2245],[520,2245],[520,2280]],'是',895,905)
down('new','owned',520,1200,1270,'是')
edge('new','fallback',[[200,1145],[100,1145],[100,2173],[200,2173]],'否',145,1110)
down('owned','supply',520,1420,1490,'否')
edge('owned','C_out',[[840,1345],[892,1345]],'是',865,1305)
down('supply','money',520,1640,1710,'是')
edge('supply','fallback',[[200,1565],[100,1565],[100,2173],[200,2173]],'无',145,1525)
edge('money','A_out',[[520,1860],[520,1930],[335,1930],[335,1977]],'足够',355,1890)
edge('money','B_out',[[520,1860],[520,1930],[705,1930],[705,1977]],'不足',695,1890)
edge('fallback','R_fallback',[[840,2173],[892,2173]])
edge('resume','R_resume',[[840,2335],[892,2335]])

port('A_in',1570,320,'A','teal')
node('reserve',1210,420,720,125,'准备购物订单\n冻结资金 · 锁定真实商品','teal')
node('visit',1210,630,720,125,'购物 Job：预约 → 寻路 → 到店\n复核商品、价格、资金与穿戴条件','teal',size=29)
node('trade',1210,840,720,140,'交付真实衣物并提交交易\n转账 · 登记产权 · 记录已购待穿','teal',size=30)
port('C_in',1570,1090,'C','teal')
text(1690,1090,'已购 / 自有衣物',27,'teal','left')
node('wearable',1240,1200,660,160,'现在可以穿戴？\n策略 · 部位 · 编码 · 锁定检查','purple','diamond',29)
node('wear',1220,1450,700,130,'执行穿戴 Job / 阶段\n原版 Wear 改变装备与实际属性','teal',size=30)
node('keep',1210,1780,720,130,'保留私人物品，等待可穿条件\n已购待穿，不再次扣款','amber',size=30)
port('R_wear',1570,1670); port('R_keep',1570,2010)
down('A_in','reserve',1570,353,420)
down('reserve','visit',1570,545,630)
down('visit','trade',1570,755,840)
down('trade','C_in',1570,980,1057)
down('C_in','wearable',1570,1123,1200)
down('wearable','wear',1570,1360,1450,'是')
edge('wearable','keep',[[1900,1280],[1990,1280],[1990,1845],[1930,1845]],'否',1950,1230)
down('wear','R_wear',1570,1580,1637)
down('keep','R_keep',1570,1910,1977)
text(1570,2180,'资金回路',35,'ink',bold=True)
node('public_legend',1200,2290,270,85,'公共账户','blue',size=29)
node('private_legend',1640,2290,270,85,'个人账户','teal',size=29)
edge('public_legend','private_legend',[[1470,2310],[1640,2310]],'工资',1555,2275,'teal')
edge('private_legend','public_legend',[[1640,2355],[1470,2355]],'购衣款',1555,2405,'teal')
text(1570,2515,'体检费用：付款方 → 机构 → 医生\n医生获得60%，机构留存40%',29)
text(1570,2660,'启动拨款仅一次；正常交易只转账\n购买不会自动终止劳动合同',28)

port('B_in',2660,320,'B','orange')
node('contract',2300,410,720,145,'已有有效劳动合同？','purple','diamond')
node('offer',2300,650,720,145,'有可加入的有效岗位？\n名额 · 工作启用 · 无排班冲突','purple','diamond',29)
node('report',2300,890,720,145,'无需体检，或已有合格报告？','purple','diamond',29)
node('clinic',2300,1130,720,145,'体检医生、预约与资金可用？','purple','diamond',29)
node('exam',2290,1365,740,145,'体检 Job：医患到场，检查600 tick\n生成报告并结算；回决策复核报告','orange',size=29)
port('R_exam',3100,1438)
node('sign',2290,1640,740,120,'劳务站 Job：到场复核并签约\n占用名额 · 创建经济工坊排班','orange',size=30)
node('work',2290,1835,740,120,'原版 DoBill：取料 → 制作 → 产出\n保留原版工单、技能、身体与预约限制','orange',size=28)
node('complete',2300,2020,720,140,'收到合法生产完成事实？','purple','diamond',30)
port('R_incomplete',2210,2090)
text(2220,2160,'未完成\n不计薪',24,'muted')
node('payable',2300,2210,720,140,'公共账户足以支付工资？','purple','diamond',30)
node('pay',2290,2400,740,105,'按真实完成结薪，更新账户\nJob ID 去重，不重复发放','orange',size=29)
node('arrears',2290,2550,740,100,'保留一次欠薪，停岗解约\n补发后需重新发布岗位与签约','amber',size=28)
node('wait',2290,2690,740,95,'等待收入 / 岗位 / 体检条件\n原版活动继续，条件变化后复评','amber',size=28)
port('R_pay',3100,2453); port('R_arrears',3100,2600); port('R_wait',3100,2738)
down('B_in','contract',2660,353,410)
down('contract','offer',2660,555,650,'无')
edge('contract','work',[[2300,482],[2160,482],[2160,1895],[2290,1895]],'有',2210,443)
down('offer','report',2660,795,890,'有')
edge('offer','wait',[[3020,722],[3210,722],[3210,2670],[2660,2670],[2660,2690]],'无',3105,682)
down('report','clinic',2660,1035,1130,'需体检')
edge('report','sign',[[2300,962],[2200,962],[2200,1700],[2290,1700]],'是',2240,920)
down('clinic','exam',2660,1275,1365,'可用')
edge('clinic','wait',[[3020,1202],[3210,1202],[3210,2670],[2660,2670],[2660,2690]],'不可用',3090,1162)
edge('exam','R_exam',[[3030,1438],[3067,1438]])
down('sign','work',2660,1760,1835)
down('work','complete',2660,1955,2020)
edge('complete','R_incomplete',[[2300,2090],[2243,2090]],'否',2260,2050)
down('complete','payable',2660,2160,2210,'是')
down('payable','pay',2660,2350,2400,'足够')
edge('payable','arrears',[[2300,2280],[2200,2280],[2200,2600],[2290,2600]],'不足',2240,2245)
edge('pay','R_pay',[[3030,2453],[3067,2453]])
edge('arrears','R_arrears',[[3030,2600],[3067,2600]])
edge('wait','R_wait',[[3030,2738],[3067,2738]])

node('recover',170,2850,2960,135,'全局恢复（覆盖全部机构 Job）\n未提交：取消并释放冻结款｜已提交：保留商品、工资与报告｜读档：取消未结单；坏档保留原数据并停用经济','red',size=30)
port('R_hub',650,3095)
node('feedback',760,3040,1850,110,'回写事实、目标与收支 → 重新评估需求缺口\n成功、等待、中断、欠薪均回到循环；不会跳过原版调度','green',size=33)
edge('R_hub','feedback',[[683,3095],[760,3095]],tone='green')
edge('recover','feedback',[[1650,2985],[1650,3040]],tone='red')
edge('feedback','state',[[2610,3095],[3270,3095],[3270,275],[520,275],[520,310]],tone='green')
text(1630,275,'下一次原版自然决策',25,'green')
text(1650,3210,'同字母圆点相连：A 购买入口 · B 收入入口 · C 穿戴入口 · R 统一反馈；各机构 Job 共用原版预约、寻路与 Toil。',27)
text(1650,3275,'范围：衣物消费、计件就业、入职体检的当前代码逻辑；完整游戏内闭环仍待实机验收。',26)

# Explicit graph edges for on-page connectors and global Job recovery.
edge('A_out','A_in',virtual=True); edge('B_out','B_in',virtual=True); edge('C_out','C_in',virtual=True)
for n in nodes:
    if n['id'].startswith('R_') and n['id'] != 'R_hub': edge(n['id'],'R_hub',virtual=True)
for id in ['resume','reserve','visit','trade','wear','exam','sign','work']:
    edge(id,'recover',virtual=True,label='中断/失效/超时/读档')
legend_ids={'public_legend','private_legend'}
flow_ids={n['id'] for n in nodes}-legend_ids
adj={id:set() for id in flow_ids}; rev={id:set() for id in flow_ids}
for e in edges:
    assert e['a'] in {n['id'] for n in nodes} and e['b'] in {n['id'] for n in nodes}
    if e['a'] in adj and e['b'] in adj:
        adj[e['a']].add(e['b']); rev[e['b']].add(e['a'])
def visit(graph,start):
    seen=set(); todo=[start]
    while todo:
        at=todo.pop()
        if at in seen: continue
        seen.add(at); todo.extend(graph[at]-seen)
    return seen
assert visit(adj,'state')==flow_ids, ('Unreachable',flow_ids-visit(adj,'state'))
assert visit(rev,'state')==flow_ids, ('No return path',flow_ids-visit(rev,'state'))
model=dict(width=3300,height=3340,scale=2,nodes=nodes,edges=edges,notes=notes,panels=panels)
(OUT/'flow_model.json').write_text(json.dumps(model,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'逻辑校验.json').write_text(json.dumps(dict(result='Passed',flowNodes=len(flow_ids),edges=len(edges),
    allNodesReachable=True,allNodesReturnToState=True,scope='current runtime; no gameplay validation'),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'PASS: {len(flow_ids)} flow nodes, {len(edges)} edges; all reachable and all return to state.')
