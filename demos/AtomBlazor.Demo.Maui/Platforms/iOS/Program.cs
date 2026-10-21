using ObjCRuntime;
using UIKit;

namespace AtomBlazor.Demo.Maui;

public class Program
{
    // 应用入口：使用 AppDelegate 作为 UIApplication 的委托类型。
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
