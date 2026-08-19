// ============================================================
// 批量去白底透明化 v2 —— PersonalChronicle 勋章图 (Medal_*.png)
// 修复：v1 用魔棒 Action Manager 在部分 PS 版本选不中白底。
//       v2 改用「色彩范围 / 选白色」(Color Range, ClrR) 稳定事件，
//       并设连续=false，确保所有白底一次性选中后删除变透明。
// 用法：Photoshop 文件 → 脚本 → 浏览 → 选本文件
// ============================================================

var FADE      = 120;   // 色彩范围「颜色容差/模糊」等效：0~200，越大选得越宽（白偏灰也选）
var INVERT    = false; // 选白色=false；选黑色=true（勋章是白底，保持 false）
var DEST_SUB  = "_bak";

var mediasDir = new Folder("e:/SteamLibrary/steamapps/common/RimWorld/11/PersonalChronicle/Textures/Medals");
if (!mediasDir.exists) { alert("目录不存在:\n" + mediasDir.fsName); throw new Error("no dir"); }

var bakDir = new Folder(mediasDir.fullName + "/" + DEST_SUB);
if (!bakDir.exists) bakDir.create();

var files = mediasDir.getFiles(function(f) {
    if (!(f instanceof File)) return false;
    var n = f.name.toLowerCase();
    return n.indexOf("medal_") === 0 && n.slice(-4) === ".png";
});
if (files.length === 0) { alert("未找到 Medal_*.png"); throw new Error("empty"); }

var ok = 0, skip = 0;

for (var i = 0; i < files.length; i++) {
    var src = files[i];
    (new File(bakDir.fullName + "/" + src.name)).remove();
    src.copy(new File(bakDir.fullName + "/" + src.name));

    var doc = app.open(src);
    if (!doc) { skip++; continue; }
    try {
        // 背景解锁
        if (doc.backgroundLayer) {
            doc.backgroundLayer.isBackgroundLayer = false;
        } else if (doc.layers[0] && doc.layers[0].isBackgroundLayer) {
            doc.layers[0].isBackgroundLayer = false;
        }

        // 色彩范围选白色
        colorRangeWhite(doc, FADE, INVERT);

        if (!doc.selection.isEmpty) {
            doc.selection.clear();   // 白底 -> 透明
        }
        doc.selection.deselect();

        var opts = new PNGSaveOptions();
        opts.interlace = false;
        doc.saveAs(src, opts, true, Extension.LOWERCASE);
        ok++;
    } catch (e) {
        skip++;
        $.writeln("FAIL " + src.name + ": " + e.message);
    } finally {
        doc.close(SaveOptions.DONOTSAVECHANGES);
    }
}

alert("完成：成功 " + ok + " 张，跳过 " + skip + " 张\n原图备份于 Medals/_bak/\n色彩范围容差=" + FADE);

// ---- 色彩范围选白色（Action Manager: ClrR） ----
function colorRangeWhite(doc, fade, invert) {
    var desc = new ActionDescriptor();
    var ref = new ActionReference();
    ref.putProperty(charIDToTypeID("Chnl"), charIDToTypeID("fsel")); // 选选区通道
    desc.putReference(charIDToTypeID("null"), ref);

    var cRng = new ActionDescriptor();
    // 颜色范围：选「白色」(ClrW)
    cRng.putEnumerated(charIDToTypeID("Md  "),
                       charIDToTypeID("ClrR"),
                       charIDToTypeID("ClrW"));
    // 容差/模糊等效
    cRng.putInteger(charIDToTypeID("Fz  "), fade);
    // 是否反相（选黑色时 true）
    cRng.putBoolean(charIDToTypeID("Invr"), invert);
    // 连续：false = 所有白底都选（不限于连通块）
    cRng.putBoolean(charIDToTypeID("Cntg"), false);

    desc.putObject(charIDToTypeID("T   "), charIDToTypeID("ClrR"), cRng);
    try { executeAction(charIDToTypeID("setd"), desc, DialogModes.NO); }
    catch (e) { $.writeln("colorRange fail: " + e.message); }
}
