using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MvcControllerScanner.Config;

/// <summary>
/// MVC控制器扫描配置项
/// </summary>
public class MvcScanOptions
{
    /// <summary>
    /// 目标程序集（默认为当前执行程序集）
    /// </summary>
    public Assembly TargetAssembly { get; set; } = Assembly.GetExecutingAssembly();

    /// <summary>
    /// MVC控制器基类（默认为ControllerBase，可改为自定义基类如SRController）
    /// </summary>
    public Type ControllerBaseType { get; set; } = typeof(ControllerBase);

    /// <summary>
    /// 控制器源码目录（默认为项目根目录/Controllers）
    /// </summary>
    public string ControllerSourceDir { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Controllers");

    /// <summary>
    /// 报告输出目录（默认为项目根目录/Docs/MvcScan）
    /// </summary>
    public string ReportOutputDir { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "MvcScan");

    /// <summary>
    /// 是否生成Markdown报告
    /// </summary>
    public bool GenerateMarkdownReport { get; set; } = true;

    /// <summary>
    /// 是否生成JSON报告
    /// </summary>
    public bool GenerateJsonReport { get; set; } = true;
}
