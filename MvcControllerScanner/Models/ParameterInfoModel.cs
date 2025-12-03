using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcControllerScanner.Models;

// 请求参数结构
public class ParameterInfoModel
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsOptional { get; set; }
    public object? DefaultValue { get; set; }
    public string Source { get; set; } = "Unknown"; // Body / Query / Route
    // 对象类型参数的属性及默认值
    public Dictionary<string, object?>? Properties { get; set; }
}
