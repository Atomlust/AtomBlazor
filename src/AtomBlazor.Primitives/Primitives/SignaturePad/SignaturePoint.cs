namespace AtomBlazor.Primitives;

/// <summary>
/// 一个捕获到的笔迹位置点。
/// </summary>
/// <param name="X">距画板左边缘的距离，以 CSS 像素计。</param>
/// <param name="Y">距画板上边缘的距离，以 CSS 像素计。</param>
/// <param name="T">
/// 采样时刻，以页面加载起的毫秒计。时间戳之间的间隙正是笔画产生锥形的原因，
/// 所以一段回放的签名需要带上它们，才能看起来像当初写下的那一个。
/// </param>
public readonly record struct SignaturePoint(double X, double Y, double T);
