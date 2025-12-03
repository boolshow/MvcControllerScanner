using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MvcControllerScanner.Config;
using MvcControllerScanner.Extensions;
using MvcControllerScanner.Models;

namespace MvcControllerScanner.Core;

public class MvcControllerScannerCoreRoslyn
{
    private readonly MvcScanOptions _options;

    private readonly JsonSerializerOptions _jsonSerializer = new() { WriteIndented = true };
    public List<ControllerActionInfo> ScanResult { get; private set; } = [];

    public MvcControllerScannerCoreRoslyn(MvcScanOptions? options = null)
    {
        _options = options ?? new MvcScanOptions();
        if (_options.TargetAssembly == null)
            throw new ArgumentNullException(null, nameof(_options.TargetAssembly));
        if (!Directory.Exists(_options.ControllerSourceDir))
            Directory.CreateDirectory(_options.ControllerSourceDir);
        if (!Directory.Exists(_options.ReportOutputDir))
            Directory.CreateDirectory(_options.ReportOutputDir);
    }

    public void Scan()
    {
        ScanResult.Clear();
        Console.WriteLine("开始使用 Roslyn 扫描 MVC 控制器源代码...");

        var files = Directory.EnumerateFiles(_options.ControllerSourceDir, "*.cs", SearchOption.TopDirectoryOnly).ToList();
        if (files.Count == 0)
        {
            Console.WriteLine("Controller 源文件夹为空：" + _options.ControllerSourceDir);
            return;
        }

        foreach (string? filePath in files)
        {
            string source = File.ReadAllText(filePath, Encoding.UTF8);
            ProcessSourceFile(source);
        }

        GenerateReports();
        Console.WriteLine($"扫描完成！共找到 {ScanResult.Sum(s => Math.Max(1, s.ReturnStatements.Count))} 个返回项，覆盖 {ScanResult.Count} 个 Action。");
    }

