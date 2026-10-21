using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DynamicForm 测试：原生/JSON Schema 双格式解析、字段类型映射、
/// 提交校验（必填 + Email 规则）、可见性表达式驱动显隐与隐藏值清理、
/// 网格结构快照。提交经 @onsubmit 直发；开关类字段经 onclick 直发。
/// </summary>
public class DynamicFormTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>两字段基础架构：姓名（必填文本）+ 邮箱（必填 + Email 校验）。</summary>
    private static FormSchema BasicSchema() => new()
    {
        Title = "测试表单",
        Description = "动态渲染验证",
        Fields =
        [
            new FormFieldDefinition
            {
                Name = "name",
                Label = "姓名",
                Type = FieldType.Text,
                Required = true,
                Placeholder = "输入姓名",
            },
            new FormFieldDefinition
            {
                Name = "email",
                Label = "邮箱",
                Type = FieldType.Email,
                Required = true,
                Description = "用于接收通知",
                Validations = [new FieldValidation { Type = ValidationType.Email }],
            },
        ],
    };

    private static async Task<(ComponentTestRenderer Renderer, DynamicForm Form)> MountAsync(
        FormSchema schema,
        Dictionary<string, object?>? extra = null)
    {
        var renderer = CreateRenderer();
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DynamicForm.Schema)] = schema,
        };
        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                parameters[key] = value;
            }
        }

        var form = await renderer.MountAsync<DynamicForm>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        return (renderer, form);
    }

    /// <summary>直发表单提交事件，触发 ValidateAsync 全量校验。</summary>
    private static async Task SubmitAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onsubmit", new EventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
    }

    // ------------------------------------------------------------------
    // FormSchemaParser：原生格式
    // ------------------------------------------------------------------

    [Fact]
    public void 原生JSON应解析出字段类型与校验规则()
    {
        const string json = """
        {
          "title": "原生架构",
          "description": "来自 JSON",
          "columns": 2,
          "fields": [
            {
              "name": "username",
              "label": "用户名",
              "type": "text",
              "required": true,
              "placeholder": "输入用户名",
              "validations": [
                { "type": "minLength", "value": 3, "message": "太短" }
              ]
            },
            {
              "name": "plan",
              "label": "套餐",
              "type": "select",
              "options": [
                { "value": "free", "text": "免费" },
                "pro"
              ]
            }
          ]
        }
        """;

        var schema = FormSchemaParser.FromJson(json);

        Assert.Equal("原生架构", schema.Title);
        Assert.Equal(2, schema.Columns);
        Assert.Equal(2, schema.Fields.Count);

        var username = schema.Fields[0];
        Assert.Equal("username", username.Name);
        Assert.Equal(FieldType.Text, username.Type);
        Assert.True(username.Required);
        Assert.Equal("输入用户名", username.Placeholder);
        Assert.NotNull(username.Validations);
        Assert.Equal(ValidationType.MinLength, Assert.Single(username.Validations!).Type);
        // 行为：ParseJsonValue 的三元表达式
        // `TryGetInt32(out var i) ? i : GetDouble()` 把 int 提升为 double
        // 故 JSON 数字恒落到 double 而非 int。
        Assert.Equal(3d, Assert.Single(username.Validations!).Value);

        var plan = schema.Fields[1];
        Assert.Equal(FieldType.Select, plan.Type);
        Assert.NotNull(plan.Options);
        Assert.Equal(2, plan.Options!.Count);
        Assert.Equal("free", plan.Options[0].Value);
        Assert.Equal("免费", plan.Options[0].Text);
        Assert.Equal("pro", plan.Options[1].Value); // 字符串选项值即文本
    }

    [Fact]
    public void 原生JSON应解析分区与可见性表达式()
    {
        const string json = """
        {
          "sections": [
            {
              "title": "高级",
              "collapsible": true,
              "defaultExpanded": false,
              "visibleWhen": "mode == 'advanced'",
              "fields": [
                { "name": "token", "type": "password" }
              ]
            }
          ]
        }
        """;

        var schema = FormSchemaParser.FromJson(json);

        var section = Assert.Single(schema.Sections);
        Assert.Equal("高级", section.Title);
        Assert.True(section.Collapsible);
        Assert.False(section.DefaultExpanded);
        Assert.Equal("mode == 'advanced'", section.VisibleWhen);

        var field = Assert.Single(section.Fields);
        Assert.Equal("token", field.Name);
        Assert.Equal(FieldType.Password, field.Type);
    }

    [Fact]
    public void 原生JSON缺少name应抛出JsonException()
    {
        const string json = """
        {
          "fields": [ { "type": "text" } ]
        }
        """;

        Assert.Throws<System.Text.Json.JsonException>(() => FormSchemaParser.FromJson(json));
    }

    // ------------------------------------------------------------------
    // FormSchemaParser：JSON Schema 自动探测
    // ------------------------------------------------------------------

    [Fact]
    public void JSONSchema应按format与required映射字段()
    {
        const string json = """
        {
          "type": "object",
          "title": "资料",
          "required": [ "email" ],
          "properties": {
            "email": {
              "type": "string",
              "format": "email",
              "title": "电子邮箱",
              "minLength": 5
            },
            "age": {
              "type": "integer",
              "minimum": 0,
              "maximum": 150
            },
            "subscribe": {
              "type": "boolean"
            }
          }
        }
        """;

        var schema = FormSchemaParser.FromJson(json);

        Assert.Equal("资料", schema.Title);
        Assert.Equal(3, schema.Fields.Count);

        var email = schema.Fields[0];
        Assert.Equal("email", email.Name);
        Assert.Equal(FieldType.Email, email.Type);
        Assert.Equal("电子邮箱", email.Label);
        Assert.True(email.Required);
        Assert.Contains(email.Validations!, v => v.Type == ValidationType.MinLength && v.Value is 5);
        Assert.Contains(email.Validations!, v => v.Type == ValidationType.Email);

        var age = schema.Fields[1];
        Assert.Equal(FieldType.Number, age.Type);
        Assert.Contains(age.Validations!, v => v.Type == ValidationType.Min);
        Assert.Contains(age.Validations!, v => v.Type == ValidationType.Max);

        var subscribe = schema.Fields[2];
        Assert.Equal(FieldType.Checkbox, subscribe.Type);
        Assert.False(subscribe.Required);
    }

    [Fact]
    public void JSONSchema应按enum与oneOf生成选项()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "color": {
              "type": "string",
              "enum": [ "red", "green" ]
            },
            "size": {
              "oneOf": [
                { "const": "s", "title": "小号" },
                { "const": "l", "title": "大号" }
              ]
            },
            "tags": {
              "type": "array",
              "items": { "type": "string" }
            }
          }
        }
        """;

        var schema = FormSchemaParser.FromJson(json);

        var color = schema.Fields[0];
        Assert.Equal(FieldType.Select, color.Type);
        Assert.Equal(2, color.Options!.Count);
        Assert.Equal("red", color.Options[0].Value);

        var size = schema.Fields[1];
        Assert.Equal(FieldType.Select, size.Type);
        Assert.Equal("s", size.Options![0].Value);
        Assert.Equal("小号", size.Options[0].Text);

        var tags = schema.Fields[2];
        Assert.Equal(FieldType.Tags, tags.Type); // 无枚举条目的数组回落 Tags
    }

    [Fact]
    public void JSONSchema的xFieldType提示应覆盖类型推断()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "city": {
              "type": "string",
              "x-field-type": "combobox"
            }
          }
        }
        """;

        var schema = FormSchemaParser.FromJson(json);

        Assert.Equal(FieldType.Combobox, Assert.Single(schema.Fields).Type);
    }

    // ------------------------------------------------------------------
    // 渲染与校验
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染应输出标题描述与标签帮助文本()
    {
        var (renderer, _) = await MountAsync(BasicSchema());

        var markup = renderer.Markup();
        Assert.Contains("测试表单", markup);
        Assert.Contains("动态渲染验证", markup);
        Assert.Contains("姓名", markup);
        Assert.Contains("邮箱", markup);
        Assert.Contains("用于接收通知", markup);
        Assert.Contains("输入姓名", markup);
        // 字段 id 由底座 FormField 生成（formfield-{guid}），非 atom-df 前缀。
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}""", markup);
        Assert.Contains("type=\"email\"", markup); // Email 字段映射 input type
        Assert.Contains("Submit", markup); // 内置提交按钮默认文本
    }

    [Fact]
    public async Task 提交空表单应产出必填错误且不触发有效提交()
    {
        var validSubmits = 0;
        var (renderer, _) = await MountAsync(BasicSchema(), new Dictionary<string, object?>
        {
            [nameof(DynamicForm.OnValidSubmit)] =
                EventCallback.Factory.Create<Dictionary<string, object?>>(new object(), _ => validSubmits++),
        });

        await SubmitAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("姓名 is required.", markup);
        Assert.Contains("邮箱 is required.", markup);
        Assert.Equal(0, validSubmits);
    }

    [Fact]
    public async Task 校验摘要开关应控制顶部错误列表显隐()
    {
        var (hidden, _) = await MountAsync(BasicSchema());
        await SubmitAsync(hidden);
        Assert.DoesNotContain("Validation Errors", hidden.Markup()); // 默认关

        var (shown, _) = await MountAsync(BasicSchema(), new Dictionary<string, object?>
        {
            [nameof(DynamicForm.ShowValidationSummary)] = true,
        });
        await SubmitAsync(shown);
        var markup = shown.Markup();
        Assert.Contains("Validation Errors", markup);
        Assert.Contains("atom:list-disc", markup);
    }

    [Fact]
    public async Task 填值后提交应通过校验并回传值字典()
    {
        Dictionary<string, object?>? submitted = null;
        var (renderer, form) = await MountAsync(BasicSchema(), new Dictionary<string, object?>
        {
            [nameof(DynamicForm.OnValidSubmit)] =
                EventCallback.Factory.Create<Dictionary<string, object?>>(new object(), v => submitted = v),
        });

        // 直接写入值字典模拟用户输入（OnFieldValueChanged 内部链路由可见性测试覆盖）
        form.Values["name"] = "张三";
        form.Values["email"] = "zhangsan@example.com";
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        await SubmitAsync(renderer);

        Assert.NotNull(submitted);
        Assert.Equal("张三", submitted!["name"]);
        Assert.Equal("zhangsan@example.com", submitted["email"]);
        Assert.DoesNotContain("is required.", renderer.Markup());
    }

    [Fact]
    public async Task 非法邮箱应报格式错误()
    {
        var (renderer, form) = await MountAsync(BasicSchema());
        form.Values["name"] = "张三";
        form.Values["email"] = "not-an-email";
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        await SubmitAsync(renderer);

        Assert.Contains("邮箱 must be a valid email address.", renderer.Markup());
    }

    [Fact]
    public async Task 异步自定义校验器应阻塞提交直至返回错误()
    {
        var gate = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asyncCalls = 0;
        var validSubmits = 0;
        var schema = new FormSchema
        {
            Fields =
            [
                new FormFieldDefinition
                {
                    Name = "code",
                    Label = "验证码",
                    Type = FieldType.Text,
                    Validations = [new FieldValidation { Type = ValidationType.Custom }],
                    AsyncValidator = async (_, ct) =>
                    {
                        Interlocked.Increment(ref asyncCalls);
                        return await gate.Task.WaitAsync(ct);
                    },
                },
            ],
        };

        var (renderer, form) = await MountAsync(schema, new Dictionary<string, object?>
        {
            [nameof(DynamicForm.OnValidSubmit)] =
                EventCallback.Factory.Create<Dictionary<string, object?>>(new object(), _ => validSubmits++),
        });
        form.Values["code"] = "1234";
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        var submit = SubmitAsync(renderer);
        for (var i = 0; i < 100 && Volatile.Read(ref asyncCalls) == 0; i++)
        {
            await Task.Delay(10);
        }

        Assert.Equal(1, Volatile.Read(ref asyncCalls));
        Assert.Equal(0, Volatile.Read(ref validSubmits)); // 异步结果未出前不判定通过

        gate.SetResult("验证码已过期");
        await submit;

        Assert.Equal(0, Volatile.Read(ref validSubmits));
        Assert.Contains("验证码已过期", renderer.Markup());
    }

    [Fact]
    public async Task 异步自定义校验器通过后应放行有效提交()
    {
        var validSubmits = 0;
        var schema = new FormSchema
        {
            Fields =
            [
                new FormFieldDefinition
                {
                    Name = "code",
                    Label = "验证码",
                    Type = FieldType.Text,
                    Validations = [new FieldValidation { Type = ValidationType.Custom }],
                    AsyncValidator = async (_, _) => { await Task.Yield(); return null; },
                },
            ],
        };

        var (renderer, form) = await MountAsync(schema, new Dictionary<string, object?>
        {
            [nameof(DynamicForm.OnValidSubmit)] =
                EventCallback.Factory.Create<Dictionary<string, object?>>(new object(), _ => validSubmits++),
        });
        form.Values["code"] = "1234";
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        await SubmitAsync(renderer);

        Assert.Equal(1, Volatile.Read(ref validSubmits));
        Assert.Contains("验证码", renderer.Markup()); // 标签仍在，仅校验通过
    }

    [Fact]
    public async Task 同步自定义校验器应优先于异步校验器()
    {
        var asyncCalls = 0;
        var schema = new FormSchema
        {
            Fields =
            [
                new FormFieldDefinition
                {
                    Name = "code",
                    Label = "验证码",
                    Type = FieldType.Text,
                    Validations = [new FieldValidation { Type = ValidationType.Custom }],
                    CustomValidator = _ => "同步拒绝",
                    AsyncValidator = async (_, _) => { asyncCalls++; await Task.Yield(); return null; },
                },
            ],
        };

        var (renderer, form) = await MountAsync(schema);
        form.Values["code"] = "1234";
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        await SubmitAsync(renderer);

        Assert.Equal(0, asyncCalls);
        Assert.Contains("同步拒绝", renderer.Markup());
    }

    [Fact]
    public async Task 可见性表达式应控制字段显隐并清理隐藏值()
    {
        var schema = new FormSchema
        {
            Fields =
            [
                new FormFieldDefinition { Name = "advanced", Label = "高级模式", Type = FieldType.Switch },
                new FormFieldDefinition
                {
                    Name = "extra",
                    Label = "额外设置",
                    Type = FieldType.Text,
                    VisibleWhen = "advanced == true",
                },
            ],
        };

        var (renderer, form) = await MountAsync(schema);

        // 初始 advanced 为 false：额外字段不可见
        Assert.DoesNotContain("额外设置", renderer.Markup());
        Assert.False(form.Values.ContainsKey("extra"));

        // 点击开关（唯一 onclick 携带者）打开高级模式
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        var markup = renderer.Markup();
        Assert.Contains("额外设置", markup);
        Assert.True(form.Values.TryGetValue("advanced", out var advanced));
        Assert.Equal(true, advanced);

        // 关闭后字段重隐且值被清理
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

        Assert.DoesNotContain("额外设置", renderer.Markup());
        Assert.False(form.Values.ContainsKey("extra"));
    }

    [Fact]
    public async Task 分区渲染应输出标题并可折叠()
    {
        var schema = new FormSchema
        {
            Sections =
            [
                new FormSectionDefinition
                {
                    Title = "基本信息",
                    Description = "分区描述",
                    Collapsible = true,
                    Fields = [new FormFieldDefinition { Name = "nick", Label = "昵称", Type = FieldType.Text }],
                },
            ],
        };

        var (renderer, _) = await MountAsync(schema);

        var markup = renderer.Markup();
        Assert.Contains("基本信息", markup);
        Assert.Contains("分区描述", markup);
        Assert.Contains("昵称", markup);
        Assert.Contains("data-slot=\"form-section\"", markup);
    }

    [Fact]
    public async Task 默认网格结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(BasicSchema());

        // 字段 id 由底座 FormField 生成（含随机 GUID），先归一化使快照可确定。
        await Verify(Regex.Replace(renderer.Markup(), "formfield-[0-9a-f]{32}", "formfield-<id>"));
    }
}
