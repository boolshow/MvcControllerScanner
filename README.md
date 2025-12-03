# MvcControllerScanner 使用文档

## 简介

MvcControllerScanner 是一款基于 Roslyn 编译器的 MVC 控制器扫描工具，旨在自动分析控制器源代码，提取关键信息并生成标准化报告。工具支持提取 Action 详情、返回语句类型、模型结构等核心数据，输出 Markdown 和 JSON 格式报告，帮助开发者快速梳理项目接口结构，提升开发与协作效率。

## 配置选项

通过 `MvcScanOptions` 类配置扫描行为，核心配置项如下表所示：



| 配置项                      | 说明               | 默认值                  |
| ------------------------ | ---------------- | -------------------- |
| `TargetAssembly`         | 目标程序集（用于模型类型解析）  | 当前执行程序集              |
| `ControllerBaseType`     | MVC 控制器基类        | `ControllerBase`     |
| `ControllerSourceDir`    | 控制器源码目录          | 项目根目录 / Controllers  |
| `ReportOutputDir`        | 报告输出目录           | 项目根目录 / Docs/MvcScan |
| `GenerateMarkdownReport` | 是否生成 Markdown 报告 | `true`               |
| `GenerateJsonReport`     | 是否生成 JSON 报告     | `true`               |

## 使用步骤

### 1. 安装与引用



* 方式一：将 MvcControllerScanner 项目直接引用到你的 MVC 应用程序中


### 2. 基本使用示例



```
using MvcControllerScanner.Config;

using MvcControllerScanner.Core;

// 初始化配置（可选，使用默认配置可跳过）

var options = new MvcScanOptions

{

   // 自定义控制器目录（若不在默认位置）

   ControllerSourceDir = Path.Combine(Directory.GetCurrentDirectory(), "Controllers"),

   // 自定义报告输出目录

   ReportOutputDir = Path.Combine(Directory.GetCurrentDirectory(), "Reports", "Mvc")

};

// 创建扫描器实例（基于 Roslyn 实现）

var scanner = new MvcControllerScannerCoreRoslyn(options);

// 执行扫描

scanner.Scan();

// 获取扫描结果（可选，用于自定义处理）

var scanResults = scanner.ScanResult;
```

### 3. 高级配置



```
using MvcControllerScanner.Config;

using MvcControllerScanner.Core;

var options = new MvcScanOptions

{

   // 配置自定义控制器基类（如项目使用自定义基类而非默认 ControllerBase）

   ControllerBaseType = typeof(MyCustomController),

  

   // 仅生成 JSON 报告（关闭 Markdown 报告）

   GenerateMarkdownReport = false,

   GenerateJsonReport = true,

  

   // 指定目标程序集（确保模型类型能正确解析，尤其多程序集项目）

   TargetAssembly = typeof(Program).Assembly,

  

   // 其他可选配置...

   ControllerSourceDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "Controllers"),

   ReportOutputDir = Path.Combine(Directory.GetCurrentDirectory(), "docs", "api")

};

var scanner = new MvcControllerScannerCoreRoslyn(options);

scanner.Scan();
```

## 扫描内容说明

工具会自动提取控制器及 Action 的以下核心信息：

### 1. 控制器与 Action 基本信息



* 控制器名称（自动去除后缀 "Controller"）

* Action 方法名

* HTTP 方法（通过 `[HttpGet]`/`[HttpPost]`/`[HttpPut]`/`[HttpDelete]` 等特性识别）

* 授权状态（通过 `[Authorize]` 特性识别是否需要授权）

* 方法返回值类型

### 2. 返回语句分析



* 返回类型分类：`Forbid()`、`NotFound()`、`View(...)`、`Redirect(...)`、`RedirectToAction(...)`、`Other`

* 原始返回语句源代码

* 重定向目标（针对 `Redirect`/`RedirectToAction` 类型）

* 模型类型及示例数据（针对 `View` 返回类型）

### 3. 模型结构分析



* 模型类型全名称

* 模型属性名称及类型

* 自动生成示例 JSON 数据（支持嵌套对象，默认最大深度 10 层）

## 报告说明

扫描完成后，工具会在 `ReportOutputDir` 目录下生成对应格式的报告文件：

### 1. Markdown 报告



* 文件名：`MvcScanReport_Roslyn.md`

* 核心内容：


  * 按「控制器 → Action」的层级结构组织

  * 每个 Action 的 HTTP 方法、授权状态、返回值类型

  * 详细的返回语句信息（含原始代码）

  * 模型类型说明及示例 JSON 数据

* 适用场景：团队协作文档、接口说明文档、项目交接资料

### 2. JSON 报告



* 文件名：`MvcScanResult_Roslyn.json`

* 核心特点：


  * 结构化数据格式，便于程序二次处理

  * 包含所有扫描提取的原始信息

* 适用场景：自动化测试、接口文档生成、自定义工具集成

## 注意事项



1. 确保 `ControllerSourceDir` 配置正确，且目录下包含 `.cs` 格式的控制器源代码文件

2. 复杂模型类型解析依赖 `TargetAssembly` 配置，需指定包含模型定义的程序集（如 `typeof(Program).Assembly` 或模型所在程序集）

3. 嵌套对象的示例数据生成默认限制为 5 层，避免递归过深导致性能问题

4. 目前支持识别的返回类型仅包含：`Forbid()`、`NotFound()`、`View(...)`、`Redirect(...)`、`RedirectToAction(...)`，其他返回类型会归类为 `Other`

5. 工具会自动创建不存在的 `ReportOutputDir` 目录，无需手动创建

## 常见问题（FAQ）

### Q：扫描不到控制器？

A：检查以下两点：



1. 控制器类名是否以 "Controller" 结尾（MVC 控制器命名规范）

2. `ControllerSourceDir` 配置是否指向正确的控制器源代码目录

### Q：模型类型显示为 null 或解析失败？

A：确保 `TargetAssembly` 已正确设置为包含模型类型定义的程序集，避免跨程序集模型未被识别。

### Q：报告文件未生成？

A：检查：



1. 扫描过程是否无异常（可添加日志输出排查）

2. `GenerateMarkdownReport`/`GenerateJsonReport` 配置是否为 `true`

3. 目标目录是否有写入权限

### Q：嵌套对象的示例数据不完整？

A：默认嵌套深度限制为 5 层，若需调整可修改工具内部配置（当前版本暂不支持外部配置）。

> （注：文档部分内容可能由 AI 生成）