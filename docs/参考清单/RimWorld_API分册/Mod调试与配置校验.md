# Mod 调试与配置校验

> 来源：原总库 §53、§54、§99–§101。

## 53. Log / Debug API

常见：

```csharp
Log.Message("...");
Log.Warning("...");
Log.Error("...");
Log.ErrorOnce("...", hash);
```

Debug 体系：

```text
DebugAction
DebugTools
DebugWindowsOpener
```

可通过特性注册/扩展 Debug Action。

## 54. ConfigErrors

Def 可实现配置错误报告：

```csharp
public override IEnumerable<string> ConfigErrors()
{
    foreach (var error in base.ConfigErrors())
        yield return error;

    if (value < 0)
        yield return "value must be >= 0";
}
```

用途：

- Mod 启动时主动检测错误
- 防止错误状态进入游戏
- 给用户明确日志

## 99. 官方开发模式 / Debug API

### 99.1 Development Mode

RimWorld 原生 Development Mode 提供：

- Debug Actions
- Debug Outputs
- Inspector
- 路径可视化
- Job 调试
- Incident 调试
- Pawn 调试
- 翻译错误检查
- 大量开发期生成/修改操作

这不是普通玩家功能意义上的“作弊 API”，对于 Mod 开发实际属于官方内置测试平台。

来源：
https://rimworldwiki.com/wiki/Development_mode
https://rimworldwiki.com/wiki/Modding_Tutorials/Testing_mods

### 99.2 DebugAction

可以把任意静态方法注册进开发模式菜单：

```csharp
[DebugAction(
    "MyMod",
    "Do Something",
    actionType = DebugActionType.Action,
    allowedGameStates = AllowedGameStates.PlayingOnMap)]
public static void DoSomething()
{
}
```

可用动作类别包括：

- `Action`
- `ToolMap`
- `ToolMapForPawns`
- `ToolWorld`

`AllowedGameStates` 可以组合使用。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/DebugActions

## 100. TweakValues：官方开发期参数调试能力

TweakValues 用于快速修改开发参数，非常适合：

- 数值平衡
- UI 尺寸
- 测试参数
- AI 参数验证
- 性能调试
- 快速迭代

它属于**开发/调试设施**，而不是正式游戏数据存储机制。

工程中建议：

```text
TweakValues
   ↓
开发调试
   ↓
验证最终数值
   ↓
回写 Def / Settings / 常量配置
```

不要把 TweakValues 当成正式 Mod 配置数据库。

## 101. 官方支持的错误报告 / 配置错误接口

Mod 可以通过游戏日志系统报告：

- Error
- Warning
- Message
- 配置错误
- 加载错误
- 翻译缺失
- Def 配置问题

推荐在启动阶段尽早发现错误，而不是让异常延迟到用户实际点击功能时才出现。

对于 Mod 工程，可建立：

```text
Config Validation
   ↓
Def Validation
   ↓
Dependency Validation
   ↓
Runtime Validation
```

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Config_Errors
