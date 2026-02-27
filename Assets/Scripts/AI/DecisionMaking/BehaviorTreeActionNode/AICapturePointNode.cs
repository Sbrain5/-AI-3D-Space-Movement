using System;

/// <summary>
/// Behavior tree node that monitors a specific capture point and triggers an event when it becomes fully captured.
/// </summary>
public sealed class AICapturePointNode : Node
{
    #region Variables

    private readonly AIEventsManager eventsManager;
    private readonly OctreeMoverAgent moverAgent;

    private CaptureZoneManager captureZoneManager;

    private int zoneId;
    private bool completionRaised;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new capture point node using shared blackboard references.
    /// </summary>
    /// <param name="bb">Shared blackboard instance.</param>
    public AICapturePointNode(Blackboard bb)
    {
        if (bb == null)
        {
            throw new ArgumentNullException(nameof(bb));
        }

        eventsManager = bb.GetAIEventsManager();
        moverAgent = bb.GetOctreeMoverAgent();

        captureZoneManager = ReferenceManager.GetManager<CaptureZoneManager>();
        zoneId = -1;
        completionRaised = false;
    }

    #endregion

    #region Behaviour Evaluation

    /// <summary>
    /// Stops movement and checks the configured zone state. If the zone is fully captured, it requests a switch back to idle roaming.
    /// </summary>
    public override void Evaluate()
    {
        StopMovement();

        if (eventsManager == null)
        {
            return;
        }

        if (zoneId < 0)
        {
            return;
        }

        if (!EnsureCaptureZoneManager())
        {
            return;
        }

        if (completionRaised)
        {
            return;
        }

        CurrentCaptureState currentState = captureZoneManager.GetCurrentCaptureState(zoneId);
        float currentProgress = captureZoneManager.GetCurrentCaptureProgress(zoneId);

        if (!IsZoneFullyCaptured(currentState, currentProgress))
        {
            return;
        }

        completionRaised = true;
        eventsManager.TriggerOnIdleRoaming();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Sets which capture zone this node monitors.
    /// </summary>
    /// <param name="id">Capture zone id.</param>
    public void SetCaptureZoneID(int id)
    {
        zoneId = id;
        completionRaised = false;
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Attempts to resolve the CaptureZoneManager reference if it is missing.
    /// </summary>
    /// <returns>True if the manager is available, otherwise false.</returns>
    private bool EnsureCaptureZoneManager()
    {
        if (captureZoneManager != null)
        {
            return true;
        }

        captureZoneManager = ReferenceManager.GetManager<CaptureZoneManager>();
        return captureZoneManager != null;
    }

    /// <summary>
    /// Stops the mover agent if it exists.
    /// </summary>
    private void StopMovement()
    {
        if (moverAgent == null)
        {
            return;
        }

        moverAgent.Stop();
    }

    /// <summary>
    /// Checks whether a zone is fully captured using both state and progress.
    /// </summary>
    /// <param name="state">Zone state.</param>
    /// <param name="progress">Zone progress.</param>
    /// <returns>True if fully captured, otherwise false.</returns>
    private bool IsZoneFullyCaptured(CurrentCaptureState state, float progress)
    {
        if (progress < 99.9f)
        {
            return false;
        }

        return state == CurrentCaptureState.CapturedTeamA || state == CurrentCaptureState.CapturedTeamB;
    }

    #endregion
}