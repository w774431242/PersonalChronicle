$ErrorActionPreference = 'Stop'
$dir = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\_scripts\_mod_audit'
$inCsv = Join-Path $dir 'mods_trim.csv'
$outHtml = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\docs\mod分类报告.html'

# ---- category order & meta ----
$catOrder = @(
  '官方内容','框架与库','性能与优化','界面与信息显示','汉化与翻译',
  '存储与物流','生产制造与资源','农业种植与食物','医疗义体与仿生','武器护甲与战斗',
  '载具与交通','建筑家具与装饰','文化意识形态与宗教','角色特质与成长','社会关系与社交',
  '经营管理与任务事件','生活品质与实用工具','生物生态与纹理美化'
)
$catMeta = @{
  '官方内容'          = @{Icon='♛'; Color='#9aa5b1'; Desc='RimWorld 本体与官方资料片（Royalty / Ideology / Biotech / Anomaly / Odyssey）'}
  '框架与库'          = @{Icon='⚙';  Color='#4d9de0'; Desc='开发框架、前置库、功能基座，为其他 Mod 提供底层能力'}
  '性能与优化'        = @{Icon='⚡'; Color='#e0b64d'; Desc='提升帧率与游戏加载/运行性能'}
  '界面与信息显示'    = @{Icon='▦';  Color='#7cc4a8'; Desc='UI 增强、信息面板、HUD、交互提示等界面层改造'}
  '汉化与翻译'        = @{Icon='文';  Color='#c98fe0'; Desc='简体/繁体中文汉化包、翻译引擎与本地化补丁'}
  '存储与物流'        = @{Icon='▣';  Color='#8fa3c0'; Desc='仓储容器、堆叠、货架等物品存放与物流系统'}
  '生产制造与资源'    = @{Icon='⚒';  Color='#d08a5a'; Desc='工作台、生产流程、资源生成与加工'}
  '农业种植与食物'    = @{Icon='✿';  Color='#8fc98f'; Desc='农作物、烹饪、食材与饮食系统'}
  '医疗义体与仿生'    = @{Icon='✚';  Color='#e08f8f'; Desc='医疗设施、仿生义体、基因与植入物'}
  '武器护甲与战斗'    = @{Icon='⚔';  Color='#d95f5f'; Desc='武器包、护甲装备、战斗机制与击杀反馈'}
  '载具与交通'        = @{Icon='▬';  Color='#b08fd0'; Desc='载具框架、车辆、火车、自行车等交通工具'}
  '建筑家具与装饰'    = @{Icon='▤';  Color='#b0a27c'; Desc='建筑、家具、地板、装饰与艺术物件'}
  '文化意识形态与宗教'= @{Icon='✧';  Color='#d0c08f'; Desc='意识形态、文化风格、宗教与派系文化'}
  '角色特质与成长'    = @{Icon='★';  Color='#e0d55a'; Desc='特质、技能、升级成长、军衔与 RPG 元素'}
  '社会关系与社交'    = @{Icon='☺';  Color='#9fd0e0'; Desc='人际关系、社交互动、浪漫与羁绊系统'}
  '经营管理与任务事件'= @{Icon='◈';  Color='#8fd0b0'; Desc='模拟经营、政务管理、悬赏任务与事件生成'}
  '生活品质与实用工具'= @{Icon='✎';  Color='#a8c090'; Desc='通用 QoL 改善、便捷工具、信息辅助'}
  '生物生态与纹理美化'= @{Icon='❋';  Color='#9dc0a0'; Desc='新生物、生态扩展与贴图美化'}
}

