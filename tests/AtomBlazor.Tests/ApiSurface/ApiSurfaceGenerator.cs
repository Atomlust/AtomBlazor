using AtomBlazor.Primitives;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Tests.ApiSurface;

/// <summary>
/// 为给定程序集生成确定性的公开 API 面文本表示。
/// 配合 Verify 快照测试使用，用于检测对组件参数、枚举与服务
/// 的非预期破坏性变更。
/// </summary>
public static class ApiSurfaceGenerator
{
    /// <summary>
    /// 为给定程序集生成完整的 API 面文本。
    /// 输出按确定性顺序排序，使快照比较不受反射返回顺序影响而保持稳定。
    /// </summary>
    public static string Generate(Assembly assembly)
    {
        var sb = new StringBuilder();
        var assemblyName = assembly.GetName().Name;

        sb.AppendLine(CultureInfo.InvariantCulture, $"# {assemblyName} - API Surface");
        sb.AppendLine();

        var exportedTypes = assembly.GetExportedTypes()
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        WriteComponents(sb, exportedTypes);
        WriteEnums(sb, exportedTypes);
        WriteServiceInterfaces(sb, exportedTypes);

        return sb.ToString().TrimEnd();
    }

    private static void WriteComponents(StringBuilder sb, List<Type> types)
    {
        var componentTypes = types
            .Where(t => typeof(ComponentBase).IsAssignableFrom(t) && !t.IsAbstract)
            // 排除 Razor 构建约定类型（_Imports.razor 会编译为一个公开的
            // ComponentBase 类，但它并不构成真实的 API 面）。
            .Where(t => !t.Name.StartsWith('_'))
            .ToList();

        if (componentTypes.Count == 0)
        {
            return;
        }

        sb.AppendLine("## Components");
        sb.AppendLine();

        foreach (var type in componentTypes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {type.Name} ({type.Namespace})");

            var parameters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

            if (parameters.Count > 0)
            {
                foreach (var prop in parameters)
                {
                    var paramAttr = prop.GetCustomAttribute<ParameterAttribute>()!;
                    var suffixes = new List<string>();
                    if (paramAttr.CaptureUnmatchedValues)
                    {
                        suffixes.Add("[CaptureUnmatchedValues]");
                    }

                    if (prop.GetCustomAttribute<EditorRequiredAttribute>() != null)
                    {
                        suffixes.Add("[EditorRequired]");
                    }

                    var suffix = suffixes.Count > 0 ? " " + string.Join(" ", suffixes) : "";
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - {prop.Name} : {FormatTypeName(prop.PropertyType)}{suffix}");
                }
            }

            var cascadingParams = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<CascadingParameterAttribute>() != null && p.DeclaringType == type)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

            if (cascadingParams.Count > 0)
            {
                foreach (var prop in cascadingParams)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - {prop.Name} : {FormatTypeName(prop.PropertyType)} [CascadingParameter]");
                }
            }

            sb.AppendLine();
        }
    }

    private static void WriteEnums(StringBuilder sb, List<Type> types)
    {
        var enumTypes = types
            .Where(t => t.IsEnum)
            .ToList();

        if (enumTypes.Count == 0)
        {
            return;
        }

        sb.AppendLine("## Enums");
        sb.AppendLine();

        foreach (var type in enumTypes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {type.Name} ({type.Namespace})");

            foreach (var name in Enum.GetNames(type))
            {
                var value = Convert.ToInt32(Enum.Parse(type, name), CultureInfo.InvariantCulture);
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - {name} = {value}");
            }

            sb.AppendLine();
        }
    }

    private static void WriteServiceInterfaces(StringBuilder sb, List<Type> types)
    {
        var interfaces = types
            .Where(t => t.IsInterface)
            .ToList();

        if (interfaces.Count == 0)
        {
            return;
        }

        sb.AppendLine("## Interfaces");
        sb.AppendLine();

        foreach (var type in interfaces)
        {
            var displayName = type.IsGenericType
                ? FormatTypeName(type)
                : type.Name;

            sb.AppendLine(CultureInfo.InvariantCulture, $"### {displayName} ({type.Namespace})");

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

            foreach (var prop in properties)
            {
                var accessors = new List<string>();
                if (prop.GetMethod != null)
                {
                    accessors.Add("get");
                }

                if (prop.SetMethod != null)
                {
                    accessors.Add("set");
                }

                var accessorStr = accessors.Count > 0 ? $" {{ {string.Join("; ", accessors)}; }}" : "";
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - {prop.Name} : {FormatTypeName(prop.PropertyType)}{accessorStr}");
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName) // 排除属性访问器
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ThenBy(m => m.GetParameters().Length)
                .ToList();

            foreach (var method in methods)
            {
                var paramList = string.Join(", ", method.GetParameters()
                    .Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - {method.Name}({paramList}) : {FormatTypeName(method.ReturnType)}");
            }

            sb.AppendLine();
        }
    }

    /// <summary>
    /// 将类型名格式化为可读的字符串表示。
    /// 处理泛型、可空类型以及嵌套泛型。
    /// </summary>
    public static string FormatTypeName(Type type)
    {
        // 处理可空值类型：Nullable<bool> -> Boolean?
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return FormatTypeName(underlying) + "?";
        }

        // 处理泛型类型：EventCallback<MouseEventArgs>、Dictionary<String, Object> 等。
        if (type.IsGenericType)
        {
            var name = type.Name.Split('`')[0];
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{name}<{args}>";
        }

        // 处理数组
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            return FormatTypeName(elementType) + "[]";
        }

        // 处理按引用类型（out/ref 参数）
        if (type.IsByRef)
        {
            var elementType = type.GetElementType()!;
            return FormatTypeName(elementType);
        }

        // 使用 CLR 类型名（String、Boolean、Int32 等）
        return type.Name;
    }
}
