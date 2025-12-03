using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcControllerScanner.Models;

/// <summary>
/// MVC控制器Action信息
/// </summary>
public class ControllerActionInfo
{
    /// <summary>控制器名（去掉Controller后缀）</summary>
    public string ControllerName { get; set; } = string.Empty;

    /// <summary>Action方法名</summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>HTTP请求方法</summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>是否需要授权</summary>
    public bool IsAuthorized { get; set; }

    /// <summary>方法返回值类型</summary>
    public string MethodReturnType { get; set; } = string.Empty;

    /// <summary>所有返回语句</summary>
    public List<ReturnStatementInfo> ReturnStatements { get; set; } = [];

    public List<ParameterInfoModel> Parameters { get; set; } = [];
}


