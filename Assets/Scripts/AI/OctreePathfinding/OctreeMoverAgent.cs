using OctreePathfinding;
using PurrNet;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mover agent that uses octree pathfinding for navigation and synchronizes pose to observers.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class OctreeMoverAgent : NetworkBehaviour
{
    #region Serialized Fields

    [Header("Movement")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float accuracy = 6f;

    [Header("Repath")]
    [SerializeField] private float repathCooldown = 0.25f;

    [Header("Movement Safety")]
    [SerializeField] private bool enableMovementSafetyCheck = true;
    [SerializeField] private float movementSafetyRadius = 2f;
    [SerializeField] private float movementSafetyPadding = 0.2f;
    [SerializeField] private float blockedStepRepathCooldown = 0.15f;

    [Header("References")]
    [SerializeField] private Rigidbody shipRigidbody;

    [Header("Networking (Observers Smoothing)")]
    [SerializeField] private float sendInterval = 0.05f;
    [SerializeField] private float observerPositionLerp = 18f;
    [SerializeField] private float observerRotationLerp = 18f;

    [Header("Debug")]
    [SerializeField] private bool debugMoverLogs = false;
    [SerializeField] private bool forceDebugMoverLogsOnServer = true;

    #endregion

    #region Private Fields

    private readonly List<Vector3> waypoints = new List<Vector3>(128);
    private int currentWaypointIndex;

    private float nextRepathTime;
    private float nextSendTime;
    private float nextBlockedStepRepathTime;

    private Vector3 observerTargetPosition;
    private Quaternion observerTargetRotation;
    private bool hasObserverTarget;

    private bool hasActiveCommand;
    private Vector3 commandedDestination;

    private readonly List<Vector3> debugWaypointSnapshot = new List<Vector3>(128);
    private int debugSnapshotIndex;
    private bool hasDebugSnapshot;

    private int teamId;
    private int ownerKey;

    #endregion

    #region Network Lifecycle

    protected override void OnSpawned(bool asServer)
    {
        if (shipRigidbody == null)
        {
            shipRigidbody = GetComponent<Rigidbody>();
        }

        if (asServer && forceDebugMoverLogsOnServer)
        {
            debugMoverLogs = true;
        }

        if (!asServer)
        {
            observerTargetPosition = transform.position;
            observerTargetRotation = transform.rotation;
            hasObserverTarget = true;
            return;
        }

        ownerKey = GetInstanceID();

        waypoints.Clear();
        currentWaypointIndex = 0;
        hasActiveCommand = false;

        nextSendTime = Time.time;
        nextRepathTime = 0f;
        nextBlockedStepRepathTime = 0f;

        WriteMoverLog("Spawned on server.");
    }

    #endregion

    #region Unity Methods

    private void Update()
    {
        if (isServer)
        {
            SendPoseIfDue();
            return;
        }

        SmoothOnObserver(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!isServer)
        {
            return;
        }

        if (shipRigidbody == null)
        {
            return;
        }

        if (!hasActiveCommand)
        {
            return;
        }

        if (waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count)
        {
            hasActiveCommand = false;
            return;
        }

        float fixedDeltaTime = Time.fixedDeltaTime;
        float reachDistance = Mathf.Max(0.05f, accuracy);

        int consumeSafetyCounter = 0;
        int maxWaypointsPerTick = 64;

        while (currentWaypointIndex < waypoints.Count && consumeSafetyCounter < maxWaypointsPerTick)
        {
            Vector3 currentWaypoint = waypoints[currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(shipRigidbody.position, currentWaypoint);

            if (distanceToWaypoint > reachDistance)
            {
                break;
            }

            currentWaypointIndex++;
            consumeSafetyCounter++;
        }

        if (currentWaypointIndex >= waypoints.Count)
        {
            hasActiveCommand = false;
            debugSnapshotIndex = currentWaypointIndex;
            WriteMoverLog("Arrived at destination.");
            return;
        }

        Vector3 nextWaypoint = waypoints[currentWaypointIndex];

        if (enableMovementSafetyCheck && !IsNextMovementStepSafe(nextWaypoint, fixedDeltaTime))
        {
            WriteMoverLog("Blocked step detected. Attempting repath.");
            HandleUnsafeMovementStep();
            return;
        }

        MoveTowards(nextWaypoint, fixedDeltaTime);
        debugSnapshotIndex = currentWaypointIndex;
    }

    #endregion

    #region Public Commands

    /// <summary>
    /// Commands movement to a specified world position.
    /// </summary>
    /// <param name="destination">World destination position.</param>
    /// <param name="force">If true, ignores repath cooldown.</param>
    /// <returns>True if accepted; otherwise false.</returns>
    public bool SetDestination(Vector3 destination, bool force)
    {
        if (!isServer)
        {
            return false;
        }

        if (!force && Time.time < nextRepathTime)
        {
            return false;
        }

        commandedDestination = destination;

        bool requestSucceeded = TryRequestPathToDestination(destination);
        if (!requestSucceeded)
        {
            waypoints.Clear();
            currentWaypointIndex = 0;
            hasActiveCommand = false;
            WriteMoverLog("SetDestination failed: no path.");
            return false;
        }

        hasActiveCommand = true;
        nextRepathTime = Time.time + Mathf.Max(0.01f, repathCooldown);

        RefreshDebugSnapshot();
        WriteMoverLog("SetDestination accepted.");
        return true;
    }

    /// <summary>
    /// Commands movement to a random navigable position.
    /// </summary>
    /// <param name="force">If true, ignores repath cooldown.</param>
    /// <returns>True if accepted; otherwise false.</returns>
    public bool SetRandomDestination(bool force)
    {
        if (!isServer)
        {
            return false;
        }

        if (!force && Time.time < nextRepathTime)
        {
            return false;
        }

        OctreePathfindingManager pathManager = OctreePathfindingManager.Instance;
        if (pathManager == null || !pathManager.IsBuilt)
        {
            return false;
        }

        Vector3 randomDestination;
        bool foundDestination = pathManager.TryGetRandomNavigablePosition(out randomDestination);
        if (!foundDestination)
        {
            return false;
        }

        return SetDestination(randomDestination, true);
    }

    /// <summary>
    /// Stops current movement and clears path.
    /// </summary>
    public void Stop()
    {
        if (!isServer)
        {
            return;
        }

        waypoints.Clear();
        currentWaypointIndex = 0;
        hasActiveCommand = false;

        debugWaypointSnapshot.Clear();
        debugSnapshotIndex = 0;
        hasDebugSnapshot = false;

        if (shipRigidbody != null && !shipRigidbody.isKinematic)
        {
            shipRigidbody.linearVelocity = Vector3.zero;
            shipRigidbody.angularVelocity = Vector3.zero;
        }

        nextRepathTime = 0f;
        nextBlockedStepRepathTime = 0f;

        WriteMoverLog("Stopped.");
    }

    /// <summary>
    /// Gets the current server position of the mover.
    /// </summary>
    /// <returns>Server position.</returns>
    public Vector3 GetServerPosition()
    {
        if (isServer && shipRigidbody != null)
        {
            return shipRigidbody.position;
        }

        return transform.position;
    }

    /// <summary>
    /// Returns whether there is an active movement command.
    /// </summary>
    /// <returns>True if a command is active; otherwise false.</returns>
    public bool HasActiveCommand()
    {
        return hasActiveCommand;
    }

    /// <summary>
    /// Sets the traffic team id used by the pathfinding manager.
    /// </summary>
    /// <param name="newTeamId">Team identifier.</param>
    [ServerOnly]
    public void SetTeamId(int newTeamId)
    {
        teamId = newTeamId;
    }

    #endregion

    #region Movement

    /// <summary>
    /// Moves the rigidbody toward the specified target position.
    /// </summary>
    /// <param name="target">World target position.</param>
    /// <param name="deltaTime">Fixed delta time.</param>
    private void MoveTowards(Vector3 target, float deltaTime)
    {
        Vector3 currentPosition = shipRigidbody.position;
        Vector3 targetOffset = target - currentPosition;

        if (targetOffset.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 moveDirection = targetOffset.normalized;
        Vector3 upReference = Vector3.up;

        float upDot = Vector3.Dot(moveDirection, upReference);
        if (upDot > 0.999f || upDot < -0.999f)
        {
            upReference = Vector3.forward;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(moveDirection, upReference);

        float turnAmount = Mathf.Clamp01(Mathf.Max(0f, turnSpeed) * deltaTime);
        Quaternion newRotation = Quaternion.Slerp(shipRigidbody.rotation, desiredRotation, turnAmount);

        float maxStepDistance = Mathf.Max(0f, speed) * deltaTime;
        float remainingDistance = targetOffset.magnitude;

        if (maxStepDistance > remainingDistance)
        {
            maxStepDistance = remainingDistance;
        }

        Vector3 newPosition = currentPosition + (moveDirection * maxStepDistance);

        shipRigidbody.MoveRotation(newRotation);
        shipRigidbody.MovePosition(newPosition);
    }

    /// <summary>
    /// Checks whether the next planned movement step is safe against obstacle colliders.
    /// </summary>
    /// <param name="currentWaypoint">Current waypoint target.</param>
    /// <param name="deltaTime">Fixed delta time.</param>
    /// <returns>True if the next step is safe; otherwise false.</returns>
    private bool IsNextMovementStepSafe(Vector3 currentWaypoint, float deltaTime)
    {
        OctreePathfindingManager pathManager = OctreePathfindingManager.Instance;
        if (pathManager == null || !pathManager.IsBuilt)
        {
            return true;
        }

        Vector3 startPosition = shipRigidbody.position;
        Vector3 endPosition = GetPlannedStepEndPosition(currentWaypoint, deltaTime);

        float safetyRadius = Mathf.Max(0.05f, movementSafetyRadius + movementSafetyPadding);

        return pathManager.IsMovementStepSafe(startPosition, endPosition, safetyRadius, shipRigidbody);
    }

    /// <summary>
    /// Computes the next step end position without moving the rigidbody.
    /// </summary>
    /// <param name="target">Current waypoint target.</param>
    /// <param name="deltaTime">Fixed delta time.</param>
    /// <returns>Predicted end position for the next movement step.</returns>
    private Vector3 GetPlannedStepEndPosition(Vector3 target, float deltaTime)
    {
        Vector3 currentPosition = shipRigidbody.position;
        Vector3 targetOffset = target - currentPosition;

        if (targetOffset.sqrMagnitude <= 0.0001f)
        {
            return currentPosition;
        }

        Vector3 moveDirection = targetOffset.normalized;

        float maxStepDistance = Mathf.Max(0f, speed) * deltaTime;
        float remainingDistance = targetOffset.magnitude;

        if (maxStepDistance > remainingDistance)
        {
            maxStepDistance = remainingDistance;
        }

        return currentPosition + (moveDirection * maxStepDistance);
    }

    /// <summary>
    /// Handles a blocked movement step by attempting a fast repath and stopping if repath fails.
    /// </summary>
    private void HandleUnsafeMovementStep()
    {
        if (!hasActiveCommand)
        {
            return;
        }

        if (Time.time < nextBlockedStepRepathTime)
        {
            return;
        }

        nextBlockedStepRepathTime = Time.time + Mathf.Max(0.01f, blockedStepRepathCooldown);

        bool repathSucceeded = TryRequestPathToDestination(commandedDestination);
        if (!repathSucceeded)
        {
            WriteMoverLog("Repath failed after blocked step.");
            Stop();
            return;
        }

        hasActiveCommand = true;
        nextRepathTime = Time.time + Mathf.Max(0.01f, repathCooldown);
        RefreshDebugSnapshot();
        WriteMoverLog("Repath succeeded after blocked step.");
    }

    #endregion

    #region Pathing

    /// <summary>
    /// Requests a path to the specified destination from the OctreePathfindingManager.
    /// </summary>
    /// <param name="destination">World destination position.</param>
    /// <returns>True if a path is found; otherwise false.</returns>
    private bool TryRequestPathToDestination(Vector3 destination)
    {
        OctreePathfindingManager pathManager = OctreePathfindingManager.Instance;
        if (pathManager == null || !pathManager.IsBuilt)
        {
            return false;
        }

        Vector3 rawStartPosition = shipRigidbody != null ? shipRigidbody.position : transform.position;

        Vector3 projectedStartPosition;
        Vector3 projectedDestinationPosition;

        if (!pathManager.TryProjectToNavigable(rawStartPosition, out projectedStartPosition))
        {
            return false;
        }

        if (!pathManager.TryProjectToNavigable(destination, out projectedDestinationPosition))
        {
            return false;
        }

        List<Vector3> newWaypointList;
        bool pathFound = pathManager.TryGetPathForAgent(projectedStartPosition, projectedDestinationPosition, teamId, ownerKey, out newWaypointList);

        if (!pathFound || newWaypointList == null || newWaypointList.Count < 2)
        {
            return false;
        }

        waypoints.Clear();

        int waypointIndex = 0;
        while (waypointIndex < newWaypointList.Count)
        {
            waypoints.Add(newWaypointList[waypointIndex]);
            waypointIndex++;
        }

        currentWaypointIndex = 0;
        return true;
    }

    #endregion

    #region Networking

    /// <summary>
    /// Sends the current server pose to observers if the send interval has elapsed.
    /// </summary>
    private void SendPoseIfDue()
    {
        if (Time.time < nextSendTime)
        {
            return;
        }

        nextSendTime = Time.time + Mathf.Max(0.01f, sendInterval);

        Vector3 serverPosition = shipRigidbody != null ? shipRigidbody.position : transform.position;
        Quaternion serverRotation = shipRigidbody != null ? shipRigidbody.rotation : transform.rotation;

        SendPoseToObservers(serverPosition, serverRotation);
    }

    /// <summary>
    /// Sends the specified pose to all observers.
    /// </summary>
    /// <param name="position">Position to send.</param>
    /// <param name="rotation">Rotation to send.</param>
    [ObserversRpc]
    private void SendPoseToObservers(Vector3 position, Quaternion rotation)
    {
        if (isServer)
        {
            return;
        }

        observerTargetPosition = position;
        observerTargetRotation = rotation;
        hasObserverTarget = true;
    }

    /// <summary>
    /// Smoothly interpolates the mover transform toward the observer target pose.
    /// </summary>
    /// <param name="deltaTime">Frame delta time.</param>
    private void SmoothOnObserver(float deltaTime)
    {
        if (!hasObserverTarget)
        {
            return;
        }

        float positionLerpSpeed = Mathf.Max(0f, observerPositionLerp);
        float rotationLerpSpeed = Mathf.Max(0f, observerRotationLerp);

        float positionLerpFactor = 1f - Mathf.Exp(-positionLerpSpeed * deltaTime);
        float rotationLerpFactor = 1f - Mathf.Exp(-rotationLerpSpeed * deltaTime);

        transform.position = Vector3.Lerp(transform.position, observerTargetPosition, positionLerpFactor);
        transform.rotation = Quaternion.Slerp(transform.rotation, observerTargetRotation, rotationLerpFactor);
    }

    #endregion

    #region Debug Path Access

    /// <summary>
    /// Refreshes the debug waypoint snapshot from the current path.
    /// </summary>
    private void RefreshDebugSnapshot()
    {
        debugWaypointSnapshot.Clear();

        int waypointIndex = 0;
        while (waypointIndex < waypoints.Count)
        {
            debugWaypointSnapshot.Add(waypoints[waypointIndex]);
            waypointIndex++;
        }

        debugSnapshotIndex = currentWaypointIndex;
        hasDebugSnapshot = debugWaypointSnapshot.Count > 0;
    }

    /// <summary>
    /// Attempts to get a copy of the current debug waypoints and index.
    /// </summary>
    /// <param name="outWaypoints">Output waypoint copy.</param>
    /// <param name="outCurrentIndex">Output current waypoint index.</param>
    /// <returns>True if debug data is available; otherwise false.</returns>
    public bool TryGetDebugWaypoints(out List<Vector3> outWaypoints, out int outCurrentIndex)
    {
        outWaypoints = null;
        outCurrentIndex = 0;

        if (!isServer)
        {
            return false;
        }

        if (!hasDebugSnapshot)
        {
            RefreshDebugSnapshot();
        }

        if (!hasDebugSnapshot)
        {
            return false;
        }

        List<Vector3> waypointCopy = new List<Vector3>(debugWaypointSnapshot.Count);

        int waypointIndex = 0;
        while (waypointIndex < debugWaypointSnapshot.Count)
        {
            waypointCopy.Add(debugWaypointSnapshot[waypointIndex]);
            waypointIndex++;
        }

        outWaypoints = waypointCopy;
        outCurrentIndex = debugSnapshotIndex;
        return true;
    }

    #endregion

    #region Debug Logging

    /// <summary>
    /// Writes a mover debug log when debug output is enabled.
    /// </summary>
    /// <param name="message">Message to log.</param>
    private void WriteMoverLog(string message)
    {
        if (!debugMoverLogs)
        {
            return;
        }

        Debug.Log("[OctreeMoverAgent][" + name + "] " + message, this);
    }

    #endregion
}