    private void ProcessSourceFile(string sourceCode)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
        SyntaxNode root = tree.GetRoot();
        IEnumerable<ClassDeclarationSyntax> classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (ClassDeclarationSyntax cls in classNodes)
        {
            string className = cls.Identifier.Text;
            if (!className.EndsWith("Controller")) continue;
            string controllerName = className.Replace("Controller", "");

            IEnumerable<MethodDeclarationSyntax> methods = cls.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (MethodDeclarationSyntax method in methods)
            {
                if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) continue;

                var actionInfo = new ControllerActionInfo
                {
                    ControllerName = controllerName,
                    ActionName = method.Identifier.Text,
                    HttpMethod = GetHttpMethodFromAttributes(method.AttributeLists),
                    IsAuthorized = HasAttribute(method.AttributeLists, "Authorize"),
                    MethodReturnType = method.ReturnType.ToString()
                };

                foreach (ParameterSyntax param in method.ParameterList.Parameters)
                {
                    string source = InferParameterSource(param);
                    if (source == "Service") continue;

                    var paramInfo = new ParameterInfoModel
                    {
                        Name = param.Identifier.Text,
                        Type = param.Type?.ToString() ?? "Unknown",
                        IsOptional = param.Default != null,
                        DefaultValue = param.Default != null ? GuessExampleValueFromExpression(param.Default.Value) : null,
                        Source = source
                    };

                    Type? resolvedType = ResolveType(paramInfo.Type);
                    if (resolvedType != null)
                    {
                        paramInfo.Properties = SampleModelBuilder.Build(resolvedType) as Dictionary<string, object?> ?? [];
                    }

                    actionInfo.Parameters.Add(paramInfo);
                }

                var returns = method.DescendantNodes().OfType<ReturnStatementSyntax>().ToList();
                foreach (ReturnStatementSyntax ret in returns)
                {
                    ReturnStatementInfo info = ParseReturnStatement(ret);
                    info.SourceCode = ret.ToString();

                    if (info.ReturnType == "View" && info.ModelType != null)
                    {
                        Type? modelType = ResolveType(info.ModelType);
                        if (modelType != null)
                        {
                            info.ExampleModel = SampleModelBuilder.Build(modelType);
                        }
                    }

                    actionInfo.ReturnStatements.Add(info);
                }

                ScanResult.Add(actionInfo);
            }
        }
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, string name)
    {
        foreach (AttributeListSyntax list in attributeLists)
            foreach (AttributeSyntax attr in list.Attributes)
            {
                string n = attr.Name.ToString();
                if (n.EndsWith(name) || n.EndsWith(name + "Attribute")) return true;
            }
        return false;
    }

    private static string GetHttpMethodFromAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (AttributeListSyntax list in attributeLists)
            foreach (AttributeSyntax attr in list.Attributes)
            {
                string n = attr.Name.ToString();
                if (n.Contains("HttpGet")) return "GET";
                if (n.Contains("HttpPost")) return "POST";
                if (n.Contains("HttpPut")) return "PUT";
                if (n.Contains("HttpDelete")) return "DELETE";
            }
        return "UNKNOWN";
    }

    private ReturnStatementInfo ParseReturnStatement(ReturnStatementSyntax ret)
    {
        var info = new ReturnStatementInfo();
        ExpressionSyntax? expr = ret.Expression;
        if (expr == null)
        {
            info.ReturnType = "Unknown";
            return info;
        }

        if (expr is InvocationExpressionSyntax inv)
        {
            string invoked = inv.Expression.ToString();

            if (invoked.EndsWith("Forbid") || invoked == "Forbid") { info.ReturnType = "Forbid"; return info; }
            if (invoked.EndsWith("NotFound") || invoked == "NotFound") { info.ReturnType = "NotFound"; return info; }

            if (invoked.Contains("Redirect"))
            {
                info.ReturnType = invoked.Contains("RedirectToAction") ? "RedirectToAction" : "Redirect";
                var dict = new Dictionary<string, object> { ["ReturnType"] = info.ReturnType };

                if (inv.ArgumentList.Arguments.Count > 0)
                {
                    string[] args = [.. inv.ArgumentList.Arguments.Select(a =>
                    {
                        if (a.Expression is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                            return lit.Token.ValueText;
                        return a.ToString();
                    })];

                    if (info.ReturnType == "Redirect")
                    {
                        dict["Url"] = args.FirstOrDefault() ?? "UnknownUrl";
                    }
                    else
                    {
                        dict["Action"] = args.ElementAtOrDefault(0) ?? "UnknownAction";
                        dict["Controller"] = args.ElementAtOrDefault(1) ?? "UnknownController";
                        dict["RouteValues"] = new Dictionary<string, object>();
                    }
                }

                info.ExampleModel = dict;
                return info;
            }

            if (invoked.EndsWith("View") || invoked == "View")
            {
                info.ReturnType = "View";
                ExpressionSyntax? arg = inv.ArgumentList.Arguments.FirstOrDefault()?.Expression;

                Type? modelType = null;
                if (arg != null)
                {
                    if (arg is ObjectCreationExpressionSyntax obj)
                        modelType = ResolveType(obj.Type.ToString());
                    else if (arg is IdentifierNameSyntax id)
                        modelType = InferModelTypeFromIdentifier(id);
                }

                if (modelType != null)
                {
                    info.ModelType = modelType.FullName!;
                    info.ExampleModel = SampleModelBuilder.Build(modelType);
                }

                return info;
            }
        }

        info.ReturnType = "Other";
        info.SourceCode = ret.ToString();
        return info;
    }

    private static object GuessExampleValueFromExpression(ExpressionSyntax expr)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax lit:
                if (lit.IsKind(SyntaxKind.StringLiteralExpression)) return lit.Token.ValueText;
                if (lit.IsKind(SyntaxKind.TrueLiteralExpression)) return true;
                if (lit.IsKind(SyntaxKind.FalseLiteralExpression)) return false;
                if (lit.IsKind(SyntaxKind.NumericLiteralExpression)) return lit.Token.Value ?? lit.Token.ValueText;
                return lit.Token.ValueText ?? lit.ToString();

            case MemberAccessExpressionSyntax member:
                return "{" + member.ToString() + "}";

            case IdentifierNameSyntax id:
                return "{" + id.Identifier.Text + "}";

            case InvocationExpressionSyntax inv:
                return "{" + inv.Expression.ToString() + "(...)}";

            case ObjectCreationExpressionSyntax obj:
                var dict = new Dictionary<string, object>();
                if (obj.Initializer != null)
                {
                    foreach (ExpressionSyntax e in obj.Initializer.Expressions)
                    {
                        if (e is AssignmentExpressionSyntax a)
                        {
                            dict[a.Left.ToString()] = GuessExampleValueFromExpression(a.Right);
                        }
                    }
                }
                return dict;

            default:
                return expr.ToString();
        }
    }

    private Type? ResolveType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        typeName = typeName.Trim();
        // 处理 Nullable<T> (int? -> Nullable<int>)
        bool isNullable = typeName.EndsWith('?');
        string baseName = isNullable ? typeName[..^1] : typeName;
        Type? baseType = baseName switch
        {
            "int" => typeof(int),
            "long" => typeof(long),
            "bool" => typeof(bool),
            "string" => typeof(string),
            "decimal" => typeof(decimal),
            "double" => typeof(double),
            "float" => typeof(float),
            "DateTime" => typeof(DateTime),
            "DateOnly" => typeof(DateOnly),
            _ => null
        } ?? _options.TargetAssembly.GetType(baseName)
                       ?? _options.TargetAssembly.GetTypes().FirstOrDefault(t => t.Name == baseName);
        // 泛型类型处理，例如 List<T>、IList<T>
        if (baseType == null && typeName.Contains('<') && typeName.EndsWith('>'))
        {
            int idx = typeName.IndexOf('<');
            string genericName = typeName[..idx].Trim();
            string genericArgs = typeName[(idx + 1)..^1].Trim();

            Type? genericTypeDef = _options.TargetAssembly.GetType(genericName)
                                    ?? _options.TargetAssembly.GetTypes().FirstOrDefault(t => t.Name == genericName)
                                    ?? genericName switch
                                    {
                                        "List" => typeof(List<>),
                                        "IList" => typeof(IList<>),
                                        "IEnumerable" => typeof(IEnumerable<>),
                                        "ICollection" => typeof(ICollection<>),
                                        _ => null
                                    };

            if (genericTypeDef != null)
            {
                Type? innerType = ResolveType(genericArgs);
                if (innerType != null)
                    return genericTypeDef.MakeGenericType(innerType);
            }
        }
        if (baseType != null && isNullable && baseType.IsValueType)
            return typeof(Nullable<>).MakeGenericType(baseType);
        return baseType;
    }

    private static string InferParameterSource(ParameterSyntax param)
    {
        foreach (AttributeListSyntax attrList in param.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                string n = attr.Name.ToString();
                if (n.EndsWith("FromBody")) return "Body";
                if (n.EndsWith("FromQuery")) return "Query";
                if (n.EndsWith("FromRoute")) return "Route";
                if (n.EndsWith("FromForm")) return "Form";
                if (n.EndsWith("FromServices")) return "Service";
            }
        }
        return "Unknown";
    }

    private Type? InferModelTypeFromIdentifier(IdentifierNameSyntax id)
    {
        string name = id.Identifier.Text;
        MethodDeclarationSyntax? method = id.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method != null)
        {
            foreach (ParameterSyntax param in method.ParameterList.Parameters)
                if (param.Identifier.Text == name && param.Type != null)
                    return ResolveType(param.Type.ToString());

            IEnumerable<VariableDeclaratorSyntax> locals = method.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(v => v.Identifier.Text == name);
            foreach (VariableDeclaratorSyntax local in locals)
                if (local.Parent is VariableDeclarationSyntax decl && decl.Type != null)
                    return ResolveType(decl.Type.ToString());
        }

        ClassDeclarationSyntax? cls = id.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (cls != null)
        {
            foreach (MemberDeclarationSyntax member in cls.Members)
            {
                switch (member)
                {
                    case PropertyDeclarationSyntax prop:
                        if (prop.Identifier.Text == name && prop.Type != null)
                            return ResolveType(prop.Type.ToString());
                        break;

                    case FieldDeclarationSyntax field:
                        foreach (VariableDeclaratorSyntax v in field.Declaration.Variables)
                            if (v.Identifier.Text == name && field.Declaration.Type != null)
                                return ResolveType(field.Declaration.Type.ToString());
                        break;
                }
            }
        }

        return null;
    }

    #region 报告生成
    private void GenerateReports()
    {
        if (_options.GenerateMarkdownReport) GenerateMarkdownReport();
        if (_options.GenerateJsonReport) GenerateJsonReport();
    }

    private void GenerateMarkdownReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MVC 控制器 Action 扫描（Roslyn）");
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (ControllerActionInfo action in ScanResult)
        {
            sb.AppendLine($"## {action.ControllerName}/{action.ActionName} [{action.HttpMethod}] {(action.IsAuthorized ? "(Authorized)" : "")}");
            sb.AppendLine($"返回类型：{action.MethodReturnType}");
            sb.AppendLine();

            // 请求参数
            if (action.Parameters.Count > 0)
            {
                sb.AppendLine("### 请求参数");

                var paramExampleDict = new Dictionary<string, object>();

                foreach (ParameterInfoModel p in action.Parameters.Where(x => x.Source != "Service"))
                {
                    string optionalText = p.IsOptional ? $"Optional, Default={p.DefaultValue}" : "Required";
                    sb.AppendLine($"- {p.Name} ({p.Type}) [{p.Source}] {optionalText}");

                    object exampleValue;

                    if (p.Properties != null && p.Properties.Count > 0)
                    {
                        // 复杂对象，直接调用 BuildPropertyValue 生成嵌套字典
                        exampleValue = SampleModelBuilder.BuildPropertyValue(ResolveType(p.Type));
                    }
                    else
                    {
                        // 基础类型或枚举，调用 BuildPropertyValue
                        exampleValue = SampleModelBuilder.BuildPropertyValue(ResolveType(p.Type));
                    }

                    // 展示示例值
                    string json = JsonSerializer.Serialize(exampleValue, _jsonSerializer);
                    sb.AppendLine($"  - 示例值: {json}");
                }
                sb.AppendLine();
            }

            // 返回值
            if (action.ReturnStatements.Count == 0)
            {
                sb.AppendLine("### 返回值");
                sb.AppendLine("无 return 语句检测到。\n");
                continue;
            }

            sb.AppendLine("### 返回值");
            foreach (ReturnStatementInfo r in action.ReturnStatements)
            {
                sb.AppendLine($"- **ReturnType**: {r.ReturnType}");
                sb.AppendLine($"  - 源码: `{r.SourceCode.Trim().Replace("\r\n", " ")}`");

                if (!string.IsNullOrEmpty(r.RedirectTarget))
                    sb.AppendLine($"  - Redirect Target: {r.RedirectTarget}");

                if (!string.IsNullOrEmpty(r.ModelType))
                {
                    sb.AppendLine($"  - Model Type: {r.ModelType}");
                    sb.AppendLine($"  - 示例 Model JSON:");

                    string json = JsonSerializer.Serialize(r.ExampleModel, _jsonSerializer);
                    sb.AppendLine("```json");
                    sb.AppendLine(json);
                    sb.AppendLine("```");
                }
            }

            sb.AppendLine();
        }
        string mdPath = Path.Combine(_options.ReportOutputDir, "MvcScanReport_Roslyn.md");
        File.WriteAllText(mdPath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine("已写入 Markdown 报告：" + mdPath);
    }

    private void GenerateJsonReport()
    {
        string json = JsonSerializer.Serialize(ScanResult, _jsonSerializer);
        string jsonPath = Path.Combine(_options.ReportOutputDir, "MvcScanResult_Roslyn.json");
        File.WriteAllText(jsonPath, json, Encoding.UTF8);
        Console.WriteLine("已写入 JSON 报告：" + jsonPath);
    }
    #endregion
}
