/// <summary>
/// Base Behavior Tree node contract. Concrete nodes implement Evaluate to execute one step of logic.
/// </summary>
public abstract class Node
{
    #region Public API

    /// <summary>
    /// Executes the node logic once.
    /// </summary>
    public abstract void Evaluate();

    #endregion
}