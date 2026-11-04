namespace LabApp.Core;

public sealed class SinglyLinkedList<T>
{
    public ListNode<T>? Head { get; private set; }
    public ListNode<T>? Tail { get; private set; }
    public int Count { get; private set; }
    public bool IsEmpty => Head is null;

    public void AddFirst(T value)
    {
        var node = new ListNode<T>(value) { Next = Head };
        Head = node;
        Tail ??= node;
        Count++;
    }

    public void AddLast(T value)
    {
        var node = new ListNode<T>(value);
        if (Tail is null) { Head = Tail = node; }
        else { Tail.Next = node; Tail = node; }
        Count++;
    }

    public T RemoveFirst()
    {
        if (Head is null) throw new InvalidOperationException("Список пуст");
        var v = Head.Value;
        Head = Head.Next;
        if (Head is null) Tail = null;
        Count--;
        return v;
    }

    public T RemoveLast()
    {
        if (Head is null) throw new InvalidOperationException("Список пуст");
        if (Head == Tail) { var v0 = Head.Value; Head = Tail = null; Count--; return v0; }
        var cur = Head;
        while (cur!.Next != Tail) cur = cur.Next;
        var v = Tail!.Value;
        cur.Next = null;
        Tail = cur;
        Count--;
        return v;
    }

    public void SetHeadOnly(ListNode<T>? head) => Head = head;

    public void RecomputeTailAndCount()
    {
        Tail = null;
        int c = 0;
        for (var cur = Head; cur is not null; cur = cur.Next) { Tail = cur; c++; }
        Count = c;
    }

    public IEnumerable<T> Items
    {
        get
        {
            for (var cur = Head; cur is not null; cur = cur.Next) yield return cur.Value;
        }
    }

    public SinglyLinkedList<T> Clone()
    {
        var copy = new SinglyLinkedList<T>();
        foreach (var v in Items) copy.AddLast(v);
        return copy;
    }

    public override string ToString() => "[" + string.Join(", ", Items) + "]";
}