# ---- packageId -> category map (218) ----
$map = @{
  'ludeon.rimworld'='官方内容'; 'ludeon.rimworld.royalty'='官方内容'; 'ludeon.rimworld.ideology'='官方内容'
  'ludeon.rimworld.biotech'='官方内容'; 'ludeon.rimworld.anomaly'='官方内容'; 'ludeon.rimworld.odyssey'='官方内容'
  'zetrith.prepatcher'='框架与库'; 'brrainz.harmony'='框架与库'; 'vortex.purepatcher'='框架与库'
  'unlimitedhugs.hugslib'='框架与库'; 'imranfish.xmlextensions'='框架与库'
  'oskarpotocki.vanillafactionsexpanded.core'='框架与库'; 'smashphil.vehicleframework'='框架与库'
  'adaptive.storage.framework'='框架与库'; 'ancot.ancotlibrary'='框架与库'; 'v1024.ebframework'='框架与库'
  'latta.fsharp.core'='框架与库'; 'andromeda.milkyway'='框架与库'; '7f.alienworlds'='框架与库'
  'chezhou.framework.rimsimmanagementframework'='框架与库'; 'al9000.tastycore'='框架与库'
  'cgf1.gunsgalorecore'='框架与库'
  'boksu.rimthreadedttr'='性能与优化'; 'sz.yaopt'='性能与优化'
  'sk.hideunloadedmoderors'='性能与优化'; 'mlie.noversionwarning'='性能与优化'
  'ilyvion.loadingprogress'='界面与信息显示'; 'andromeda.nicebilltab'='界面与信息显示'
  'andromeda.nicehealthtab'='界面与信息显示'; 'andromeda.niceinventorytab'='界面与信息显示'
  'andromeda.niceplantsmenu'='界面与信息显示'; 'andromeda.niceresearchtab'='界面与信息显示'
  'jaxe.rimhud'='界面与信息显示'; 'andromeda.pawnquickinfo'='界面与信息显示'
  'hobtook.tradeui'='界面与信息显示'; 'timmyliang.tradehelper'='界面与信息显示'
  'andromeda.usefulmarks'='界面与信息显示'; 'alendio.daycounter'='界面与信息显示'
  'dawnsglow.qualcolor'='界面与信息显示'; 'sandy.rpgstyleinventory'='界面与信息显示'
  'jaxe.bubbles'='界面与信息显示'; 'kahdeg.killfeed'='界面与信息显示'
  'ruina99.battlefieldvkillfeed'='界面与信息显示'; 'zal.killcounter'='界面与信息显示'
  'automatic.bionicicons'='界面与信息显示'
  'aitranslation.pack'='汉化与翻译'; 'auto.aitranslation.core'='汉化与翻译'; 'latta.devl10n'='汉化与翻译'
  'baldman47.dev1.6.zh'='汉化与翻译'; 'xiaotudouzh.ebframework'='汉化与翻译'; 'endlessgrowth.zh'='汉化与翻译'
  'ilyvion.loadingprogress.zh'='汉化与翻译'; 'yk.bicyclesandmore.zh'='汉化与翻译'; 'lan.pawnquickinfo.zh'='汉化与翻译'
  'baldman47.nit.zh'='汉化与翻译'; 'rwzh.chinesepack.hugslib'='汉化与翻译'
  'rwzh.chinesepack.vanillafactionsexpandedcore'='汉化与翻译'; 'rwzh.chinesepack.ogrestack'='汉化与翻译'
  'rwzh.chinesepack.vfe'='汉化与翻译'; 'rwzh.chinesepack.rimtraits'='汉化与翻译'
  'asavikle.xml.extensions.cn'='汉化与翻译'; 'zh.mzmgow.sbz.neatstoragefridge'='汉化与翻译'
  'zh.adaptive.storage.neolithic'='汉化与翻译'; 'fruitytrump.chinesepack.7f.alienworlds'='汉化与翻译'
  'costel.customroomnames.zh'='汉化与翻译'; 'jaxe.bubbles.zh.hc'='汉化与翻译'; 'zh.kahdeg.killfeed'='汉化与翻译'
  'chinesepack.veterano.mythicages.megafaunabestiary'='汉化与翻译'
  'veterano.mythicages.megafaunabestiary.zhmiz'='汉化与翻译'
  'andromeda.nicebilltab.zh'='汉化与翻译'; 'day29zh.nicehealthtab'='汉化与翻译'
  'andromeda.niceinventorytab.zh'='汉化与翻译'; 'andromeda.niceplantsmenu.zh'='汉化与翻译'
  'fuopaxy.backpacksarenotbelts.zh'='汉化与翻译'; 'day29zh.qualcolor'='汉化与翻译'
  'cc.chinese.rimworld.questgenerationtweaks'='汉化与翻译'
  'meandssh.tsltn.zhcn.researchreinvented'='汉化与翻译'
  'cncg779.resourcegenerator.zhcn'='汉化与翻译'; 'shouer.chinesepack.rimhud'='汉化与翻译'
  'translator.daylight.rlevvcultivationplus.20260303172146'='汉化与翻译'
  'reisende.rimrelations.zh'='汉化与翻译'; 'zh.pb3n.simplewarrants'='汉化与翻译'
  'zh.mzmgow.vin.warehouse.storage'='汉化与翻译'; 'zh.sk.weaponmastery'='汉化与翻译'
  'apeanutazh.rockets.ranks'='汉化与翻译'; 'tianti.pla.customizeweapon'='汉化与翻译'
  'feliperathal.customizeweaponexpanded.zh'='汉化与翻译'; 'zh.cyberware2077'='汉化与翻译'
  'jellycreative.isekaileveling.chinesesimplified'='汉化与翻译'; 'jellycreative.isekaileveling.zh'='汉化与翻译'
  'local.niceresearchtab.zh'='汉化与翻译'; 'cc.chinese.rimworld.researchreinvented'='汉化与翻译'
  'meandssh.tsltn.zhcn.researchreinvented.steppingstones'='汉化与翻译'; 'zh.minzo.tastyarmory'='汉化与翻译'
  'princeofpluto.vehicles.trains.zh'='汉化与翻译'; 'hours.usefulmarks.zh'='汉化与翻译'
  'shavius.vce.zh'='汉化与翻译'; 'shavius.vie.zh'='汉化与翻译'; 'zh.vanillaexpanded.skills'='汉化与翻译'
  'shavius.vve.zh'='汉化与翻译'; 'shavius.vehicleframework.zh'='汉化与翻译'
  'nikeh.fsfabe.translation'='汉化与翻译'; 'poupun.guildfactionaddon.zh'='汉化与翻译'
  'mika.guildfactionaddon.chinesesimplified'='汉化与翻译'; 'cgf1.meleegalore.medieval.i.cn'='汉化与翻译'
  'sbz.neatstoragefridge'='存储与物流'; 'adaptive.ideology.storage'='存储与物流'
  'adaptive.primitivestorage'='存储与物流'; 'adaptive.simplestorage'='存储与物流'
  'adaptive.storage.neolithic'='存储与物流'; 'asf.deepstorage'='存储与物流'; 'vin.warehouse.storage'='存储与物流'
  'ogre.ogrestack'='存储与物流'
  'kearril.qualityrecastingtable'='生产制造与资源'; 'mlie.resourcegenerator'='生产制造与资源'
  'kattalisk.allproductionxp'='生产制造与资源'; 'trigger.universalblueprints'='生产制造与资源'
  'vanillaexpanded.vfeproduction'='生产制造与资源'; 'lotusland.weaponupgrade'='生产制造与资源'
  'vanillaexpanded.vcooke'='农业种植与食物'; 'vanillaexpanded.vcookebakery'='农业种植与食物'
  'vanillaexpanded.vcookehaute'='农业种植与食物'; 'vanillaexpanded.vcookestews'='农业种植与食物'
  'vanillaexpanded.vfefarming'='农业种植与食物'; 'daylight.rlevvcultivationplus'='农业种植与食物'
  'lingluo.giantcrop'='农业种植与食物'; 'lingluo.giantcropex'='农业种植与食物'
  'lysh.magicsoil'='农业种植与食物'; 'shenpc.vegetablebasketproject'='农业种植与食物'
  'shenpc.vegetablebasketproject.chefmeowstall'='农业种植与食物'
  'vanillaexpanded.vfemedical'='医疗义体与仿生'; 'frozensnowfox.advancedbionicsexpansion'='医疗义体与仿生'
  'assassinsbro.qualitybionicsremastered'='医疗义体与仿生'; 'cyberware.2077'='医疗义体与仿生'
  'zuoyao.universalimplantcompat'='医疗义体与仿生'; 'custom.evolutionarygene'='医疗义体与仿生'
  'custom.immortalregenerant'='医疗义体与仿生'; 'tixiv.whoshotmylegoff'='医疗义体与仿生'
  'tw.cwp.chineseweaponpack'='武器护甲与战斗'; 'issaczhuang.pla.armory'='武器护甲与战斗'
  'yimu.rimbattlefield'='武器护甲与战斗'; 'krkr.rule56'='武器护甲与战斗'
  'kazepsi.irbm'='武器护甲与战斗'; 'nekotee.irbmldr'='武器护甲与战斗'; 'drog.irbmretex'='武器护甲与战斗'
  'vortex.customizeweapon'='武器护甲与战斗'; 'feliperathal.customizeweaponexpanded'='武器护甲与战斗'
  'deko.weaponfitting'='武器护甲与战斗'; 'sk.weaponmastery'='武器护甲与战斗'
  'sunsetmoderteam.growthweaponsnew'='武器护甲与战斗'; 'almw.mod.azurlanemeleepack'='武器护甲与战斗'
  'vincentz.cpap'='武器护甲与战斗'; 'tianti.pla.weapon.fitting'='武器护甲与战斗'
  'aliza.vanillafiremodes'='武器护甲与战斗'; 'aliza.vanillameleemodes'='武器护甲与战斗'
  'memegoddess.runandgun'='武器护甲与战斗'; 'turnovus.submod.backpacksarenotbelts'='武器护甲与战斗'
  'al9000.fireandsteel'='武器护甲与战斗'; 'al9000.highmedieval'='武器护甲与战斗'
  'al9000.shiningarmor'='武器护甲与战斗'; 'al9000.sengoku'='武器护甲与战斗'
  'al9000.tastysteppe'='武器护甲与战斗'; 'cgf1.meleegalore.medieval.i'='武器护甲与战斗'
  'seti.transportation'='载具与交通'; 'oskarpotocki.vanillavehiclesexpanded'='载具与交通'
  'oskarpotocki.vanillavehiclesexpandedtier3'='载具与交通'; 'oskarpotocki.vanillavehiclesexpandedupgrades'='载具与交通'
  'oels.vehiclemapframework'='载具与交通'; 'chaotic.rvinterior'='载具与交通'
  'princeofpluto.vehicles.trains'='载具与交通'
  'vanillaexpanded.vfecore'='建筑家具与装饰'; 'vanillaexpanded.vfearchitect'='建筑家具与装饰'
  'vanillaexpanded.vfeart'='建筑家具与装饰'; 'vanillaexpanded.vfespacer'='建筑家具与装饰'
  'vanillaexpanded.vfepower'='建筑家具与装饰'; 'vanillaexpanded.vfesecurity'='建筑家具与装饰'
  'hope.terrainmaker'='建筑家具与装饰'; 'costel.customroomnames'='建筑家具与装饰'
  'lingluo.customizeroomrole'='建筑家具与装饰'; 'navyseal5.cleaningstation'='建筑家具与装饰'
  'lwop.corpsecollector.standalone'='建筑家具与装饰'
  'baal.cultureandfaith'='文化意识形态与宗教'; 'vanillaexpanded.ideo.dryads'='文化意识形态与宗教'
  'vanillaexpanded.viehar'='文化意识形态与宗教'; 'vanillaexpanded.ideo.relicsandartifacts'='文化意识形态与宗教'
  'vanillaexpanded.ideo.sophianstyle'='文化意识形态与宗教'; 'vanillaexpanded.viesas'='文化意识形态与宗教'
  'vanillaexpanded.vmemese'='文化意识形态与宗教'
  'slimesenpai.endlessgrowth'='角色特质与成长'; 'sierra.rt.generaltraits'='角色特质与成长'
  'rockets.ranks'='角色特质与成长'; 'vanillaexpanded.skills'='角色特质与成长'
  'jellycreative.isekaileveling'='角色特质与成长'; 'qing.more.traits2'='角色特质与成长'
  'codex.randompawnnamepool'='角色特质与成长'; 'lvkavidya.isekaileveling.forgepatch'='角色特质与成长'
  'poupun.guildfactionaddon'='角色特质与成长'
  'reo.rimrelations'='社会关系与社交'; 'wstlst.rimromance'='社会关系与社交'
  'golden.governancesystem'='经营管理与任务事件'; 'pb3n.simplewarrants'='经营管理与任务事件'
  'jaeger972.questgenerationtweaks'='经营管理与任务事件'; 'andromeda.legendaryboss'='经营管理与任务事件'
  'jianyuan.rsmf.expandedcustomerpack'='经营管理与任务事件'
  'jianyuan.rsmf.restockstaffexpander'='经营管理与任务事件'
  'jianyuan.rsmf.storagecapacitypatch'='经营管理与任务事件'; 'maiya.rimdigitallife.core'='经营管理与任务事件'
  'baal.partiesandcurrents'='经营管理与任务事件'; 'baal.peopleandrepublic'='经营管理与任务事件'
  'void.charactereditor'='生活品质与实用工具'; 'avilmask.commonsense'='生活品质与实用工具'
  'rabiosus.minesight'='生活品质与实用工具'; 'personalchronicle.archive'='生活品质与实用工具'
  'petetimessix.researchreinvented'='生活品质与实用工具'; 'petetimessix.researchreinvented.steppingstones'='生活品质与实用工具'
  'veterano.mythicages.megafaunabestiary'='生物生态与纹理美化'; 'cyberware.2077.retexture.lk'='生物生态与纹理美化'
  'bionicicons.hd'='生物生态与纹理美化'
}

