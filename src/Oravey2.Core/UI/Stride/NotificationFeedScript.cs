using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Renders active notifications from NotificationService as a stack of text
/// messages at the bottom-center of the screen. Messages fade as they expire.
/// </summary>
public class NotificationFeedScript : SyncScript
{
    public NotificationService? Notifications { get; set; }
    public SpriteFont? Font { get; set; }

    private StackPanel? _stack;
    private UIComponent? _uiComponent;

    public override void Start()
    {
        base.Start();

        _stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 80),
        };

        var page = new UIPage { RootElement = _stack };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    public override void Update()
    {
        if (Notifications == null || _stack == null) return;

        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        Notifications.Update(dt);

        var active = Notifications.GetActive();

        _stack.Children.Clear();

        foreach (var notification in active)
        {
            var alpha = Math.Clamp(notification.TimeRemaining / 0.5f, 0f, 1f);
            var color = new Color(255, 255, 255, (byte)(alpha * 255));

            _stack.Children.Add(new TextBlock
            {
                Text = notification.Message,
                Font = Font,
                TextSize = 16,
                TextColor = color,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
            });
        }
    }
}
