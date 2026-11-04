namespace LabApp.Core;

public sealed class TreeNode<T>
{
    public T Value { get; init; } = default!;
    public TreeNode<T>? Left { get; set; }
    public TreeNode<T>? Right { get; set; }
}

public sealed class BinaryTree<T>
{
    public TreeNode<T>? Root { get; set; }
}