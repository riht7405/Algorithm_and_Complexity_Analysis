namespace LabApp.Core;

public interface IStack<T>
{
    int Count { get; }
    void Push(T value);
    T Pop();
    T Top();
    bool IsEmpty();
    IReadOnlyList<T> Print();
}

public sealed class LinkedListStack<T> : IStack<T>
{
    private readonly SinglyLinkedList<T> _inner = new();
    public int Count => _inner.Count;
    public void Push(T value) => _inner.AddFirst(value);
    public T Pop() => _inner.RemoveFirst();
    public T Top()
    {
        var head = _inner.Head;
        if (head is null) throw new InvalidOperationException("Стек пуст");
        return head.Value;
    }
    public bool IsEmpty() => _inner.IsEmpty;
    public IReadOnlyList<T> Print() => _inner.Items.ToList();
}

public sealed class StandardStackAdapter<T> : IStack<T>
{
    private readonly System.Collections.Generic.Stack<T> _inner = new();
    public int Count => _inner.Count;
    public void Push(T value) => _inner.Push(value);
    public T Pop() => _inner.Pop();
    public T Top() => _inner.Peek();
    public bool IsEmpty() => _inner.Count == 0;
    public IReadOnlyList<T> Print() => _inner.ToList();
}