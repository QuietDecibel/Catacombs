using Avalonia.Threading;

namespace XTouch.UI.ViewModels;

/// <summary>Marshals service callbacks (background threads) onto the UI thread. Replaceable for tests.</summary>
public static class UiThread
{
    public static Action<Action> Post { get; set; } = a => Dispatcher.UIThread.Post(a);
}
