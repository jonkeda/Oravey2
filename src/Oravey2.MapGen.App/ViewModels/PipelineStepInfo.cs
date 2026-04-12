namespace Oravey2.MapGen.App.ViewModels;

public sealed class PipelineStepInfo
{
    private static readonly string[] StepNumbers = ["\u2460", "\u2461", "\u2462", "\u2463", "\u2464", "\u2465", "\u2466", "\u2467"];

    public int Number { get; }
    public string Name { get; }
    public string Icon { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsCompleted { get; set; }

    public PipelineStepInfo(int number, string name)
    {
        Number = number;
        Name = name;
        Icon = "\U0001F512"; // locked
    }

    public void UpdateStatus(bool isCompleted, bool isCurrent, bool isUnlocked)
    {
        IsCompleted = isCompleted;
        IsCurrent = isCurrent;
        IsUnlocked = isUnlocked;

        Icon = isCompleted ? "\u2705" : isCurrent ? "\U0001F535" : "\U0001F512";
    }

    public string DisplayText => $"{StepNumbers[Number - 1]} {Icon} {Name}";
    public string AutomationId => $"WizardStep{Number}";
}
