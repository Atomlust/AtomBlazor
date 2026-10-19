using System.Linq.Expressions;
using System.Reflection;

namespace AtomBlazor;

/// <summary>
/// 把值表达式（例如 <c>() =&gt; Input.Username</c>）格式化为 HTML name 属性使用的
/// 点号分隔成员路径（"Input.Username"），与 Blazor 内置 <c>InputBase&lt;T&gt;</c>
/// 行为对齐，使 <c>[SupplyParameterFromForm]</c> 在增强/SSR 表单回传时能正确模型绑定。
/// </summary>
internal static class ExpressionPathFormatter
{
    /// <summary>
    /// 格式化值表达式的成员路径；当表达式形态不是纯粹的成员/索引器链时返回 null
    /// （调用方应回退到 <see cref="Microsoft.AspNetCore.Components.Forms.FieldIdentifier.FieldName"/>）。
    /// </summary>
    public static string? FormatLambda(LambdaExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        // 自内向外收集，随后反转。索引器片段渲染为 "[n]"，
        // 直接附着在前一个成员之后、不带点号分隔。
        var segments = new List<string>();
        var node = expression.Body;

        while (node is not null)
        {
            switch (node)
            {
                case MemberExpression member:
                    segments.Add(member.Member.Name);
                    node = member.Expression;
                    break;

                case BinaryExpression { NodeType: ExpressionType.ArrayIndex } arrayIndex:
                    if (!TryEvaluateIndex(arrayIndex.Right, out var arrayIdx))
                    {
                        return null;
                    }
                    segments.Add($"[{arrayIdx}]");
                    node = arrayIndex.Left;
                    break;

                case MethodCallExpression { Method.Name: "get_Item", Arguments.Count: 1 } call when call.Object is not null:
                    if (!TryEvaluateIndex(call.Arguments[0], out var itemIdx))
                    {
                        return null;
                    }
                    segments.Add($"[{itemIdx}]");
                    node = call.Object;
                    break;

                case ConstantExpression:
                    // 链的根：组件实例或闭包类。
                    node = null;
                    break;

                default:
                    // 不支持的形态（转换、方法调用等）——交由调用方回退。
                    return null;
            }
        }

        if (segments.Count == 0)
        {
            return null;
        }

        segments.Reverse();

        var result = new System.Text.StringBuilder();
        foreach (var segment in segments)
        {
            if (result.Length > 0 && segment[0] != '[')
            {
                result.Append('.');
            }
            result.Append(segment);
        }

        return result.ToString();
    }

    private static bool TryEvaluateIndex(Expression indexExpression, out object? value)
    {
        switch (indexExpression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            // 捕获的循环变量：闭包常量上的字段访问。
            case MemberExpression { Expression: ConstantExpression closure, Member: FieldInfo field }:
                value = field.GetValue(closure.Value);
                return true;

            default:
                value = null;
                return false;
        }
    }
}
