using global::Stride.Engine;
using global::Stride.Graphics;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Quests;
using Oravey2.Core.World;
using Color = global::Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Persistent HUD widget (top-right) showing the active quest objective.
/// Reads from QuestLogComponent + WorldStateService each frame.
/// Fades out when no active quest.
/// </summary>
public class QuestTrackerScript : SyncScript
{
    public QuestLogComponent? QuestLog { get; set; }
    public WorldStateService? WorldState { get; set; }
    public GameStateManager? StateManager { get; set; }
    public SpriteFont? Font { get; set; }

    private TextBlock? _titleText;
    private TextBlock? _objectiveText;
    private TextBlock? _progressText;
    private StackPanel? _container;

    /// <summary>Exposes tracker visibility for automation queries.</summary>
    public bool IsVisible => _container?.Visibility == Visibility.Visible;

    /// <summary>Current quest ID being tracked, or null.</summary>
    public string? TrackedQuestId { get; private set; }

    /// <summary>Current objective text displayed.</summary>
    public string? ObjectiveText => _objectiveText?.Text;

    /// <summary>Current progress text displayed (e.g. "(2/3)").</summary>
    public string? ProgressText => _progressText?.Text;

    public override void Start()
    {
        base.Start();

        _titleText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 15,
            TextColor = new Color(255, 220, 50),
            Margin = new Thickness(8, 6, 8, 2),
        };

        _objectiveText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 13,
            TextColor = Color.White,
            Margin = new Thickness(12, 0, 8, 2),
        };

        _progressText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 13,
            TextColor = new Color(180, 220, 180),
            Margin = new Thickness(12, 0, 8, 6),
        };

        _container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundColor = new Color(0, 0, 0, 140),
            Margin = new Thickness(0, 10, 10, 0),
            MinimumWidth = 220,
            Children = { _titleText, _objectiveText, _progressText },
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = _container };
        Entity.Add(new UIComponent { Page = page });
    }

    public override void Update()
    {
        if (QuestLog == null || _container == null)
            return;

        // Find the first active quest
        string? activeQuestId = null;
        QuestDefinition? activeDef = null;
        string? currentStageId = null;

        foreach (var def in QuestChainDefinitions.All)
        {
            if (QuestLog.GetStatus(def.Id) == QuestStatus.Active)
            {
                activeQuestId = def.Id;
                activeDef = def;
                currentStageId = QuestLog.GetCurrentStage(def.Id);
                break;
            }
        }

        if (activeQuestId == null || activeDef == null || currentStageId == null)
        {
            _container.Visibility = Visibility.Collapsed;
            TrackedQuestId = null;
            return;
        }

        TrackedQuestId = activeQuestId;
        _container.Visibility = Visibility.Visible;

        _titleText!.Text = $"\u2B26 {activeDef.Title}";

        if (activeDef.Stages.TryGetValue(currentStageId, out var stage))
        {
            _objectiveText!.Text = stage.Description;

            // Show live counter progress for counter-based conditions
            var progress = GetProgressText(stage);
            _progressText!.Text = progress;
        }
    }

    private string GetProgressText(QuestStage stage)
    {
        if (WorldState == null) return "";

        foreach (var condition in stage.Conditions)
        {
            if (condition is QuestCounterCondition counter)
            {
                var current = WorldState.GetCounter(counter.CounterName);
                var target = counter.MinValue;
                return $"({current}/{target})";
            }
        }

        return "";
    }
}
