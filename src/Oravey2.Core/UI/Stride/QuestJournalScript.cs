using global::Stride.Engine;
using global::Stride.Graphics;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Quests;
using Oravey2.Core.World;
using Color = global::Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Full-screen quest journal overlay toggled with J key.
/// Lists active and completed quests with details.
/// Closes with J or Escape.
/// </summary>
public class QuestJournalScript : SyncScript
{
    public QuestLogComponent? QuestLog { get; set; }
    public WorldStateService? WorldState { get; set; }
    public GameStateManager? StateManager { get; set; }
    public IInputProvider? InputProvider { get; set; }
    public SpriteFont? Font { get; set; }

    private UIComponent? _uiComponent;
    private StackPanel? _questList;
    private bool _visible;

    /// <summary>Exposes journal visibility for automation queries.</summary>
    public bool IsVisible => _visible;

    public override void Start()
    {
        base.Start();
        BuildUI();
    }

    public override void Update()
    {
        if (StateManager?.CurrentState is GameState.GameOver) return;

        // Toggle on J
        if (InputProvider?.IsActionPressed(GameAction.OpenJournal) == true)
            Toggle();

        // Close on Escape
        if (_visible && InputProvider?.IsActionPressed(GameAction.Pause) == true)
        {
            _visible = false;
            ApplyVisibility();
        }
    }

    public void Toggle()
    {
        _visible = !_visible;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_uiComponent?.Page?.RootElement == null) return;

        if (_visible)
        {
            RefreshQuests();
            _uiComponent.Page.RootElement.Visibility = Visibility.Visible;
        }
        else
        {
            _uiComponent.Page.RootElement.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildUI()
    {
        var header = new TextBlock
        {
            Text = "=== QUEST JOURNAL ===",
            Font = Font,
            TextSize = 20,
            TextColor = Color.Gold,
            Margin = new Thickness(10, 10, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _questList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10, 0, 10, 10),
        };

        var closeHint = new TextBlock
        {
            Text = "[J / Esc to close]",
            Font = Font,
            TextSize = 12,
            TextColor = Color.Gray,
            Margin = new Thickness(10, 5, 10, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BackgroundColor = new Color(0, 0, 0, 200),
            Width = 500,
            Children = { header, _questList, closeHint },
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = container };
        _uiComponent = new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 };
        Entity.Add(_uiComponent);
    }

    private void RefreshQuests()
    {
        if (QuestLog == null || _questList == null) return;

        _questList.Children.Clear();

        var hasEntries = false;

        // Active quests
        foreach (var def in QuestChainDefinitions.All)
        {
            var status = QuestLog.GetStatus(def.Id);
            if (status != QuestStatus.Active) continue;

            hasEntries = true;
            AddQuestEntry(def, status);
        }

        // Completed quests
        foreach (var def in QuestChainDefinitions.All)
        {
            var status = QuestLog.GetStatus(def.Id);
            if (status != QuestStatus.Completed) continue;

            hasEntries = true;
            AddQuestEntry(def, status);
        }

        if (!hasEntries)
        {
            _questList.Children.Add(new TextBlock
            {
                Text = "No quests yet.",
                Font = Font,
                TextSize = 14,
                TextColor = Color.Gray,
                Margin = new Thickness(5, 10, 0, 2),
            });
        }
    }

    private void AddQuestEntry(QuestDefinition def, QuestStatus status)
    {
        var statusIcon = status == QuestStatus.Active ? "\u25CF" : "\u25CB";
        var statusColor = status == QuestStatus.Active
            ? new Color(100, 255, 100)
            : new Color(150, 150, 150);

        _questList!.Children.Add(new TextBlock
        {
            Text = $"{statusIcon} {def.Title} [{status}]",
            Font = Font,
            TextSize = 16,
            TextColor = statusColor,
            Margin = new Thickness(5, 8, 0, 2),
        });

        _questList.Children.Add(new TextBlock
        {
            Text = def.Description,
            Font = Font,
            TextSize = 13,
            TextColor = Color.LightGray,
            Margin = new Thickness(20, 0, 0, 2),
        });

        if (status == QuestStatus.Active)
        {
            var stageId = QuestLog!.GetCurrentStage(def.Id);
            if (stageId != null && def.Stages.TryGetValue(stageId, out var stage))
            {
                var progress = GetProgressSuffix(stage);
                _questList.Children.Add(new TextBlock
                {
                    Text = $"Objective: {stage.Description}{progress}",
                    Font = Font,
                    TextSize = 13,
                    TextColor = new Color(180, 220, 180),
                    Margin = new Thickness(20, 0, 0, 2),
                });
            }
        }

        if (def.XPReward > 0)
        {
            _questList.Children.Add(new TextBlock
            {
                Text = $"Reward: {def.XPReward} XP",
                Font = Font,
                TextSize = 12,
                TextColor = new Color(200, 200, 100),
                Margin = new Thickness(20, 0, 0, 4),
            });
        }
    }

    private string GetProgressSuffix(QuestStage stage)
    {
        if (WorldState == null) return "";

        foreach (var condition in stage.Conditions)
        {
            if (condition is QuestCounterCondition counter)
            {
                var current = WorldState.GetCounter(counter.CounterName);
                return $" ({current}/{counter.MinValue})";
            }
        }

        return "";
    }
}