# name overrides (official)
$nameOverride = @{
  'ludeon.rimworld'='RimWorld 本体'
  'ludeon.rimworld.royalty'='DLC 皇权 Royalty'
  'ludeon.rimworld.ideology'='DLC 文化 Ideology'
  'ludeon.rimworld.biotech'='DLC 生物科技 Biotech'
  'ludeon.rimworld.anomaly'='DLC 异象 Anomaly'
  'ludeon.rimworld.odyssey'='DLC 奥德赛 Odyssey'
  'personalchronicle.archive'='Personal Chronicle - 档案馆（本 Mod）'
}

# ---- Chinese descriptions map ----
$descCsv = Join-Path $dir 'desc_cn.tsv'
$descCnMap = @{}
foreach ($dl in [System.IO.File]::ReadAllLines($descCsv, [System.Text.Encoding]::UTF8)) {
  if (-not $dl.Trim()) { continue }
  $df = $dl -split "`t"
  if ($df.Count -ge 2) { $descCnMap[$df[0]] = $df[1] }
}

# ---- parse TSV ----
$lines = [System.IO.File]::ReadAllLines($inCsv)
$items = New-Object System.Collections.Generic.List[object]
for ($i = 1; $i -lt $lines.Length; $i++) {
  $ln = $lines[$i]
  if (-not $ln.Trim()) { continue }
  $f = $ln -split "`t"
  if ($f.Count -lt 8) { continue }
  $pkg = $f[0]
  if (-not $map.ContainsKey($pkg)) { $map[$pkg] = '生活品质与实用工具' }
  $cat = $map[$pkg]
  $name = $f[1]
  if ($nameOverride.ContainsKey($pkg)) { $name = $nameOverride[$pkg] }
  $items.Add([PSCustomObject]@{
    pkg=$pkg; name=$name; author=$f[2]; desc=$f[3]
    vers=$f[4]; deps=$f[5]; loadAfter=$f[6]; source=$f[7]; cat=$cat
    descCn=$descCnMap[$pkg]
  })
}

