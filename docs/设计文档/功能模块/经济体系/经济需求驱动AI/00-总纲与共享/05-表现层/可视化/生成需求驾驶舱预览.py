"""Static design preview with hypothetical data; no game or RUIF runtime code."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json

HERE = Path(__file__).resolve().parent
SCALE = 2
W, H = 1600, 1070
FONT = Path('C:/Windows/Fonts/msyh.ttc')
BOLD = Path('C:/Windows/Fonts/msyhbd.ttc')
image = Image.new('RGB', (W*SCALE, H*SCALE), '#101823')
draw = ImageDraw.Draw(image)
texts = []
fonts = {}
C = dict(text='#edf2f8', muted='#aebbd0', blue='#6aaff8', amber='#f0bc65', red='#ef9090', panel='#1a2636', line='#334358')

def font(size, bold=False):
    key = (size,bold)
    if key not in fonts:
        fonts[key] = ImageFont.truetype(str(BOLD if bold else FONT), size*SCALE)
    return fonts[key]

def rect(box, fill, outline=None, radius=0):
    box = tuple(int(v*SCALE) for v in box)
    if radius:
        draw.rounded_rectangle(box, radius*SCALE, fill, outline, width=SCALE)
    else:
        draw.rectangle(box, fill, outline, width=SCALE)

def text(x,y,value,size=17,color=None,bold=False,max_width=None):
    f=font(size,bold)
    width=draw.textlength(value,font=f)/SCALE
    if max_width is not None:
        assert width <= max_width, (value,width,max_width)
    assert x+width <= W-12 and y+size+8 <= H, (value,x,y,width)
    draw.text((x*SCALE,y*SCALE),value,font=f,fill=color or C['text'])
    texts.append(dict(text=value,x=x,y=y,width=round(width,2),fontSize=size))

def line(y,x1=60,x2=920):
    draw.line((x1*SCALE,y*SCALE,x2*SCALE,y*SCALE), fill=C['line'], width=SCALE)

def button(x,y,width,label):
    rect((x,y,x+width,y+40),'#243c58',C['line'],6)
    text(x+14,y+8,label,16,max_width=width-24)

def bar(x,y,width,used,cap,color):
    rect((x,y,x+width,y+12),'#314056',radius=3)
    rect((x,y,x+width*min(used/cap,1),y+12),color,radius=3)

budgets=[9,9,9,9,9,6,3,0,0,0]
needs=[3]*10
def intent(price): return sum(min(n,b//price) for n,b in zip(needs,budgets))
assert intent(3)==18 and intent(2)==19
fixture={
    'status':'Hypothetical design fixture; not game telemetry, not RUIF runtime screenshot.',
    'population':12,'forecastHours':24,
    'food':{'grossQuantity':36,'reliablePublicCover':6,'paidCandidate':30,
            'needsAfterPublicCover':needs,'foodBudgetsAfterOtherNecessaryCosts':budgets,
            'price':3,'budgetIntent':intent(3),'confirmedOpen':12,'forecastSupply':24,
            'moneyBlockedPeople':sum(b//3<n for n,b in zip(needs,budgets)),
            'moneyBlockedQuantity':30-intent(3)},
    'scenarios':{'newPrice':{'scope':'Next equal-needs and equal-budget window, new orders only; current confirmed orders are not repriced.','price':2,'intent':intent(2),'cashIfAllIntentDelivered':intent(2)*2,'baselineCashIfAllIntentDelivered':intent(3)*3,'zeroBudgetPeopleUnaffected':sum(b==0 for b in budgets)},
                 'publicPurchase':{'quantity':6,'unitPrice':3,'publicCost':18,'personalIntentUnchanged':18,'paidCandidateCoverageIfDelivered':24,'totalLifeCoverageIfAllDelivered':30}},
    'rows':[
        ['食品','30 份','18 份','12 份','24 份','购买力不足'],
        ['衣物','2 件','1 件','1 件','4 件','部分买不起'],
        ['医疗','3 人次','公共','—','1 人次','缺可用医生'],
        ['托育','8 小时','公共','—','2 小时','人手与时段不足'],
        ['睡眠','3 时段','公共','—','3 时段','尚未安排休息'],
        ['娱乐','4 人','公共','—','可用','生活时间冲突']
    ],
    'notes':['Amounts cannot be added across units. Public services have no commercial orders in this fixture.',
             'Budget intent includes confirmed orders; do not add them.',
             'All quantities, population and windows are illustrative, not vanilla balance constants.']
}
assert fixture['food']['moneyBlockedPeople']==5

text(40,27,'殖民地需求驾驶舱',30,bold=True)
text(1072,37,'设计预览 · 假设数据 · 尚未实装',18,C['amber'],max_width=488)
text(40,78,'当前地图：示例殖民地    居民：12 人    规划窗：未来 24 小时    事实：当前快照',17,C['muted'])
for x,width,label,selected in [(40,160,'需求总览',True),(212,160,'个人与家庭',False),(384,160,'公有机构',False),(556,160,'政策与反馈',False)]:
    rect((x,117,x+width,158),'#2a4565' if selected else C['panel'],C['line'],6)
    text(x+20,126,label,18,C['blue'] if selected else C['muted'])
rect((40,177,1560,222),'#39282c','#67454a',6)
text(57,185,'紧急受阻：2 人。先查看具体对象、可用帮助与时限；急救不按付款金额排序。',18,C['red'])

for x,title,value,note,color in [
    (40,'生活缺口','按需求域查看','食物、休息、健康、照护等',C['text']),
    (552,'食品预算受阻','5 人 / 缺 12 份','有需要，也有库存，但买不起',C['amber']),
    (1064,'医疗供给受阻','2 人待服务','需求 3 人次，可供 1 人次',C['red'])]:
    rect((x,240,x+496,330),C['panel'],C['line'],8)
    text(x+18,250,title,16,C['muted'])
    text(x+18,275,value,23,color,True)
    text(x+245,285,note,14,C['muted'],max_width=235)

rect((40,350,940,840),C['panel'],C['line'],8)
text(60,368,'需要什么，卡在哪里',23,bold=True)
text(60,407,'同窗比较；不同单位不相加。公共服务不虚构商业订单。',15,C['muted'])
xs=[60,190,312,434,546,670]
headers=['需求域','方案量','预算意向','已确认','可供量','主要受阻原因']
for x,label in zip(xs,headers):text(x,447,label,15,C['muted'])
line(480)
for i,row in enumerate(fixture['rows']):
    y=490+i*48
    if i==0:rect((52,y-2,928,y+39),'#223c58',radius=4)
    for j,(x,value) in enumerate(zip(xs,row)):
        color=C['text']
        if j==5: color=C['amber'] if i<2 else C['muted']
        text(x,y+5,value,16,color,max_width=(xs[j+1]-x-12 if j<5 else 244))
text(60,798,'[食品已选中]   完整需求域与个人列表使用分页选择。',15,C['muted'])

rect((964,350,1560,840),C['panel'],C['line'],8)
text(986,368,'食品详情｜未来 24 小时',23,bold=True)
for x,label,value in [(986,'总需要','36 份'),(1170,'可靠公共覆盖','6 份'),(1360,'付费候选','30 份')]:
    text(x,415,label,15,C['muted'])
    text(x,443,value,24,bold=True)
text(986,500,'预算购买意向',17)
text(1330,500,'18 / 30 份',18,C['blue'])
bar(986,533,548,18,30,C['blue'])
text(986,566,'其中已确认',17)
text(1330,566,'12 / 30 份',18,C['blue'])
bar(986,599,548,12,30,'#5484ba')
text(986,628,'已确认包含在预算意向内，二者不能相加。',15,C['muted'])
text(986,667,'主因：5 人缺预算，合计 12 份尚无资金支持。',17,C['amber'],max_width=550)
text(986,701,'预计可供 24 份；先核对购买力与真实订单。',16,C['muted'])
button(986,750,168,'调整新报价')
button(1166,750,168,'公共采购预算')
button(1346,750,190,'查看受阻居民')
text(986,807,'医疗、托育等逐类适配前沿用原版公共机制。',14,C['muted'])

rect((40,858,1560,1017),'#1a2d40',C['line'],8)
text(60,872,'政策情景｜固定预算与需要；降价仅比较下一同等窗口的新订单，当前确认单不重定价',19,bold=True)
text(60,910,'新单价 3 → 2 银币 / 份',19,C['blue'],True)
text(60,944,'预算意向 18 → 19 份；全额交付现金情景 54 → 38。',17,max_width=720)
text(60,977,'3 名零预算居民仍买不起；已确认订单保持原价。',15,C['muted'])
text(824,910,'公共采购 6 份',19,C['blue'],True)
text(824,944,'公共需预留 18 银币；原付费候选覆盖 18 + 6 = 24 份。',17,max_width=720)
text(824,977,'含原有公共覆盖后共 30 / 36 份；个人意向仍 18 份。',15,C['muted'])
text(40,1035,'需求 → 受阻原因 → 政策调整 → 真实行动与使用 → 结果反馈。预测与情景均不代表已经交付。',16,C['muted'])

HERE.mkdir(parents=True,exist_ok=True)
image.save(HERE/'需求驾驶舱_布局预览.png',dpi=(192,192))
(HERE/'需求驾驶舱_假设数据.json').write_text(json.dumps(fixture,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(HERE/'需求驾驶舱_排版记录.json').write_text(json.dumps({'resolution':[W*SCALE,H*SCALE],'font':str(FONT),'textCount':len(texts),'boundsChecked':True,'texts':texts},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Created {W*SCALE}x{H*SCALE} preview; {len(texts)} text bounds checked; hypothetical scenario arithmetic passed.')
