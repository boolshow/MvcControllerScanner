using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MvcControllerScanner.Extensions;

public static class SampleModelBuilder
{
    private const int MaxDepth = 10;
    public static Dictionary<string, object> Build(Type type)
    {
        return Build(type, 0, []);
    }
    private static Dictionary<string, object> Build(Type type, int depth = 0, HashSet<Type>? visited = null)
    {
        visited ??= [];
        if (type == null || depth > MaxDepth)
            return [];
        if (visited.Contains(type))
            return [];
        visited.Add(type);
        var dict = new Dictionary<string, object>();
        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            dict[prop.Name] = BuildPropertyValue(prop.PropertyType, depth + 1, visited);
        }

        visited.Remove(type);
        return dict;
    }
    public static object BuildPropertyValue(Type? type, int depth = 0, HashSet<Type>? visited = null)
    {
        if (type is null) return null!;
        if (depth > MaxDepth) return null!;

        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        // 基础类型
        if (underlying == typeof(string)) return "";
        if (underlying == typeof(bool)) return false;
        if (underlying == typeof(int)) return 0;
        if (underlying == typeof(long)) return 0L;
        if (underlying == typeof(decimal)) return 0m;
        if (underlying == typeof(double)) return 0.0;
        if (underlying == typeof(float)) return 0f;
        // 日期类型
        if (underlying == typeof(DateTime)) return DateTime.MinValue.ToString("yyyy-MM-dd");
        if (underlying == typeof(DateOnly)) return new DateOnly(1, 1, 1).ToString("yyyy-MM-dd");

        // 枚举
        if (underlying.IsEnum)
        {
            Array values = Enum.GetValues(underlying);
            return values.Length > 0 ? values.GetValue(0)! : Activator.CreateInstance(underlying)!;
        }

        // IList<T> / IEnumerable<T> / ICollection<T>
        if (underlying.IsGenericType &&
            (underlying.GetGenericTypeDefinition() == typeof(IList<>) ||
             underlying.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             underlying.GetGenericTypeDefinition() == typeof(ICollection<>)))
        {
            Type elemType = underlying.GetGenericArguments()[0];
            return new List<object> { BuildElement(elemType, depth + 1, visited) };
        }

        // IPageList<T>
        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition().Name.StartsWith("IPageList"))
        {
            Type itemType = underlying.GetGenericArguments()[0];
            return new Dictionary<string, object>
            {
                ["PageIndex"] = 0,
                ["PageSize"] = 0,
                ["TotalItemCount"] = 0,
                ["TotalPageCount"] = 0,
                ["Items"] = new List<object> { BuildElement(itemType, depth + 1, visited) }
            };
        }

        // 普通对象 / record
        return Build(type, depth + 1, visited);
    }
    private static object BuildElement(Type type, int depth = 0, HashSet<Type>? visited = null)
    {
        // 基础类型直接生成默认值
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsPrimitive || underlying == typeof(string) || underlying.IsEnum || underlying == typeof(DateTime) || underlying == typeof(DateOnly))
            return BuildPropertyValue(type, depth, visited);
        // 对象递归生成字典
        return Build(type, depth, visited);
    }

}
