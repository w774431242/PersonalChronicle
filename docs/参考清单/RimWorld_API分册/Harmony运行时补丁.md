# Harmony 运行时补丁

> 来源：原总库 §43、§44。

## 43. Harmony：修改原版代码的主流机制

Harmony 不是 Ludeon 自有 API，而是社区广泛使用的运行时补丁库。

基本结构：

```csharp
[HarmonyPatch(typeof(SomeClass))]
[HarmonyPatch(nameof(SomeClass.SomeMethod))]
static class SomePatch
{
    static void Prefix(...)
    {
    }

    static void Postfix(...)
    {
    }
}
```

常见 Patch 类型：

```text
Prefix
Postfix
Transpiler
Finalizer
```

特殊注入参数：

```text
__instance
__result
__state
__args
```

用途：

- 修改原版行为
- 监听原版方法
- 修改返回值
- 插入逻辑
- 与其他 Mod 做条件兼容

Wiki：
<https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony>

## 44. Harmony 使用优先级

Mod 开发最佳实践不是“所有问题都 Harmony”。

推荐优先级：

```text
1. XML Def
2. PatchOperation
3. DefModExtension
4. ThingComp / HediffComp
5. Game/World/MapComponent
6. 新建继承类
7. Extension Method
8. Harmony Prefix/Postfix
9. Harmony Transpiler
10. 反射/内部字段访问
```

Harmony 教程也明确建议在可用的情况下优先考虑 subclass、ThingComp、MapComponent 等低冲突方式。
