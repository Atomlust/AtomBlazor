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
/// Toast 渲染面测试：变体自动图标（Default 恒无图标、四档变体各落一枚路径数据）、
/// 图标开关与图标类、关闭钮显隐与 padding 档位联动、ActionText/OnAction 双条件门控与点击
/// 先回调后关闭、悬停暂停/恢复对 animation-play-state 的驱动、ShowCountdown 与 Duration
/// 的缺省推导、Compact 尺寸的整套类位、Class 合并与 splat 落点（根元素同承 role/data-state）。
/// Toast 零 JS、零本地化（用户面文案 "Close" 照硬码英文），故服务面只放一个 no-op JS 桩
/// 与默认本地化器以兜住渲染器依赖。
/// 测试面局限：Toast 自身不管计时器，关闭计时/续期沿属 ToastProviderTests。
/// </summary>
public class ToastTests
{
    /// <summary>归一化 GUID（Id 若入 DOM）与时间戳，使快照跨运行确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "toast-<id>"),
            @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?", "<timestamp>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>实探根元素开标签（role="alert" 的持有者）。</summary>
    private static string RootTag(ComponentTestRenderer renderer)
    {
        var match = Regex.Match(renderer.Markup(), @"<div[^>]*role=""alert""[^>]*>", RegexOptions.Singleline);
        Assert.True(match.Success, "未渲染出携带 role=\"alert\" 的根元素");
        return match.Value;
    }

    // ------------------------------------------------------------------
    // 变体与自动图标
    // ------------------------------------------------------------------

    [Fact]
    public async Task Default变体即便ShowIcon为真也不出图标()
    {
        var (renderer, _) = await MountAsync(new ToastData
        {
            Title = "标题",
            Description = "描述",
            ShowIcon = true,
            ShowClose = false,
        });

        Assert.DoesNotContain("<svg", renderer.Markup());
        Assert.DoesNotContain("atom:shrink-0", renderer.Markup());
        Assert.Contains("atom:bg-background", RootTag(renderer));
    }

    [Fact]
    public async Task 四档变体应各自落对应Lucide图标路径与着色类()
    {
        var cases = new (ToastVariant Variant, string Path, string Tint)[]
        {
            (ToastVariant.Success, @"d=""m9 12l2 2l4-4""", "atom:text-alert-success"),
            (ToastVariant.Info, @"d=""M12 16v-4m0-4h.01""", "atom:text-alert-info"),
            (ToastVariant.Warning, @"d=""m21.73 18l-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3M12 9v4m0 4h.01""", "atom:text-alert-warning"),
            (ToastVariant.Destructive, @"d=""m15 9l-6 6m0-6l6 6""", "atom:text-destructive-foreground"),
        };

        foreach (var (variant, iconPath, tint) in cases)
        {
            var (renderer, _) = await MountAsync(new ToastData
            {
                Description = "变体检证",
                Variant = variant,
                ShowClose = false,
            });

            var markup = renderer.Markup();
            Assert.Contains(iconPath, markup);

            // 图标类落 LucideIcon 的 svg class（尺寸档 + 变体着色），非根元素的同名前景类
            Assert.Contains($"lucide-icon atom:h-5 atom:w-5 {tint}", markup);

            // 变体着色类落根元素（success/info/warning 走 alert-* 描边+底色；destructive 走 destructive 标记）
            Assert.Contains(RootVariantToken(variant), RootTag(renderer));
        }
    }

    private static string RootVariantToken(ToastVariant variant) => variant switch
    {
        ToastVariant.Success => "atom:bg-alert-success-bg",
        ToastVariant.Info => "atom:bg-alert-info-bg",
        ToastVariant.Warning => "atom:bg-alert-warning-bg",
        ToastVariant.Destructive => "atom:bg-destructive",
        _ => "atom:bg-background",
    };

    [Fact]
    public async Task ShowIcon为假时变体图标整体缺席但关闭钮仍在()
    {
        var (renderer, _) = await MountAsync(new ToastData
        {
            Description = "无图标",
            Variant = ToastVariant.Success,
            ShowIcon = false,
        });

        var markup = renderer.Markup();
        Assert.DoesNotContain("atom:shrink-0", markup); // 图标包裹块整块缺席
        Assert.DoesNotContain(@"d=""m9 12l2 2l4-4""", markup);

        // 唯一剩下的 svg 是关闭钮的 x 图标 + 无障碍文本 Close
        var svg = Assert.Single(Regex.Matches(markup, "<svg[^>]*>", RegexOptions.Singleline));
        Assert.Contains("lucide-icon atom:h-4 atom:w-4", svg.Value);
        Assert.Contains(@"d=""M18 6L6 18M6 6l12 12""", markup);
        Assert.Contains("atom:sr-only\">Close</span>", markup);
    }

