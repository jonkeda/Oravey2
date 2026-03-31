using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace Oravey2.Core.NPC;

/// <summary>
/// Renders a floating name label above an NPC entity using a billboard UI.
/// </summary>
public class NpcNameLabelScript : SyncScript
{
    public string DisplayName { get; set; } = "";
    public Color LabelColor { get; set; } = new Color(255, 255, 255);
    public SpriteFont? Font { get; set; }

    private TextBlock? _label;

    public override void Start()
    {
        base.Start();

        _label = new TextBlock
        {
            Text = DisplayName,
            Font = Font,
            TextSize = 12,
            TextColor = LabelColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var canvas = new Canvas { Width = 200, Height = 30 };
        canvas.Children.Add(_label);

        Entity.Add(new UIComponent
        {
            Page = new UIPage { RootElement = canvas },
            Resolution = new Vector3(200, 30, 100),
            Size = new Vector3(1f, 0.2f, 1f),
            IsFullScreen = false,
            IsBillboard = true,
        });
    }

    public override void Update()
    {
        // Label is static — no per-frame update needed.
    }
}