# ---- build JSON for JS ----
$arr = @()
foreach ($it in $items) {
  $arr += [PSCustomObject]@{
    pkg=$it.pkg; name=$it.name; author=$it.author; desc=$it.desc; descCn=$it.descCn
    vers=$it.vers; deps=$it.deps; source=$it.source; cat=$it.cat
  }
}
$json = $arr | ConvertTo-Json -Compress -Depth 2
$catsJson = @{}
foreach ($c in $catOrder) { $catsJson[$c] = $catMeta[$c] }
$catsJson2 = $catsJson | ConvertTo-Json -Compress -Depth 3

# counts
$nLocal = @($items | Where-Object { $_.source -like 'local:*' }).Count
$nWs = @($items | Where-Object { $_.source -like 'ws:*' }).Count

$html = @'
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>RimWorld Mod 列表详细分类报告</title>
<style>
:root { --bg:#0d1117; --panel:#161b22; --panel2:#1c2330; --line:#2d3748; --text:#e6edf3; --muted:#8b949e; --gold:#d4af37; }
* { box-sizing:border-box; margin:0; padding:0; }
body { background:var(--bg); color:var(--text); font-family:"Segoe UI","Microsoft YaHei",system-ui,sans-serif; line-height:1.5; }
.wrap { max-width:1280px; margin:0 auto; padding:28px 20px 80px; }
header { text-align:center; padding:34px 0 10px; }
header h1 { font-size:30px; color:var(--gold); letter-spacing:1px; }
header p { color:var(--muted); margin-top:8px; font-size:14px; }
.stats { display:flex; gap:14px; justify-content:center; flex-wrap:wrap; margin:26px 0 10px; }
.stat { background:var(--panel); border:1px solid var(--line); border-radius:12px; padding:14px 26px; min-width:120px; text-align:center; }
.stat b { display:block; font-size:26px; color:var(--gold); }
.stat span { font-size:12px; color:var(--muted); }
.toolbar { position:sticky; top:0; z-index:50; background:rgba(13,17,23,.94); backdrop-filter:blur(6px); padding:14px 0; border-bottom:1px solid var(--line); margin-bottom:22px; }
.toolbar .row { display:flex; gap:10px; align-items:center; flex-wrap:wrap; }
#search { flex:1; min-width:220px; background:var(--panel2); border:1px solid var(--line); color:var(--text); border-radius:8px; padding:9px 14px; font-size:14px; outline:none; }
#search:focus { border-color:var(--gold); }
.chips { display:flex; gap:8px; flex-wrap:wrap; margin-top:10px; }
.chip { background:var(--panel); border:1px solid var(--line); color:var(--muted); border-radius:999px; padding:5px 13px; font-size:12.5px; cursor:pointer; transition:.15s; user-select:none; }
.chip:hover { border-color:var(--gold); color:var(--text); }
.chip.on { background:var(--gold); border-color:var(--gold); color:#1a1400; font-weight:600; }
section.cat { margin-bottom:34px; }
.cat-head { display:flex; align-items:center; gap:12px; padding:12px 16px; border-radius:12px 12px 0 0; background:var(--panel); border:1px solid var(--line); border-bottom:none; }
.cat-head .ic { width:34px; height:34px; border-radius:9px; display:flex; align-items:center; justify-content:center; font-size:18px; color:#111; }
.cat-head h2 { font-size:18px; }
.cat-head .cnt { margin-left:auto; background:var(--panel2); border:1px solid var(--line); border-radius:999px; padding:2px 12px; font-size:12.5px; color:var(--muted); }
.cat-desc { padding:8px 16px; background:var(--panel); border:1px solid var(--line); border-top:none; color:var(--muted); font-size:13px; }
.grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(300px,1fr)); gap:12px; padding:16px; background:var(--panel); border:1px solid var(--line); border-top:none; border-radius:0 0 12px 12px; }
.card { background:var(--panel2); border:1px solid var(--line); border-radius:10px; padding:12px 14px; transition:.15s; position:relative; }
.card:hover { border-color:var(--gold); transform:translateY(-1px); }
.card .nm { font-size:14.5px; font-weight:600; color:var(--text); display:flex; align-items:center; gap:8px; }
.card .tag { font-size:10px; background:var(--line); color:var(--muted); border-radius:4px; padding:1px 6px; white-space:nowrap; }
.card .pid { font-family:Consolas,monospace; font-size:11px; color:var(--muted); margin-top:2px; }
.card .author { font-size:11.5px; color:var(--gold); margin-top:4px; }
.card .desc { font-size:12.5px; color:#c9d1d9; margin-top:7px; display:-webkit-box; -webkit-line-clamp:3; -webkit-box-orient:vertical; overflow:hidden; }
.card .foot { display:flex; gap:8px; margin-top:9px; align-items:center; }
.card .ver { font-size:10.5px; color:var(--muted); background:var(--line); border-radius:4px; padding:1px 7px; }
.card a { color:var(--gold); font-size:11.5px; text-decoration:none; margin-left:auto; }
.card a:hover { text-decoration:underline; }
.empty { color:var(--muted); text-align:center; padding:40px 0; display:none; font-size:15px; }
footer { text-align:center; color:var(--muted); font-size:12px; padding-top:26px; }
@media(max-width:720px){ .grid{grid-template-columns:1fr;} }
</style>
</head>
<body>
<div class="wrap">
<header>
  <h1>RimWorld Mod 列表详细分类报告</h1>
  <p>数据来源：ModsConfig.xml + 各 Mod About.xml（本地采集） · 生成于 2026-08-23 · 共启用 __N_TOTAL__ 个 Mod（官方 __N_OFFICIAL__ / 工坊 __N_WS__ / 本地 __N_LOCAL__）</p>
</header>

<div class="stats" id="stats"></div>

<div class="toolbar">
  <div class="row">
    <input id="search" type="text" placeholder="搜索名称 / 作者 / packageId / 描述…">
    <button class="chip" id="reset" style="padding:9px 16px;">显示全部</button>
  </div>
  <div class="chips" id="chips"></div>
</div>

<div id="cats"></div>
<div class="empty" id="empty">没有匹配的 Mod</div>

<footer>Personal Chronicle 项目配套环境报告 · 自动生成脚本：_scripts/_mod_audit/</footer>
</div>

<script>
const CATS = __CATS_JSON__;
const DATA = __DATA_JSON__;
let activeCat = null, query = '';

const catOrder = Object.keys(CATS);
const catCounts = {};
DATA.forEach(m => catCounts[m.cat] = (catCounts[m.cat]||0)+1);

// stats cards
const stats = [
  ['__N_TOTAL__','启用 Mod 总数'],
  ['__N_CATS__','功能分类'],
  ['__N_ZH__','汉化/翻译包'],
  ['__N_LIB__','框架与库'],
  ['__N_WS__','Steam 工坊来源'],
  ['__N_LOCAL__','本地 Mod 目录'],
];
document.getElementById('stats').innerHTML = stats.map(([v,l])=>
  `<div class="stat"><b>${v}</b><span>${l}</span></div>`).join('');

// chips
const chipsBox = document.getElementById('chips');
chipsBox.innerHTML = catOrder.map(c=>`<span class="chip" data-c="${c}">${CATS[c].Icon} ${c} <small>(${catCounts[c]||0})</small></span>`).join('');
chipsBox.querySelectorAll('.chip').forEach(ch=>ch.onclick=()=>{
  const c = ch.dataset.c;
  activeCat = (activeCat===c)?null:c;
  ch.classList.toggle('on', activeCat===c);
  render();
});

document.getElementById('reset').onclick = ()=>{ activeCat=null; query=''; document.getElementById('search').value='';
  chipsBox.querySelectorAll('.chip').forEach(ch=>ch.classList.remove('on')); render(); };

document.getElementById('search').addEventListener('input', e=>{ query=e.target.value.trim().toLowerCase(); render(); });

function esc(s){ return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

function wsUrl(src){ const m = /^ws:(\d+)$/.exec(src||''); return m?`https://steamcommunity.com/sharedfiles/filedetails/?id=${m[1]}`:null; }

function render(){
  const root = document.getElementById('cats');
  let html = '';
  let shown = 0;
  catOrder.forEach(c=>{
    const items = DATA.filter(m=>m.cat===c && (!activeCat||m.cat===activeCat));
    const filtered = items.filter(m=>{
      if(!query) return true;
      const hay = (m.name+' '+m.author+' '+m.pkg+' '+(m.descCn||m.desc)).toLowerCase();
      return hay.includes(query);
    });
    shown += filtered.length;
    const meta = CATS[c];
    html += `<section class="cat" id="cat-${c}" ${activeCat&&activeCat!==c?'style="display:none"':''}>
      <div class="cat-head"><div class="ic" style="background:${meta.Color}">${meta.Icon}</div><h2>${c}</h2><span class="cnt">${filtered.length} / ${items.length}</span></div>
      <div class="cat-desc">${meta.Desc}</div>
      <div class="grid">`;
    filtered.forEach(m=>{
      const url = wsUrl(m.source);
      const srcLabel = m.source.startsWith('local:') ? ('本地：'+m.source.slice(6)) : (url?'工坊':'未知');
      html += `<div class="card">
        <div class="nm">${esc(m.name)}</div>
        <div class="pid">${esc(m.pkg)}</div>
        ${m.author?`<div class="author">作者：${esc(m.author)}</div>`:''}
        ${(m.descCn||m.desc)?`<div class="desc" title="${esc(m.desc||'')}">${esc(m.descCn||m.desc)}</div>`:''}
        <div class="foot"><span class="ver">${m.vers?esc(m.vers):'—'}</span>${url?`<a href="${url}" target="_blank">Steam 页面 ↗</a>`:`<span style="font-size:11px;color:var(--muted)">${srcLabel}</span>`}</div>
      </div>`;
    });
    html += `</div></section>`;
  });
  root.innerHTML = html;
  document.getElementById('empty').style.display = shown? 'none':'block';
}
render();
</script>
</body>
</html>
'@

$nZh = @($items | Where-Object { $_.cat -eq '汉化与翻译' }).Count
$nLib = @($items | Where-Object { $_.cat -eq '框架与库' }).Count
$html = $html.Replace('__CATS_JSON__', $catsJson2).Replace('__DATA_JSON__', $json)
$html = $html.Replace('__N_TOTAL__', $items.Count.ToString())
$html = $html.Replace('__N_OFFICIAL__', ($items.Count - $nWs - $nLocal).ToString())
$html = $html.Replace('__N_WS__', $nWs.ToString())
$html = $html.Replace('__N_LOCAL__', $nLocal.ToString())
$html = $html.Replace('__N_CATS__', $catOrder.Count.ToString())
$html = $html.Replace('__N_ZH__', $nZh.ToString())
$html = $html.Replace('__N_LIB__', $nLib.ToString())

[System.IO.File]::WriteAllText($outHtml, $html, (New-Object System.Text.UTF8Encoding($true)))
Write-Host "OK -> $outHtml  ($($items.Count) items)"
