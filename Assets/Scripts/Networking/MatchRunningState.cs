using PurrNet.StateMachine;

/// <summary>
/// State representing the match running phase. This state is active when the match is in progress and players are actively playing.
/// </summary>
public sealed class MatchRunningState : StateNode
{
    #region State Lifecycle

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);
    }

    #endregion
}