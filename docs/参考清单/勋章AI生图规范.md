# PersonalChronicle 勋章 AI 生图标准规范

> 用途：直接交给 AI 生图 Agent / 制图 Agent，保证全勋章家族视觉一致。
> 核心原则：**结构统一 → 材质统一 → 工艺统一 → 构图统一 → 仅主题变化。**
> 即保持“勋章家族风格”一致，后续无论生产劳动 / 工程 / 医学 / 农业 / 战斗 / 科研 / 矿业 / 传奇勋章，都套同一套档案馆视觉体系。

---

## 一、生产规则（统一视觉规则）

所有勋章必须遵守：

1. **风格**：RimWorld 2D 游戏资产；正面视角、居中构图、清晰轮廓、高辨识度；不做照片、场景图、Logo。
2. **结构**：上方绶带/挂扣 → 中部主体徽章 → 内部主题图案/人物/职业符号 → 下方主标题或装饰；整体稳定、完整、对称。
3. **金属工艺**：金属浮雕、立体压铸、清晰高光与阴影、边缘有厚度；禁止塑料感。
4. **中国荣誉体系符号**：五角星、麦穗、祥云、回纹、齿轮等，按主题取舍不强行全加；保持庄重、荣誉、工业化风格。
5. **主题差异**：
   - 职业勋章：人物 + 职业工具/符号
   - 成就勋章：成就象征 + 荣誉符号
   - 战斗勋章：战斗/武器/防御象征
   - 科技勋章：机械、电路、实验、能源等
   - 农业勋章：作物、麦穗、农具等
   - 特殊勋章：按主题自由设计，但必须保持母版风格
6. **等级（金/银/铜）**：结构尽量一致，仅改变材质与少量等级装饰。金=高亮黄金，银=明亮银色，铜=深铜/青铜。
7. **背景（强制透明）**：**必须含透明通道（RGBA Alpha）并彻底移除背景**；孤立于透明画布、无环境、无填充；禁止白底、黑底、场景背景、桌面、阴影背景。
8. **文字**：中文标题用厚重、立体、浮雕效果；AI 易生成错字时优先出无文字版本，后期补字；禁止随机文字、乱码、错误中文。
9. **资产质量**：AI 源图 1024×1024；最终母版 512×512；游戏可缩 256/128/64；缩小后必须仍可辨识主题。

10. **姿势与朝向统一**：全家族勋章的朝向与人物姿势必须一致——一律正面正交视角、主体正对镜头、上下不颠倒、无旋转侧倾；人物均为正面直立居中半身像（胸以上），武器/工具对称垂直摆放；各系列仅换主题内容，姿势朝向保持不变。

---

## 二、通用提示词模板

新勋章只需替换 **【】** 里的内容。

### 正面提示词（Master Prompt）

```text
【RIMWORLD MEDAL MASTER PROMPT】

RimWorld 2D game asset, ceremonial honor medal,
front-facing orthographic view, centered composition, consistent upright front-facing orientation and pose across the whole medal family,
clean silhouette, highly recognizable, symmetrical design.

A premium 【勋章类型】 honor medal representing 【主题】.

Main medal:
【主体形状】,
layered metallic medal construction,
deep bas-relief,
3D sculpted metal,
precision engraved details,
beveled edges,
realistic metallic highlights and shadows.

Central design:
【核心人物/核心图案】.

Symbolic elements:
【职业/主题符号】.

Decorative elements:
【五角星/麦穗/祥云/齿轮/回纹等】,
traditional Chinese ceremonial ornament,
balanced symmetrical decoration.

Ribbon:
ceremonial medal ribbon,
【绶带颜色】,
metallic gold edging,
ornate suspension bar,
Chinese geometric decorative pattern.

Material:
【gold / silver / bronze】,
premium polished metal,
dimensional relief,
strong metallic highlights,
deep engraved shadows.

Typography:
large embossed Chinese title “【主标题】”,
formal ceremonial Chinese typography,
raised metallic lettering.

Style:
Chinese industrial honor medal,
prestigious ceremonial design,
stylized realistic 3D relief,
high visual clarity,
dense but readable details,
RimWorld-compatible game asset.

Transparent background,
isolated object,
mandatory RGBA alpha channel,
background fully removed,
no environment,
no fill.
```

### 配套负面提示词（Negative Prompt）

```text
no background, no white background, no black background, no environment, no table,
no hand, no person holding medal, no photograph, no realistic scene, no plastic,
no flat vector, no cartoon, no anime, no western medieval heraldry, no fantasy armor,
no crown, no random symbols, no random text, no incorrect Chinese characters,
no watermark, no signature, no perspective distortion, no tilted medal, no side view,
no asymmetry, no cropped object, no missing parts, no excessive tiny details
```

### 快速填表（交给 Agent 的极简输入）

只需提供以下字段，Agent 自动套用上面的 Master Prompt + Negative Prompt：

```text
勋章类型：【职业荣誉勋章 / 成就勋章 / 战斗勋章 / 科技勋章 / 农业勋章 / 特殊勋章】
主题：【如 首席机械工程师】
等级：【金 / 银 / 铜】
核心人物：【如 机械工程师】
核心符号：【如 大型齿轮、机械臂、精密工具】
辅助符号：【如 五角星、麦穗、祥云】
主标题：【如 工程荣誉】
材质：【高级抛光黄金 / 明亮银色 / 深铜】
绶带颜色：【如 深红金边】
背景：完全透明
视角：正面正交
```
