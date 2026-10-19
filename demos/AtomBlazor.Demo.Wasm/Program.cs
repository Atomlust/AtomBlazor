using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AtomBlazor.Demo.Extensions;
using AtomBlazor.Demo.Wasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 通过共享扩展方法注册全部演示站服务（对照源 Demo.Wasm 的 Program.cs）
builder.Services.AddAtomBlazorDemo();

await builder.Build().RunAsync();
