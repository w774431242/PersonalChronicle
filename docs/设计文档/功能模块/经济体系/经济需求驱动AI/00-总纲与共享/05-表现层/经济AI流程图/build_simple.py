"""Generate a compact, readable view of the implemented economic AI loop."""
from pathlib import Path
import json

out = Path(__file__).resolve().parent
nodes, edges, notes = [], [], []
def node(id,x,y,w,h,text,tone,size=38):
    nodes.append(dict(id=id,x=x,y=y,w=w,h=h,text=text,tone=tone,shape='rect',size=size))
def edge(a,b,points,label='',lx=0,ly=0,tone='line'):
    edges.append(dict(a=a,b=b,points=points,label=label,lx=lx,ly=ly,tone=tone,virtual=False))
def note(x,y,text,size=32,tone='muted',bold=False):
    notes.append(dict(x=x,y=y,text=text,size=size,tone=tone,align='center',bold=bold))

note(1500,100,'Pawn 经济 AI · 简明闭环',66,'ink',True)
note(1500,185,'需求决定行动，真实行动改变状态，下一轮重新判断',36)
node('state',850,270,1300,130,'① 读取当前状态\n需求 · 衣物 · 余额 · 合同 · 体检报告','blue')
node('gate',850,470,1300,160,'② 原版调度：现在适合经济活动吗？\n自由时段且安全；原版需求与强制命令优先','blue',36)
node('choose',850,710,1300,180,'③ 选择行动（按优先级）\n未结订单先续办；新决策需自主开启、冷却结束\n再看衣物、商品与余额','purple',35)
node('wait',2480,710,360,180,'继续原版活动\n等待条件变化','amber',34)
node('wear',270,1060,700,340,'优先：已有合适衣物\n检查穿戴条件\n直接穿戴','teal',38)
node('buy',1150,1060,700,340,'其次：有合适商品且钱够\n冻结款项 → 到店复核\n交付衣物并扣款 → 穿戴','teal',36)
node('income',2030,1060,700,340,'有商品但钱不足：获取收入\n已有合同 → 原版生产\n无合同 → 找岗 → 必要体检\n→ 签约 → 原版生产\n完成产出 → 结薪','orange',34)
node('execute',650,1580,1700,180,'④ 执行并确认真实结果（原版 Job）\n穿戴生效 · 交货扣款 · 体检出报告 · 完工计薪\n失败或中断：未提交则取消退款；已提交则保留结果','blue',35)
node('feedback',850,1890,1300,160,'⑤ 回写状态与收支，重新评估需求\n成功、等待、中断、欠薪均进入下一轮','green',38)

edge('state','gate',[[1500,400],[1500,470]])
edge('gate','choose',[[1500,630],[1500,710]],'可以',1565,668)
edge('gate','wait',[[2150,550],[2660,550],[2660,710]],'暂不参与',2400,515)
edge('choose','wait',[[2150,800],[2480,800]],'无可行动条件',2310,762)
edge('choose','wear',[[1500,890],[1500,970],[620,970],[620,1060]])
edge('choose','buy',[[1500,890],[1500,1060]])
edge('choose','income',[[1500,890],[1500,970],[2380,970],[2380,1060]])
edge('wear','execute',[[620,1400],[620,1490],[1500,1490],[1500,1580]])
edge('buy','execute',[[1500,1400],[1500,1580]])
edge('income','execute',[[2380,1400],[2380,1490],[1500,1490],[1500,1580]])
edge('execute','feedback',[[1500,1760],[1500,1890]])
edge('wait','feedback',[[2840,800],[2900,800],[2900,1970],[2150,1970]],tone='amber')
edge('feedback','state',[[850,1970],[120,1970],[120,335],[850,335]],tone='green')
note(450,290,'下一次原版自然决策',30,'green')
note(1500,2140,'条件暂缺就等待：缺货、缺岗、体检未完成或穿戴受限，均不强行执行。',32)
note(1500,2210,'资金不足以发薪：记一次欠薪并停岗解约；补发后重新招聘。读档取消未结订单。',32)
note(1500,2280,'当前范围：衣物消费、计件就业与入职体检；图结构已校验，游戏内闭环仍待实机验收。',29)

ids={n['id'] for n in nodes}
adj={i:set() for i in ids}; rev={i:set() for i in ids}
for e in edges:
    adj[e['a']].add(e['b']); rev[e['b']].add(e['a'])
def walk(g):
    seen=set(); todo=['state']
    while todo:
        i=todo.pop()
        if i not in seen:
            seen.add(i); todo.extend(g[i]-seen)
    return seen
assert walk(adj)==ids and walk(rev)==ids
model=dict(width=3000,height=2350,scale=2,nodes=nodes,edges=edges,notes=notes,panels=[])
(out/'simple_model.json').write_text(json.dumps(model,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'简明版_逻辑校验.json').write_text(json.dumps(dict(result='Passed',flowNodes=len(ids),edges=len(edges),allNodesReachable=True,allNodesReturnToState=True),ensure_ascii=False,indent=2),encoding='utf-8')
# Reuse the existing deterministic glyph-outline renderer without altering the detailed version.
script=(out/'render_flow.ps1').read_text(encoding='utf-8-sig')
script=script.replace('flow_model.json','simple_model.json').replace('width="6600" height="6680" viewBox="0 0 3300 3340"','width="6000" height="4700" viewBox="0 0 3000 2350"')
script=script.replace('width="3300" height="3340"','width="3000" height="2350"')
script=script.replace('Pawn经济AI完整闭环流程图','Pawn经济AI简明闭环流程图').replace('所有中文已转换矢量轮廓；同字母圆点相连。','所有中文已转换矢量轮廓；全部路径通过实线箭头返回反馈。')
script=script.replace('Pawn经济AI_闭环流程图_','Pawn经济AI_简明闭环_').replace('6600×6680','6000×4700')
(out/'render_simple.ps1').write_text(script,encoding='utf-8-sig')
print(f'PASS: {len(ids)} nodes, {len(edges)} connections; fully closed.')
