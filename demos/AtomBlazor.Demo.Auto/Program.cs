using AtomBlazor.Demo.Extensions;
using AtomBlazor.Demo.Auto;
using AtomBlazor.Demo.Services;

var builder = WebApplication.CreateBuilder(args);

// 浏览器测试文化基线（授权偏差）：同 Server 宿主——本机 zh-CN 线程文化使文化驱动
// 文案输出「下午/周一」，与 en 基线的 Playwright 规格不符；宿主固定 en-US。
var usCulture = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = usCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = usCulture;

// 注册 Razor 组件服务：Auto 渲染模式同时注册 Server 与 WebAssembly 两种交互后端
// （对照源 Demo.Auto 的 Program.cs）。
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// 通过共享扩展方法注册全部演示站服务
builder.Services.AddAtomBlazorDemo();

var app = builder.Build();

// 配置 HTTP 请求管线。
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

// 多语言：由 cookie 初始化本请求作用域的 DemoCultureService
// 使 SSR 首帧即以上次选择的语言渲染（Server 宿主同款中间件）。
app.Use(async (context, next) =>
{
    context.RequestServices.GetRequiredService<DemoCultureService>()
        .InitializeFromCookie(context.Request.Cookies[DemoCultureService.CookieName]);
    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(AtomBlazor.Demo.Routes).Assembly,
        typeof(AtomBlazor.Demo.Auto.Client._Imports).Assembly);

app.Run();
