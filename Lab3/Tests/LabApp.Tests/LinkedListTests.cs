using LabApp.Core;
using Xunit;

public class LinkedListTests
{
    private static SinglyLinkedList<int> Make(params int[] xs)
    {
        var l = new SinglyLinkedList<int>();
        foreach (var x in xs) l.AddLast(x);
        return l;
    }

    [Fact] public void Reverse_Works()
    {
        var l = Make(1, 2, 3, 4);
        LinkedListAlgorithms.Reverse(l);
        Assert.Equal(new[] { 4, 3, 2, 1 }, l.Items);
    }

    [Fact] public void Reverse_Empty_Ok()
    {
        var l = new SinglyLinkedList<int>();
        LinkedListAlgorithms.Reverse(l);
        Assert.Empty(l.Items);
    }

    [Fact] public void Reverse_Twice_Identity()
    {
        var l = Make(1, 2, 3);
        LinkedListAlgorithms.Reverse(l);
        LinkedListAlgorithms.Reverse(l);
        Assert.Equal(new[] { 1, 2, 3 }, l.Items);
    }

    [Fact] public void MoveLastToFront_Works()
    {
        var l = Make(1, 2, 3);
        LinkedListAlgorithms.MoveLastToFront(l);
        Assert.Equal(new[] { 3, 1, 2 }, l.Items);
    }

    [Fact] public void CountDistinct_Works()
    {
        Assert.Equal(2, LinkedListAlgorithms.CountDistinct(Make(1, 1, 2, 2, 2, 1)));
    }

    [Fact] public void RemoveNonUnique_LeavesOnlyUniques()
    {
        var l = Make(1, 1, 2, 3, 3, 4);
        LinkedListAlgorithms.RemoveNonUnique(l);
        Assert.Equal(new[] { 2, 4 }, l.Items);
    }

    [Fact] public void RemoveAll_Works()
    {
        var l = Make(1, 2, 1, 3, 1);
        LinkedListAlgorithms.RemoveAll(l, 1);
        Assert.Equal(new[] { 2, 3 }, l.Items);
    }

    [Fact] public void InsertBefore_Works()
    {
        var l = Make(1, 2, 3);
        LinkedListAlgorithms.InsertBefore(l, 2, 99);
        Assert.Equal(new[] { 1, 99, 2, 3 }, l.Items);
    }

    [Fact] public void SortedInsert_KeepsOrder()
    {
        var l = Make(1, 3, 5);
        LinkedListAlgorithms.SortedInsert(l, 4);
        Assert.Equal(new[] { 1, 3, 4, 5 }, l.Items);
    }

    [Fact] public void SplitByValue_Works()
    {
        var (left, right) = LinkedListAlgorithms.SplitByValue(Make(1, 2, 3, 4), 3);
        Assert.Equal(new[] { 1, 2 }, left.Items);
        Assert.Equal(new[] { 3, 4 }, right.Items);
    }

    [Fact] public void SplitByValue_Missing_RightEmpty()
    {
        var (left, right) = LinkedListAlgorithms.SplitByValue(Make(1, 2), 99);
        Assert.Equal(new[] { 1, 2 }, left.Items);
        Assert.Empty(right.Items);
    }

    [Fact] public void Double_Works()
    {
        var l = Make(1, 2);
        LinkedListAlgorithms.Double(l);
        Assert.Equal(new[] { 1, 2, 1, 2 }, l.Items);
    }

    [Fact] public void Append_Works()
    {
        var l = Make(1, 2);
        LinkedListAlgorithms.Append(l, Make(3, 4));
        Assert.Equal(new[] { 1, 2, 3, 4 }, l.Items);
    }

    [Fact] public void Swap_Adjacent()
    {
        var l = Make(1, 2, 3);
        Assert.True(LinkedListAlgorithms.Swap(l, 1, 2));
        Assert.Equal(new[] { 2, 1, 3 }, l.Items);
    }

    [Fact] public void Swap_NonAdjacent()
    {
        var l = Make(1, 2, 3, 4, 5);
        Assert.True(LinkedListAlgorithms.Swap(l, 2, 4));
        Assert.Equal(new[] { 1, 4, 3, 2, 5 }, l.Items);
    }

    [Fact] public void Swap_Missing_ReturnsFalse()
    {
        var l = Make(1, 2, 3);
        Assert.False(LinkedListAlgorithms.Swap(l, 1, 99));
        Assert.Equal(new[] { 1, 2, 3 }, l.Items);
    }
}