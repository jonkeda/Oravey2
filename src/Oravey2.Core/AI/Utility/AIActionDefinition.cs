namespace Oravey2.Core.AI.Utility;

public sealed record AIActionDefinition(
    string Name,
    AIConsideration[] Considerations);
