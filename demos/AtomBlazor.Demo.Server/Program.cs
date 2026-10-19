using AtomBlazor.Demo.Extensions;
using AtomBlazor.Demo.Server;
using AtomBlazor.Demo.Services;

var builder = WebApplication.CreateBuilder(args);

// 浏览器测试文化基线（授权偏差）：本机 zh-CN 线程文化使 TimeInput/Scheduler 等
// 文化驱动文案输出「下午/周一」，与 en 基线的 Playwright 规格不符；宿主固定 en-US。
var usCulture = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = usCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = usCulture;

// 注册 Razor 组件服务（对照源 Demo.Server 的 Program.cs）。
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

// 文档站显示语言：以 atom-lang cookie 初始化请求作用域的语言状态（SSR 首帧即目标语言，默认中文）。
app.Use(async (context, next) =>
{
    context.RequestServices
        .GetRequiredService<DemoCultureService>()
        .InitializeFromCookie(context.Request.Cookies[DemoCultureService.CookieName]);
    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AtomBlazor.Demo.Routes).Assembly);

app.Run();
