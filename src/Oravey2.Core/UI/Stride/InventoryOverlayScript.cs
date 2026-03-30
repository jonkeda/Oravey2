using global::Stride.Engine;
using global::Stride.Input;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.UI.ViewModels;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Tab-toggled inventory overlay. Reads from InventoryComponent each time it opens.
/// M0: text-only list, no drag-drop or item interaction.
/// </summary>
public class InventoryOverlayScript : SyncScript
{
    public InventoryComponent? Inventory { get; set; }

    private UIComponent? _uiComponent;
    private StackPanel? _itemList;
    private TextBlock? _weightText;
    private bool _visible;

    /// <summary>
    /// Exposes overlay visibility for automation queries.
    /// </summary>
    public bool IsVisible => _visible;

    public override void Start()
    {
        base.Start();
        BuildUI();
    }

    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.Tab))
        {
            _visible = !_visible;
            if (_uiComponent != null)
            {
                if (_visible)
                {
                    RefreshInventory();
                    _uiComponent.Page!.RootElement.Visibility = Visibility.Visible;
                }
                else
                {
                    _uiComponent.Page!.RootElement.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private void BuildUI()
    {
        _weightText = new TextBlock
        {
            Text = "Weight: 0/0",
            TextSize = 16,
            TextColor = global::Stride.Core.Mathematics.Color.White,
            Margin = new Thickness(10, 10, 10, 5)
        };

        _itemList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10, 0, 10, 10)
        };

        var header = new TextBlock
        {
            Text = "=== INVENTORY ===",
            TextSize = 20,
            TextColor = global::Stride.Core.Mathematics.Color.Gold,
            Margin = new Thickness(10, 10, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundColor = new global::Stride.Core.Mathematics.Color(0, 0, 0, 180),
            Width = 350,
            Children = { header, _weightText, _itemList },
            Visibility = Visibility.Collapsed
        };

        var page = new UIPage { RootElement = container };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    private void RefreshInventory()
    {
        if (Inventory == null || _itemList == null || _weightText == null) return;

        var vm = InventoryViewModel.Create(Inventory);
        _weightText.Text = $"Weight: {vm.CurrentWeight:F1} / {vm.MaxCarryWeight:F0}" +
                           (vm.IsOverweight ? " [OVERWEIGHT]" : "");

        _itemList.Children.Clear();

        if (vm.Items.Count == 0)
        {
            _itemList.Children.Add(new TextBlock
            {
                Text = "(empty)",
                TextSize = 14,
                TextColor = global::Stride.Core.Mathematics.Color.Gray,
                Margin = new Thickness(5, 2, 0, 2)
            });
            return;
        }

        foreach (var item in vm.Items)
        {
            var countSuffix = item.StackCount > 1 ? $" x{item.StackCount}" : "";
            var durSuffix = item.CurrentDurability.HasValue
                ? $" [{item.CurrentDurability}/{item.MaxDurability}]"
                : "";
            var color = item.Category switch
            {
                ItemCategory.WeaponMelee or
                ItemCategory.WeaponRanged
                    => global::Stride.Core.Mathematics.Color.OrangeRed,
                ItemCategory.Armor
                    => global::Stride.Core.Mathematics.Color.SteelBlue,
                ItemCategory.Consumable
                    => global::Stride.Core.Mathematics.Color.LimeGreen,
                _ => global::Stride.Core.Mathematics.Color.LightGray
            };

            _itemList.Children.Add(new TextBlock
            {
                Text = $"{item.Name}{countSuffix}{durSuffix}  ({item.Weight:F1} kg)",
                TextSize = 14,
                TextColor = color,
                Margin = new Thickness(5, 2, 0, 2)
            });
        }
    }
}
