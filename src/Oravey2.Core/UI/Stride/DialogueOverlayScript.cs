using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.NPC;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Renders the dialogue panel: speaker name, text, and numbered choices.
/// Visible when GameState == InDialogue.
/// Text uses black outlines (8-direction shadow copies) for readability.
/// Auto-closes on ESC or when player walks too far from NPC.
/// Choices are clickable buttons as well as keyboard-selectable.
/// </summary>
public class DialogueOverlayScript : SyncScript
{
    public DialogueProcessor? Processor { get; set; }
    public DialogueContext? Context { get; set; }
    public GameStateManager? StateManager { get; set; }
    public IInputProvider? InputProvider { get; set; }
    public SpriteFont? Font { get; set; }
    public Entity? PlayerEntity { get; set; }
    public float CloseDistance { get; set; } = 4.0f;

    private Border? _overlay;
    private TextBlock? _speakerText;
    private TextBlock[] _speakerShadows = [];
    private TextBlock? _dialogueText;
    private TextBlock[] _dialogueShadows = [];
    private TextBlock?[] _choiceTexts = [];
    private TextBlock[][] _choiceShadows = [];
    private Button[] _choiceButtons = [];

    // Shadow offsets: 8 directions around center (1,1) for outline effect
    private static readonly (int Left, int Top)[] ShadowPositions =
        [(0, 1), (2, 1), (1, 0), (1, 2), (0, 0), (2, 0), (0, 2), (2, 2)];

    public override void Start()
    {
        base.Start();

        var (speakerEl, speakerMain, speakerShadows) = CreateOutlinedText(
            20, new Color(255, 220, 100), new Thickness(10, 8, 0, 4));
        _speakerText = speakerMain;
        _speakerShadows = speakerShadows;

        var (dialogueEl, dialogueMain, dialogueShadows) = CreateOutlinedText(
            16, Color.White, new Thickness(10, 0, 0, 12));
        _dialogueText = dialogueMain;
        _dialogueShadows = dialogueShadows;

        _choiceTexts = new TextBlock[4];
        _choiceShadows = new TextBlock[4][];
        _choiceButtons = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            var (el, main, shadows) = CreateOutlinedText(
                14, Color.White, new Thickness(0, 0, 0, 0));
            _choiceTexts[i] = main;
            _choiceShadows[i] = shadows;

            var capturedIndex = i;
            var btn = new Button
            {
                Content = el,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(20, 2, 0, 2),
                BackgroundColor = Color.Transparent,
                PressedImage = null,
                MouseOverImage = null,
                NotPressedImage = null,
                Visibility = Visibility.Collapsed,
            };
            btn.Click += (_, _) => OnChoiceClicked(capturedIndex);
            _choiceButtons[i] = btn;
        }

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                speakerEl, dialogueEl,
                _choiceButtons[0], _choiceButtons[1], _choiceButtons[2], _choiceButtons[3],
            },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(0, 0, 0, 255),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 220,
            Content = stack,
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = _overlay };
        Entity.Add(new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 });
    }

    public override void Update()
    {
        if (StateManager?.CurrentState != GameState.InDialogue || Processor?.IsActive != true)
        {
            if (_overlay != null) _overlay.Visibility = Visibility.Collapsed;
            return;
        }

        // ESC closes dialogue
        if (InputProvider?.IsActionPressed(GameAction.Pause) == true)
        {
            CloseDialogue();
            return;
        }

        // Walk-away check: close if player moved too far from NPC
        if (PlayerEntity != null && Processor.ActiveTree != null)
        {
            var npcEntity = FindNpcEntity(Processor.ActiveTree.Id);
            if (npcEntity != null)
            {
                var dist = (PlayerEntity.Transform.Position - npcEntity.Transform.Position).Length();
                if (dist > CloseDistance)
                {
                    CloseDialogue();
                    return;
                }
            }
        }

        if (_overlay != null) _overlay.Visibility = Visibility.Visible;

        var node = Processor.CurrentNode;
        if (node == null) return;

        SetOutlinedText(_speakerText!, _speakerShadows, node.Speaker);
        SetOutlinedText(_dialogueText!, _dialogueShadows, $"\"{node.Text}\"");

        var choices = Processor.GetAvailableChoices(Context!);
        for (int i = 0; i < 4; i++)
        {
            if (i < choices.Count)
            {
                SetOutlinedText(_choiceTexts[i]!, _choiceShadows[i], $"{i + 1}. {choices[i].Choice.Text}");
                _choiceTexts[i]!.TextColor = choices[i].Available ? Color.White : new Color(100, 100, 100);
                _choiceButtons[i].Visibility = Visibility.Visible;
            }
            else
            {
                _choiceButtons[i].Visibility = Visibility.Collapsed;
            }
        }

        // Handle choice input (keyboard)
        if (InputProvider == null) return;

        GameAction[] choiceActions =
            [GameAction.DialogueChoice1, GameAction.DialogueChoice2, GameAction.DialogueChoice3, GameAction.DialogueChoice4];

        for (int i = 0; i < choiceActions.Length && i < choices.Count; i++)
        {
            if (InputProvider.IsActionPressed(choiceActions[i]))
            {
                SelectChoice(i);
                break;
            }
        }
    }

    private void OnChoiceClicked(int index)
    {
        SelectChoice(index);
    }

    private void SelectChoice(int index)
    {
        if (Processor == null || Context == null) return;

        var selected = Processor.SelectChoice(index, Context);
        if (selected && !Processor.IsActive)
        {
            StateManager?.TransitionTo(GameState.Exploring);
        }
    }

    private void CloseDialogue()
    {
        Processor?.EndDialogue();
        StateManager?.TransitionTo(GameState.Exploring);
    }

    private Entity? FindNpcEntity(string dialogueTreeId)
    {
        foreach (var entity in Entity.Scene.Entities)
        {
            var npc = entity.Get<NpcComponent>();
            if (npc?.Definition?.DialogueTreeId == dialogueTreeId)
                return entity;
        }
        return null;
    }

    private (UIElement Element, TextBlock Main, TextBlock[] Shadows) CreateOutlinedText(
        float textSize, Color textColor, Thickness outerMargin)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = outerMargin,
        };

        var shadows = new TextBlock[ShadowPositions.Length];
        for (int i = 0; i < ShadowPositions.Length; i++)
        {
            shadows[i] = new TextBlock
            {
                Text = "",
                Font = Font,
                TextSize = textSize,
                TextColor = Color.Black,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(ShadowPositions[i].Left, ShadowPositions[i].Top, 0, 0),
            };
            grid.Children.Add(shadows[i]);
        }

        var main = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = textSize,
            TextColor = textColor,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(1, 1, 0, 0),
        };
        grid.Children.Add(main);

        return (grid, main, shadows);
    }

    private static void SetOutlinedText(TextBlock main, TextBlock[] shadows, string text)
    {
        main.Text = text;
        foreach (var s in shadows) s.Text = text;
    }
}
