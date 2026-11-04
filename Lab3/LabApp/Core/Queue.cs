namespace LabApp.Core;

public interface IQueue<T>
{
    int Count { get; }
    void Enqueue(T value);
    T Dequeue();
    T Peek();
    bool IsEmpty();
    IReadOnlyList<T> Print();
}

public sealed class ListQueue<T> : IQueue<T>
{
    private readonly SinglyLinkedList<T> _inner = new();
    public int Count => _inner.Count;
    public void Enqueue(T value) => _inner.AddLast(value);
    public T Dequeue() => _inner.RemoveFirst();
    public T Peek()
    {
        var head = _inner.Head;
        if (head is null) throw new InvalidOperationException("Очередь пуста");
        return head.Value;
    }
    public bool IsEmpty() => _inner.IsEmpty;
    public IReadOnlyList<T> Print() => _inner.Items.ToList();
}

public sealed class StandardQueueAdapter<T> : IQueue<T>
{
    private readonly System.Collections.Generic.Queue<T> _inner = new();
    public int Count => _inner.Count;
    public void Enqueue(T value) => _inner.Enqueue(value);
    public T Dequeue() => _inner.Dequeue();
    public T Peek() => _inner.Peek();
    public bool IsEmpty() => _inner.Count == 0;
    public IReadOnlyList<T> Print() => _inner.ToList();
}