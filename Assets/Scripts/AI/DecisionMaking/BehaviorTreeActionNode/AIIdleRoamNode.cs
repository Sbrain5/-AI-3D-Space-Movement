using UnityEngine;

/// <summary>
/// Behavior tree node that handles idle roaming by requesting random destinations for the mover agent when it has no active path command.
/// </summary>
public sealed class AIIdleRoamNode : Node
{
    #region Variables

    private readonly OctreeMoverAgent moverAgent;

    private float retryDelay;
    private float nextRetryTime;

    private bool hasStartedIdleRoam;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes the idle roam node.
    /// </summary>
    /// <param name="blackboard">Shared blackboard instance.</param>
    public AIIdleRoamNode(Blackboard blackboard)
    {
        moverAgent = blackboard.GetOctreeMoverAgent();

        retryDelay = 0.25f;
        nextRetryTime = 0f;

        hasStartedIdleRoam = false;
    }

    #endregion

    #region Behaviour

    /// <summary>
    /// Requests a random destination only when the mover has no active path command.
    /// </summary>
    public override void Evaluate()
    {
        if (moverAgent == null)
        {
            return;
        }

        if (!hasStartedIdleRoam)
        {
            hasStartedIdleRoam = true;
            nextRetryTime = 0f;
        }

        if (moverAgent.HasActiveCommand())
        {
            return;
        }

        if (Time.time < nextRetryTime)
        {
            return;
        }

        TryStartNextRoamPath();
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets internal idle roam state so it can request a destination immediately.
    /// </summary>
    public void ResetState()
    {
        hasStartedIdleRoam = false;
        nextRetryTime = 0f;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Tries to start the next random roaming path and schedules a retry if pathing fails.
    /// </summary>
    private void TryStartNextRoamPath()
    {
        bool commandAccepted = moverAgent.SetRandomDestination(true);

        if (commandAccepted)
        {
            nextRetryTime = 0f;
            return;
        }

        nextRetryTime = Time.time + Mathf.Max(0.05f, retryDelay);
    }

    #endregion
}