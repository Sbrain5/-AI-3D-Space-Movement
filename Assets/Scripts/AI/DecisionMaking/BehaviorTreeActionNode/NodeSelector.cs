using System.Collections.Generic;

/// <summary>
/// Behavior Tree selector that evaluates only the currently active node.
/// </summary>
public sealed class NodeSelector : Node
{
    #region Variables

    private readonly List<Node> nodes;
    private Node activeNode;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a selector with a fixed node set.
    /// </summary>
    /// <param name="nodeList">Nodes that can be activated by this selector.</param>
    public NodeSelector(List<Node> nodeList)
    {
        nodes = nodeList != null ? nodeList : new List<Node>();
        activeNode = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Evaluates the currently active node.
    /// </summary>
    public override void Evaluate()
    {
        if (activeNode == null)
        {
            return;
        }

        activeNode.Evaluate();
    }

    /// <summary>
    /// Switches which node is evaluated.
    /// </summary>
    /// <param name="node">Node to activate.</param>
    public void SwitchActiveNode(Node node)
    {
        if (node == null)
        {
            return;
        }

        if (activeNode == node)
        {
            return;
        }

        if (!nodes.Contains(node))
        {
            return;
        }

        activeNode = node;
    }

    /// <summary>
    /// Returns the currently active node.
    /// </summary>
    /// <returns>Active node or null.</returns>
    public Node GetActiveNode()
    {
        return activeNode;
    }

    #endregion
}