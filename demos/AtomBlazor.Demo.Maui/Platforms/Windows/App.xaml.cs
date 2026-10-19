using Microsoft.UI.Xaml;

namespace AtomBlazor.Demo.Maui.WinUI;

/// <summary>
/// WinUI 平台入口：以 <see cref="MauiWinUIApplication"/> 引导共享 MauiProgram。
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
