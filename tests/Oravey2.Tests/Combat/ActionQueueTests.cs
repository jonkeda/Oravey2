using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class ActionQueueTests
{
    private static CombatAction MakeAction(string actor = "player", CombatActionType type = CombatActionType.MeleeAttack)
        => new(actor, type);

    [Fact]
    public void Empty_CountZero()
    {
        var q = new ActionQueue();
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void Enqueue_IncreasesCount()
    {
        var q = new ActionQueue();
        q.Enqueue(MakeAction());
        Assert.Equal(1, q.Count);
    }

    [Fact]
    public void Dequeue_ReturnsFirst()
    {
        var q = new ActionQueue();
        var a = MakeAction("a");
        var b = MakeAction("b");
        q.Enqueue(a);
        q.Enqueue(b);
        Assert.Equal(a, q.Dequeue());
    }

    [Fact]
    public void Dequeue_Empty_ReturnsNull()
    {
        var q = new ActionQueue();
        Assert.Null(q.Dequeue());
    }

    [Fact]
    public void Peek_DoesNotRemove()
    {
        var q = new ActionQueue();
        q.Enqueue(MakeAction());
        q.Peek();
        Assert.Equal(1, q.Count);
    }

    [Fact]
    public void Peek_Empty_ReturnsNull()
    {
        var q = new ActionQueue();
        Assert.Null(q.Peek());
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var q = new ActionQueue();
        q.Enqueue(MakeAction("a"));
        q.Enqueue(MakeAction("b"));
        q.Enqueue(MakeAction("c"));
        q.Clear();
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void PendingActions_ReflectsQueue()
    {
        var q = new ActionQueue();
        var a = MakeAction("a");
        var b = MakeAction("b");
        q.Enqueue(a);
        q.Enqueue(b);
        var pending = q.PendingActions;
        Assert.Equal(2, pending.Count);
        Assert.Equal(a, pending[0]);
        Assert.Equal(b, pending[1]);
    }

    [Fact]
    public void FIFO_Order()
    {
        var q = new ActionQueue();
        var a = MakeAction("a");
        var b = MakeAction("b");
        var c = MakeAction("c");
        q.Enqueue(a);
        q.Enqueue(b);
        q.Enqueue(c);
        Assert.Equal(a, q.Dequeue());
        Assert.Equal(b, q.Dequeue());
        Assert.Equal(c, q.Dequeue());
    }

    [Fact]
    public void Enqueue_Null_Throws()
    {
        var q = new ActionQueue();
        Assert.Throws<ArgumentNullException>(() => q.Enqueue(null!));
    }
}
