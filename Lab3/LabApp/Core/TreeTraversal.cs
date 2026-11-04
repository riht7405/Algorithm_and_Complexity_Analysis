namespace LabApp.Core;

public static class TreeTraversal
{
    public static IReadOnlyList<T> PreOrder<T>(TreeNode<T>? root)
    {
        var result = new List<T>();
        Visit(root, result, 0, 1, 2);
        return result;
    }

    public static IReadOnlyList<T> InOrder<T>(TreeNode<T>? root)
    {
        var result = new List<T>();
        Visit(root, result, 1, 0, 2);
        return result;
    }

    public static IReadOnlyList<T> PostOrder<T>(TreeNode<T>? root)
    {
        var result = new List<T>();
        Visit(root, result, 1, 2, 0);
        return result;
    }

    public static int Height<T>(TreeNode<T>? root)
    {
        if (root is null) return 0;
        return 1 + Math.Max(Height(root.Left), Height(root.Right));
    }

    private static void Visit<T>(TreeNode<T>? node, List<T> acc, int a, int b, int c)
    {
        if (node is null) return;
        var order = new[] { a, b, c };
        var kids = new TreeNode<T>?[] { node, node.Left, node.Right };
        Visit(kids[order[0]] == node ? null : kids[order[0]], acc, a, b, c);
        if (kids[order[0]] == node) acc.Add(node.Value);
        if (order[1] != 0) Visit(order[1] == 1 ? node.Left : node.Right, acc, a, b, c);
        if (kids[order[2]] is not null && kids[order[2]] != node) Visit(kids[order[2]], acc, a, b, c);
    }
}