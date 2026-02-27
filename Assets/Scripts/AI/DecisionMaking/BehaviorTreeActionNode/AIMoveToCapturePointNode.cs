using UnityEngine;

/// <summary>
/// Behaviour Tree action node that moves an AI ship toward a neutral capture zone and switches to the capture-hold state when the ship arrives.
/// </summary>
public sealed class AIMoveToCapturePointNode : Node
{
    #region Variables

    private AIEventsManager aiEventsManager;
    private OctreeMoverAgent moverAgent;
    private CaptureZoneManager captureZoneManager;

    private int targetZoneID;

    private float arrivalDistance;
    private float repathInterval;

    private bool hasActiveMoveCommand;
    private float nextRepathTime;
    private float nextRetryTime;

    private Vector3 currentTargetPosition;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes the move-to-capture node using values from the blackboard.
    /// </summary>
    /// <param name="blackboard">Shared blackboard instance.</param>
    public AIMoveToCapturePointNode(Blackboard blackboard)
    {
        aiEventsManager = blackboard.GetAIEventsManager();
        moverAgent = blackboard.GetOctreeMoverAgent();
        captureZoneManager = ReferenceManager.GetManager<CaptureZoneManager>();

        targetZoneID = -1;

        arrivalDistance = Mathf.Max(0.05f, blackboard.GetDistanceToTargetThreshold());
        repathInterval = Mathf.Max(0.05f, blackboard.GetScanInterval());

        hasActiveMoveCommand = false;
        nextRepathTime = 0f;
        nextRetryTime = 0f;

        currentTargetPosition = Vector3.zero;
    }

    #endregion

    #region Behaviour

    /// <summary>
    /// Moves the AI ship toward the selected neutral capture zone and triggers arrival when close enough.
    /// </summary>
    public override void Evaluate()
    {
        if (moverAgent == null)
        {
            return;
        }

        if (aiEventsManager == null)
        {
            return;
        }

        if (!TryEnsureCaptureZoneManager())
        {
            return;
        }

        if (!IsZoneIDValid(targetZoneID))
        {
            return;
        }

        CurrentCaptureState zoneState;
        Vector3 zonePosition;

        if (!TryReadZoneInfo(targetZoneID, out zoneState, out zonePosition))
        {
            return;
        }

        float zoneProgress = captureZoneManager.GetCurrentCaptureProgress(targetZoneID);

        if (IsZoneFullyCaptured(zoneState, zoneProgress))
        {
            StopMovementAndReset();
            aiEventsManager.TriggerOnIdleRoaming();
            return;
        }

        currentTargetPosition = zonePosition;

        if (!hasActiveMoveCommand)
        {
            TrySendInitialMoveCommand();
            return;
        }

        TryRepath();

        float distanceToZone = Vector3.Distance(moverAgent.GetServerPosition(), currentTargetPosition);
        if (distanceToZone <= arrivalDistance)
        {
            StopMovementAndReset();
            aiEventsManager.TriggerOnArrivedAtCapturePoint(targetZoneID);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the destination capture zone id and resets internal movement state.
    /// </summary>
    /// <param name="zoneID">Target capture zone id.</param>
    public void SetZoneID(int zoneID)
    {
        targetZoneID = zoneID;
        hasActiveMoveCommand = false;
        nextRepathTime = 0f;
        nextRetryTime = 0f;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Ensures the capture zone manager reference is available.
    /// </summary>
    /// <returns>True if the manager is available.</returns>
    private bool TryEnsureCaptureZoneManager()
    {
        if (captureZoneManager != null)
        {
            return true;
        }

        captureZoneManager = ReferenceManager.GetManager<CaptureZoneManager>();
        return captureZoneManager != null;
    }

    /// <summary>
    /// Checks whether a zone id is valid for the current manager.
    /// </summary>
    /// <param name="zoneID">Zone id to validate.</param>
    /// <returns>True if the zone id is valid.</returns>
    private bool IsZoneIDValid(int zoneID)
    {
        if (captureZoneManager == null)
        {
            return false;
        }

        int zoneCount = captureZoneManager.GetCaptureZoneCount();

        if (zoneCount <= 0)
        {
            return false;
        }

        if (zoneID < 0)
        {
            return false;
        }

        if (zoneID >= zoneCount)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to read both the zone state and world position from the manager.
    /// </summary>
    /// <param name="zoneID">Zone id.</param>
    /// <param name="zoneState">Zone state output.</param>
    /// <param name="zonePosition">Zone position output.</param>
    /// <returns>True if both reads succeeded.</returns>
    private bool TryReadZoneInfo(int zoneID, out CurrentCaptureState zoneState, out Vector3 zonePosition)
    {
        zoneState = CurrentCaptureState.Neutral;
        zonePosition = Vector3.zero;

        if (!captureZoneManager.TryGetCurrentCaptureState(zoneID, out zoneState))
        {
            return false;
        }

        if (!captureZoneManager.TryGetCaptureZonePosition(zoneID, out zonePosition))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sends the first destination command and retries later if it is rejected.
    /// </summary>
    private void TrySendInitialMoveCommand()
    {
        if (Time.time < nextRetryTime)
        {
            return;
        }

        bool commandAccepted = moverAgent.SetDestination(currentTargetPosition, true);
        if (!commandAccepted)
        {
            hasActiveMoveCommand = false;
            nextRetryTime = Time.time + 0.25f;
            return;
        }

        hasActiveMoveCommand = true;
        nextRepathTime = 0f;
    }

    /// <summary>
    /// Reissues the destination command on a fixed interval.
    /// </summary>
    private void TryRepath()
    {
        if (Time.time < nextRepathTime)
        {
            return;
        }

        nextRepathTime = Time.time + repathInterval;
        moverAgent.SetDestination(currentTargetPosition, false);
    }

    /// <summary>
    /// Stops movement and clears internal move command state.
    /// </summary>
    private void StopMovementAndReset()
    {
        moverAgent.Stop();
        hasActiveMoveCommand = false;
        nextRepathTime = 0f;
        nextRetryTime = 0f;
    }

    /// <summary>
    /// Returns true when the zone is fully captured using both state and progress.
    /// </summary>
    /// <param name="state">Zone state.</param>
    /// <param name="progress">Zone progress.</param>
    /// <returns>True when fully captured.</returns>
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