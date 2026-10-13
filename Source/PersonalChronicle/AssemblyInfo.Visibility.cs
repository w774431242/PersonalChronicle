using System.Runtime.CompilerServices;

// ── 测试可见性（唯一职责）────────────────────────────────────────────────────
//
// 只把 **internal** 暴露给测试工程，不开 public：
//   面板的格式化/归一化规则、版式比例常量属于实现细节，不是对外契约。
//   若做成 public，版本敏感类型与版式数字会被钉进公开 API 表面，
//   第三方一旦依赖就再也改不动（本工程「可选参数 ≠ 二进制兼容」同源教训）。
//
// ⚠ 为什么是显式源文件而不是 csproj 的 <AssemblyAttribute> 项：
//   本工程 GenerateAssemblyInfo=false ⇒ 该 MSBuild 项属于被禁用的属性生成管线，
//   加了**完全不生效且不报错**（实测在 DLL 里搜不到 InternalsVisibleTo 字符串，
//   表现为测试工程 CS0122「不可访问」且原因不明显）。显式源文件走正常 Compile 路径。
//
// ⚠ 目标程序集名必须与 Tests/PersonalChronicle.Tests.csproj 的 <AssemblyName> 完全一致。
[assembly: InternalsVisibleTo("PersonalChronicle.Tests")]
