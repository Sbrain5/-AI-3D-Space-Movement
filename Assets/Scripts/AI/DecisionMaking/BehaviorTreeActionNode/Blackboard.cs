using UnityEngine;

/// <summary>
/// Behavior Tree blackboard holding shared AI references and tuning values.
/// </summary>
public sealed class Blackboard
{
    #region Variables

    private AIEventsManager aiEvents;
    private OctreeMoverAgent moverAgent;

    private Vector3 targetPosition;
    private float roamWeight;
    private float scanInterval;
    private float arrivalThreshold;
    private int teamId;

    #endregion

    #region Setters

    /// <summary>
    /// Sets the target world position used by movement/decision nodes.
    /// </summary>
    /// <param name="position">Target world position.</param>
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
    }

    /// <summary>
    /// Sets the AI event hub reference.
    /// </summary>
    /// <param name="eventsManager">AI event hub.</param>
    public void SetAIEventsManager(AIEventsManager eventsManager)
    {
        aiEvents = eventsManager;
    }

    /// <summary>
    /// Sets the mover agent reference.
    /// </summary>
    /// <param name="agent">Mover agent.</param>
    public void SetOctreeMoverAgent(OctreeMoverAgent agent)
    {
        moverAgent = agent;
    }

    /// <summary>
    /// Sets roam weight used by roaming logic.
    /// </summary>
    /// <param name="weight">Roam weight.</param>
    public void SetRoamWeight(float weight)
    {
        roamWeight = weight;
    }

    /// <summary>
    /// Sets the scan interval used by detection/awareness logic.
    /// </summary>
    /// <param name="seconds">Interval in seconds.</param>
    public void SetScanInterval(float seconds)
    {
        scanInterval = seconds;
    }

    /// <summary>
    /// Sets the arrival distance threshold used by movement nodes.
    /// </summary>
    /// <param name="distance">Distance threshold.</param>
    public void SetDistanceToTargetThreshold(float distance)
    {
        arrivalThreshold = distance;
    }

    /// <summary>
    /// Sets the AI team identifier.
    /// </summary>
    /// <param name="id">Team id.</param>
    public void SetTeamID(int id)
    {
        teamId = id;
    }

    #endregion

    #region Getters

    /// <summary>
    /// Gets the current target world position.
    /// </summary>
    /// <returns>Target world position.</returns>
    public Vector3 GetTargetPosition()
    {
        return targetPosition;
    }

    /// <summary>
    /// Gets the AI event hub reference.
    /// </summary>
    /// <returns>AI event hub.</returns>
    public AIEventsManager GetAIEventsManager()
    {
        return aiEvents;
    }

    /// <summary>
    /// Gets the mover agent reference.
    /// </summary>
    /// <returns>Mover agent.</returns>
    public OctreeMoverAgent GetOctreeMoverAgent()
    {
        return moverAgent;
    }

    /// <summary>
    /// Gets roam weight.
    /// </summary>
    /// <returns>Roam weight.</returns>
    public float GetRoamWeight()
    {
        return roamWeight;
    }

    /// <summary>
    /// Gets scan interval in seconds.
    /// </summary>
    /// <returns>Scan interval.</returns>
    public float GetScanInterval()
    {
        return scanInterval;
    }

    /// <summary>
    /// Gets the arrival distance threshold.
    /// </summary>
    /// <returns>Arrival threshold.</returns>
    public float GetDistanceToTargetThreshold()
    {
        return arrivalThreshold;
    }

    /// <summary>
    /// Gets the team identifier.
    /// </summary>
    /// <returns>Team id.</returns>
    public int GetTeamID()
    {
        return teamId;
    }

    #endregion
}