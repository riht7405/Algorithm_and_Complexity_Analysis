namespace LabApp.Core;

public static class PrefixParser
{
    public static BinaryTree<char> ParsePrefix(string s)
    {
        int pos = 0;
        var root = Parse(s, ref pos);
        return new BinaryTree<char> { Root = root };
    }

    private static TreeNode<char>? Parse(string s, ref int pos)
    {
        if (pos >= s.Length) return null;
        var c = s[pos++];
        if (c == '*') return null;
        var node = new TreeNode<char> { Value = c };
        node.Left = Parse(s, ref pos);
        node.Right = Parse(s, ref pos);
        return node;
    }
}