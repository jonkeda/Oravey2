namespace Oravey2.Core.Dialogue;

public sealed class GiveCapsAction : IConsequenceAction
{
    public int Amount { get; }

    public GiveCapsAction(int amount)
    {
        Amount = amount;
    }

    public void Execute(DialogueContext context)
        => context.Inventory.Caps += Amount;
}
