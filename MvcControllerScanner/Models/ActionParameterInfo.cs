using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcControllerScanner.Models;

public class ActionParameterInfo
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public Type? ResolvedType { get; set; }
    public string? ResolvedTypeName => ResolvedType?.FullName; // 序列化用
    public List<string> Attributes { get; set; } = new List<string>();
    public object? ExampleValue { get; set; } // 用 BuildFlatDefaultModel 生成
}
