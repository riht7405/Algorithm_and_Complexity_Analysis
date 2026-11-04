namespace LabApp.Core;

public sealed class ListNode<T>
{
    public T Value { get; set; }
    public ListNode<T>? Next { get; set; }
    public ListNode(T value) => Value = value;
}