namespace Oravey2.Core.AI.Group;

public sealed record GroupMember(
    string Id,
    float X, float Y,
    float HealthPercent,
    bool IsAlive);

public enum GroupRole
{
    Attack,
    Flank,
    Retreat
}

public sealed record GroupAssignment(string MemberId, GroupRole Role, string? FocusTargetId);

public sealed class AIGroupCoordinator
{
    public const float FlankAngleThreshold = 45f;
    public const int FocusFireThreshold = 3;
    public const float RetreatThreshold = 0.50f;

    public IReadOnlyList<GroupAssignment> UpdateGroup(
        IReadOnlyList<GroupMember> group,
        float targetX, float targetY,
        string targetId)
    {
        var alive = group.Where(m => m.IsAlive).ToList();
        if (alive.Count == 0)
            return [];

        var totalMembers = group.Count;
        var deadOrFled = totalMembers - alive.Count;
        var shouldRetreat = totalMembers > 0 && (float)deadOrFled / totalMembers >= RetreatThreshold;

        if (shouldRetreat)
            return alive.Select(m => new GroupAssignment(m.Id, GroupRole.Retreat, null)).ToList();

        var assignments = new List<GroupAssignment>();
        string? focusTarget = alive.Count >= FocusFireThreshold ? targetId : null;

        foreach (var member in alive)
        {
            var role = ShouldFlank(member, targetX, targetY, alive)
                ? GroupRole.Flank
                : GroupRole.Attack;
            assignments.Add(new GroupAssignment(member.Id, role, focusTarget));
        }

        return assignments;
    }

    private static bool ShouldFlank(GroupMember member, float targetX, float targetY,
                                     List<GroupMember> allies)
    {
        if (allies.Count < 2) return false;

        var centroidX = allies.Average(a => a.X);
        var centroidY = allies.Average(a => a.Y);

        var groupAngle = MathF.Atan2(targetY - centroidY, targetX - centroidX) * (180f / MathF.PI);
        var memberAngle = MathF.Atan2(targetY - member.Y, targetX - member.X) * (180f / MathF.PI);

        var delta = MathF.Abs(NormalizeAngle(memberAngle - groupAngle));
        return delta > FlankAngleThreshold;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
