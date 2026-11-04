namespace LabApp.Core;

public static class LinkedListAlgorithms
{
    public static void Reverse(SinglyLinkedList<int> list)
    {
        ListNode<int>? prev = null, cur = list.Head;
        while (cur is not null)
        {
            var nxt = cur.Next;
            cur.Next = prev;
            prev = cur;
            cur = nxt;
        }
        list.SetHeadOnly(prev);
        list.RecomputeTailAndCount();
    }

    public static void MoveLastToFront(SinglyLinkedList<int> list)
    {
        if (list.Head is null || list.Head == list.Tail) return;
        var last = list.RemoveLast();
        list.AddFirst(last);
    }

    public static void MoveFirstToEnd(SinglyLinkedList<int> list)
    {
        if (list.Head is null || list.Head == list.Tail) return;
        var first = list.RemoveFirst();
        list.AddLast(first);
    }

    public static int CountDistinct(SinglyLinkedList<int> list)
        => list.Items.Distinct().Count();

    public static void RemoveNonUnique(SinglyLinkedList<int> list)
    {
        var counts = list.Items.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        RemoveWhere(list, x => counts[x] > 1);
    }

    public static void InsertSelfAfterX(SinglyLinkedList<int> list, int x)
    {
        var cur = list.Head;
        while (cur is not null && cur.Value != x) cur = cur.Next;
        if (cur is null) return;

        var snapshot = list.Items.ToList();
        var insertAt = cur;
        foreach (var v in snapshot)
        {
            var node = new ListNode<int>(v) { Next = insertAt.Next };
            insertAt.Next = node;
            insertAt = node;
        }
        list.RecomputeTailAndCount();
    }

    public static void SortedInsert(SinglyLinkedList<int> list, int e)
    {
        if (list.Head is null || e <= list.Head.Value) { list.AddFirst(e); return; }
        var cur = list.Head;
        while (cur.Next is not null && cur.Next.Value < e) cur = cur.Next;
        var node = new ListNode<int>(e) { Next = cur.Next };
        cur.Next = node;
        list.RecomputeTailAndCount();
    }

    public static void RemoveAll(SinglyLinkedList<int> list, int e)
        => RemoveWhere(list, x => x == e);

    public static void InsertBefore(SinglyLinkedList<int> list, int e, int f)
    {
        if (list.Head is null) return;
        if (list.Head.Value == e) { list.AddFirst(f); return; }
        var cur = list.Head;
        while (cur.Next is not null && cur.Next.Value != e) cur = cur.Next;
        if (cur.Next is null) return;
        var node = new ListNode<int>(f) { Next = cur.Next };
        cur.Next = node;
        list.RecomputeTailAndCount();
    }

    public static void Append(SinglyLinkedList<int> l, SinglyLinkedList<int> e)
    {
        foreach (var v in e.Items.ToList()) l.AddLast(v);
    }

    public static (SinglyLinkedList<int> left, SinglyLinkedList<int> right) SplitByValue(
        SinglyLinkedList<int> list, int x)
    {
        var left = new SinglyLinkedList<int>();
        var right = new SinglyLinkedList<int>();
        var found = false;
        foreach (var v in list.Items)
        {
            if (!found && v == x) { found = true; right.AddLast(v); }
            else if (found) right.AddLast(v);
            else left.AddLast(v);
        }
        return (left, right);
    }

    public static void Double(SinglyLinkedList<int> list)
    {
        var snapshot = list.Items.ToList();
        foreach (var v in snapshot) list.AddLast(v);
    }

    public static bool Swap(SinglyLinkedList<int> list, int a, int b)
    {
        if (a == b) return false;

        ListNode<int>? pa = null, pb = null, prevA = null, prevB = null, prev = null;
        for (var cur = list.Head; cur is not null; cur = cur.Next)
        {
            if (cur.Value == a && pa is null) { pa = cur; prevA = prev; }
            if (cur.Value == b && pb is null) { pb = cur; prevB = prev; }
            prev = cur;
        }
        if (pa is null || pb is null) return false;

        if (pa.Next == pb)
        {
            if (prevA is not null) prevA.Next = pb; else list.SetHeadOnly(pb);
            pa.Next = pb.Next;
            pb.Next = pa;
        }
        else if (pb.Next == pa)
        {
            if (prevB is not null) prevB.Next = pa; else list.SetHeadOnly(pa);
            pb.Next = pa.Next;
            pa.Next = pb;
        }
        else
        {
            if (prevA is not null) prevA.Next = pb; else list.SetHeadOnly(pb);
            if (prevB is not null) prevB.Next = pa; else list.SetHeadOnly(pa);
            var tmp = pa.Next;
            pa.Next = pb.Next;
            pb.Next = tmp;
        }
        list.RecomputeTailAndCount();
        return true;
    }

    private static void RemoveWhere(SinglyLinkedList<int> list, Func<int, bool> pred)
    {
        while (list.Head is not null && pred(list.Head.Value))
        {
            list.SetHeadOnly(list.Head.Next);
        }
        var cur = list.Head;
        while (cur?.Next is not null)
        {
            if (pred(cur.Next.Value)) cur.Next = cur.Next.Next;
            else cur = cur.Next;
        }
        list.RecomputeTailAndCount();
    }
}