namespace AtomBlazor;

/// <summary>可复用的动画形状；自定义 Keyframes 优先于预设。</summary>
public enum MotionPreset
{
    /// <summary>淡入淡出。</summary>
    Fade,
    /// <summary>自下而上滑入。</summary>
    SlideUp,
    /// <summary>自上而下滑入。</summary>
    SlideDown,
    /// <summary>自右向左滑入（起点偏右）。</summary>
    SlideLeft,
    /// <summary>自左向右滑入（起点偏左）。</summary>
    SlideRight,
    /// <summary>缩放。</summary>
    Scale,
    /// <summary>弹跳。</summary>
    Bounce,
    /// <summary>脉冲。</summary>
    Pulse,
    /// <summary>水平抖动。</summary>
    ShakeX,
    /// <summary>垂直抖动。</summary>
    ShakeY,
    /// <summary>回弹（越过终点后回落）。</summary>
    Spring
}

/// <summary>控制动画在何时启动。</summary>
public enum MotionTrigger
{
    /// <summary>响应可见性变化动画，可选在首次可交互渲染后播放一次。</summary>
    Visibility,
    /// <summary>元素进入视口时动画。</summary>
    InView,
    /// <summary>指针悬停或键盘聚焦时动画。</summary>
    Hover,
    /// <summary>指针按下或空格/回车时动画。</summary>
    Press,
    /// <summary>仅在调用 PlayAsync 时动画。</summary>
    Manual
}
