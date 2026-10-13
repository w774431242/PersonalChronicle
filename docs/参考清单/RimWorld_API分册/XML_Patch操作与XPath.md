# XML Patch 操作与 XPath

> 来源：原总库 §45、§46。

## 45. XML PatchOperation API

当前 Wiki 列出的主要操作类型：

```text
PatchOperationAdd
PatchOperationInsert
PatchOperationRemove
PatchOperationReplace
PatchOperationAttributeAdd
PatchOperationAttributeSet
PatchOperationAttributeRemove
PatchOperationSequence
PatchOperationAddModExtension
PatchOperationSetName
```

来源：
<https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations>

典型：

```xml
<Operation Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="Steel"]</xpath>
    <value>
        <someField>123</someField>
    </value>
</Operation>
```

## 46. XPath Patch 核心能力

核心定位：

```text
选择节点
新增节点
删除节点
替换节点
修改属性
新增 ModExtension
改变节点名
```

常用 XPath：

```text
Defs/ThingDef[defName="X"]
Defs/ThingDef[thingCategories/li="Building"]
Defs/RecipeDef[defName="X"]
```

### 推荐命名实践

```xml
<xpath>Defs/ThingDef[defName="MyTarget"]</xpath>
```

避免：

```xml
<xpath>Defs/ThingDef[3]</xpath>
```

因为加载顺序、DLC、其他 Mod 均可能改变索引。
