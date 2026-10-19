using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AtomBlazor.Demo.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// 通过共享扩展方法注册全部演示站服务（对照源 Demo.Auto.Client 的 Program.cs）。
// Auto 渲染模式下根组件由宿主页 SSR 标记驱动，客户端无需显式 RootComponents.Add。
builder.Services.AddAtomBlazorDemo();

await builder.Build().RunAsync();
