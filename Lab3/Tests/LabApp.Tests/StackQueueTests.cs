using LabApp.Core;
using Xunit;

public class StackQueueTests
{
    [Fact] public void LinkedListStack_LIFO()
    {
        var s = new LinkedListStack<int>();
        s.Push(1); s.Push(2); s.Push(3);
        Assert.Equal(3, s.Pop());
        Assert.Equal(2, s.Top());
        Assert.False(s.IsEmpty());
    }

    [Fact] public void StandardStack_LIFO()
    {
        var s = new StandardStackAdapter<int>();
        s.Push(1); s.Push(2); s.Push(3);
        Assert.Equal(3, s.Pop());
        Assert.Equal(2, s.Top());
    }

    [Fact] public void ListQueue_FIFO()
    {
        var q = new ListQueue<int>();
        q.Enqueue(1); q.Enqueue(2); q.Enqueue(3);
        Assert.Equal(1, q.Dequeue());
        Assert.Equal(2, q.Peek());
    }

    [Fact] public void StandardQueue_FIFO()
    {
        var q = new StandardQueueAdapter<int>();
        q.Enqueue(1); q.Enqueue(2); q.Enqueue(3);
        Assert.Equal(1, q.Dequeue());
        Assert.Equal(2, q.Peek());
    }

    [Fact] public void BothStacks_GiveSameResults()
    {
        var a = new LinkedListStack<string>();
        var b = new StandardStackAdapter<string>();
        foreach (var v in new[] { "a", "b", "c" }) { a.Push(v); b.Push(v); }
        while (!a.IsEmpty()) Assert.Equal(a.Pop(), b.Pop());
    }

    [Fact] public void BothQueues_GiveSameResults()
    {
        var a = new ListQueue<string>();
        var b = new StandardQueueAdapter<string>();
        foreach (var v in new[] { "x", "y", "z" }) { a.Enqueue(v); b.Enqueue(v); }
        while (!a.IsEmpty()) Assert.Equal(a.Dequeue(), b.Dequeue());
    }
}