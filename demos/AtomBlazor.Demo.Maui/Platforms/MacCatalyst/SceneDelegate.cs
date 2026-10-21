using System.Diagnostics.CodeAnalysis;
using Foundation;

namespace AtomBlazor.Demo.Maui;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "UIKit 入口类型名由平台注册与 MAUI 模板约定，不能重命名。")]
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
