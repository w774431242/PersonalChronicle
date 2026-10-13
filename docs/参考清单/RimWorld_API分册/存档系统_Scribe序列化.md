# 存档系统：Scribe 序列化

> 来源：原总库 §40、§97。

## 40. Save / Scribe API

RimWorld 保存系统核心：

```text
IExposable
Scribe
Scribe_Values
Scribe_References
Scribe_Collections
Scribe_Deep
Scribe_Defs
ScribeExtractor
LookMode
LoadSaveMode
```

典型：

```csharp
public void ExposeData()
{
    Scribe_Values.Look(ref value, "value", 0);
    Scribe_References.Look(ref pawn, "pawn");
    Scribe_Collections.Look(ref list, "list", LookMode.Deep);
}
```

### 常用 Scribe 方法族

```text
Scribe_Values.Look
Scribe_References.Look
Scribe_Defs.Look
Scribe_Deep.Look
Scribe_Collections.Look
```

### 存档设计原则

- 使用稳定字段键。
- 新增字段必须提供默认值。
- 删除字段要考虑旧存档读取。
- 不保存可从 Def 重建的数据。
- 不直接序列化 Unity 对象。
- 尽量保存稳定引用而非运行时缓存。

## 97. 官方存档系统：ExposeData / Scribe

RimWorld 原生提供自己的序列化体系。

核心入口：

```csharp
public override void ExposeData()
{
    Scribe_Values.Look(ref myValue, "myValue");
    Scribe_Deep.Look(ref myObject, "myObject");
    Scribe_Collections.Look(ref myList, "myList");
}
```

核心类型族：

- `Scribe_Values`
- `Scribe_References`
- `Scribe_Deep`
- `Scribe_Collections`
- `Scribe_Defs`

适用位置：

- `GameComponent`
- `WorldComponent`
- `MapComponent`
- `ThingComp`
- `HediffComp`
- 自定义可保存对象

### 重要约束

保存数据应当：

1. 有稳定 key。
2. 有缺省值处理。
3. 考虑旧存档迁移。
4. 不依赖运行时瞬时引用。
5. 避免保存可由 Def 恢复的静态数据。