    // ------------------------------------------------------------------
    // 关闭钮与操作钮
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭钮点击应回播OnDismiss()
    {
        var dismisses = 0;
        var data = new ToastData { Description = "要点掉" };
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Toast.OnDismiss)] = EventCallback.Factory.Create(new object(), () => dismisses++),
        };
        var (renderer, _) = await MountAsync(parameters, data);

        // ShowClose 缺省为真 → 关闭钮是唯一的 onclick 携带者
        await FireAsync(renderer, "onclick", new MouseEventArgs());

        Assert.Equal(1, dismisses);
    }

    [Fact]
    public async Task ShowClose为假时既无关闭钮也不加末端留白类()
    {
        var (renderer, _) = await MountAsync(new ToastData
        {
            Description = "不相关闭",
            Variant = ToastVariant.Info,
            ShowClose = false,
        });

        var markup = renderer.Markup();
        Assert.DoesNotContain("<button", markup);
        Assert.DoesNotContain("atom:sr-only", markup);

        // padding 档：默认尺寸 + 无关闭钮 → p-6（pe-8 让位）
        Assert.Contains("atom:p-6", RootTag(renderer));
        Assert.DoesNotContain("atom:pe-8", RootTag(renderer));
    }

    [Fact]
    public async Task 操作钮需ActionText与OnAction齐备且点击先回调后关闭()
    {
        var sequence = new List<string>();

        var data = new ToastData
        {
            Description = "已归档",
            ActionText = "撤销",
            OnAction = () => sequence.Add("action"),
            ShowClose = false,
        };
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Toast.Data)] = data,
            [nameof(Toast.OnDismiss)] = EventCallback.Factory.Create(new object(), () => sequence.Add("dismiss")),
        };
        var (renderer, _) = await MountAsync(parameters, data);

        Assert.Contains(">撤销</button>", renderer.Markup());
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        Assert.Equal(["action", "dismiss"], sequence);
    }

    [Fact]
    public async Task 只有ActionText而无OnAction时操作钮不落地()
    {
        var (renderer, _) = await MountAsync(new ToastData
        {
            Description = "诱饵",
            ActionText = "撤销",
            ShowClose = false,
        });

        var markup = renderer.Markup();
        Assert.DoesNotContain("撤销", markup);
        Assert.DoesNotContain("<button", markup);
    }

    [Fact]
    public async Task 无OnAction时OnDismiss仍应能由关闭钮触发()
    {
        var dismisses = 0;
        var data = new ToastData { Description = "只关闭", ActionText = "撤销" };
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Toast.Data)] = data,
            [nameof(Toast.OnDismiss)] = EventCallback.Factory.Create(new object(), () => dismisses++),
        };
        var (renderer, _) = await MountAsync(parameters, data);

        // ActionText 有值但 OnAction 为空 → 仅关闭钮携带 onclick
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        Assert.Equal(1, dismisses);
    }

    // ------------------------------------------------------------------
    // 标题/描述的显隐沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 标题与描述各自空串即整块不落地()
    {
        var (renderer, _) = await MountAsync(new ToastData { Description = "只有描述" });

        var markup = renderer.Markup();
        Assert.Contains("只有描述", markup);
        Assert.Contains("atom:opacity-90", markup);
        Assert.DoesNotContain("atom:font-semibold", markup); // 标题块整块缺席
    }

    [Fact]
    public async Task 标题与描述齐备时落grid内容区的两级文本()
    {
        var (renderer, _) = await MountAsync(new ToastData { Title = "已保存", Description = "草稿已更新" });

        var markup = renderer.Markup();
        Assert.Contains("atom:grid", markup);
        Assert.Contains("atom:flex-1", markup);
        Assert.Contains("atom:gap-1", markup);
        Assert.Contains("atom:text-sm atom:font-semibold", markup);
        Assert.Contains(">已保存</div>", markup);
        Assert.Contains(">草稿已更新</div>", markup);
    }

    // ------------------------------------------------------------------
    // 倒计时条与暂停恢复
    // ------------------------------------------------------------------

    [Fact]
    public async Task 倒计时条需ShowCountdown与正时长同时成立且时长直写动画()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Toast.ShowCountdown)] = true,
        }, new ToastData { Description = "五秒", Duration = 5000, ShowClose = false });

        Assert.Contains("animation: atom-alert-countdown 5000ms linear forwards", renderer.Markup());
        Assert.Contains("animation-play-state: running", renderer.Markup());
        Assert.Contains("atom:bg-current/20", renderer.Markup());
    }

    [Fact]
    public async Task Duration为零时开关联动不产出倒计时条()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Toast.ShowCountdown)] = true,
        }, new ToastData { Description = "常驻", Duration = 0, ShowClose = false });

        Assert.DoesNotContain("atom-alert-countdown", renderer.Markup());
    }

    [Fact]
    public async Task ShowCountdown为假时不产出倒计时条()
    {
        var (renderer, _) = await MountAsync(new ToastData { Description = "无进度", Duration = 8000 });

        Assert.DoesNotContain("atom-alert-countdown", renderer.Markup());
    }

    [Fact]
    public async Task 悬停进入退出应把动画状态在paused与running间翻面并播报回调()
    {
        var events = new List<string>();
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Toast.ShowCountdown)] = true,
            [nameof(Toast.OnPause)] = EventCallback.Factory.Create(new object(), () => events.Add("pause")),
            [nameof(Toast.OnResume)] = EventCallback.Factory.Create(new object(), () => events.Add("resume")),
        };
        var (renderer, _) = await MountAsync(parameters, new ToastData { Description = "悬停", Duration = 4000 });

        Assert.Contains("animation-play-state: running", renderer.Markup());

        await FireAsync(renderer, "onmouseenter", new MouseEventArgs());
        Assert.Contains("animation-play-state: paused", renderer.Markup());

        await FireAsync(renderer, "onmouseleave", new MouseEventArgs());
        Assert.Contains("animation-play-state: running", renderer.Markup());

        Assert.Equal(["pause", "resume"], events);
    }

    [Fact]
    public async Task 无OnPause委托时悬停仍翻自身暂停位但零回调()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Toast.ShowCountdown)] = true,
        }, new ToastData { Description = "无委托", Duration = 4000 });

        await FireAsync(renderer, "onmouseenter", new MouseEventArgs());
        Assert.Contains("animation-play-state: paused", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // Compact 尺寸档位
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compact档应整体收紧内边距字号图标与关闭钮位()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Toast.ShowCountdown)] = false,
        }, new ToastData
        {
            Title = "紧凑标题",
            Description = "紧凑描述",
            Variant = ToastVariant.Warning,
            Size = ToastSize.Compact,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:p-3", RootTag(renderer));
        Assert.Contains("atom:pe-6", RootTag(renderer));
        Assert.Contains("atom:gap-0.5", markup);
        Assert.Contains("atom:text-xs atom:font-semibold", markup);
        Assert.Contains("lucide-icon atom:h-4 atom:w-4 atom:text-alert-warning", markup);
        Assert.Contains("atom:end-1 atom:top-1", markup); // 关闭钮贴角位（Compact 档）
        Assert.DoesNotContain("atom:end-2 atom:top-2", markup);
    }

    [Fact]
    public async Task 默认尺寸与Compact尺寸的操作钮档位类位不同()
    {
        var (defaultRenderer, _) = await MountAsync(new ToastData
        {
            Description = "默认档",
            ActionText = "撤销",
            OnAction = () => { },
            ShowClose = false,
        });
        Assert.Contains("atom:h-8 atom:px-3 atom:text-sm", defaultRenderer.Markup());

        var (compactRenderer, _) = await MountAsync(new ToastData
        {
            Description = "紧凑档",
            Variant = ToastVariant.Info,
            Size = ToastSize.Compact,
            ActionText = "撤销",
            OnAction = () => { },
            ShowClose = false,
        });
        var compactMarkup = compactRenderer.Markup();
        Assert.Contains("atom:h-7 atom:px-2 atom:text-xs", compactMarkup);
        Assert.DoesNotContain("atom:h-8", compactMarkup);
    }

    // ------------------------------------------------------------------
    // Class 合并与 splat 落点
    // ------------------------------------------------------------------

    [Fact]
    public async Task Class与透传应同落根元素且类串经合并器归一()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Toast.Class)] = "my-toast",
            ["data-toast-probe"] = "on-root",
        };
        var (renderer, _) = await MountAsync(parameters, new ToastData { Description = "类合并" });

        var root = RootTag(renderer);
        Assert.Contains("my-toast", root);
        Assert.Contains("atom:rounded-md", root);
        Assert.Contains("data-toast-probe=\"on-root\"", root);

        // @attributes 在源序最末展开：class、role、data-state 先落，splat 后落
        Assert.True(root.IndexOf("class=\"", StringComparison.Ordinal) < root.IndexOf("data-toast-probe", StringComparison.Ordinal));
        Assert.Contains("role=\"alert\"", root);
        Assert.Contains("data-state=\"open\"", root);
    }

    [Fact]
    public async Task 快照应把Id与时间戳归一后落盘()
    {
        var (renderer, _) = await MountAsync(new ToastData
        {
            Title = "已同步",
            Description = "3 个文件已上传",
            Variant = ToastVariant.Success,
        });

        var markup = renderer.Markup();
        Assert.Equal(markup, Scrub(markup)); // 实探：Id/CreatedAt 不入 DOM，归一为无害保险
        await Verify(Scrub(markup));
    }

    // ------------------------------------------------------------------
    // 挂载助手
    // ------------------------------------------------------------------

    private static async Task<(ComponentTestRenderer Renderer, Toast Component)> MountAsync(ToastData data) =>
        await MountAsync(new Dictionary<string, object?>(), data);

    private static async Task<(ComponentTestRenderer Renderer, Toast Component)> MountAsync(
        Dictionary<string, object?> parameters, ToastData data)
    {
        var renderer = CreateRenderer();
        var bag = new Dictionary<string, object?>(parameters)
        {
            [nameof(Toast.Data)] = data,
        };
        var component = await renderer.MountAsync<Toast>(bag);
        return (renderer, component);
    }
}
