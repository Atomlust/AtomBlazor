using AtomBlazor.Demo.Extensions;
using Microsoft.Extensions.Logging;

namespace AtomBlazor.Demo.Maui;

/// <summary>
/// MAUI Blazor Hybrid 入口：装配 MAUI 应用、BlazorWebView 与共享文档站服务。
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

        // 与 Server/Wasm/Auto 宿主同一注册面（Primitives + Components + Theme），实证 Portal/JS 模块/静态资源在 Hybrid 下可解析
        builder.Services.AddAtomBlazorDemo();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
