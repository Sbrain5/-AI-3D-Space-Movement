using System.Collections.Generic;
using PurrNet;
using UnityEngine;

/// <summary>
/// Server-side AI controller managing behaviour state and transitions for an AI ship, using a behaviour tree and event-driven architecture.
/// </summary>
public sealed class AIController : NetworkBehaviour
{
    #region Serialized References

    [Header("Base References")]
    [SerializeField] private AIShipHolder shipHolder;
    [SerializeField] private OctreeMoverAgent moverAgent;

    [Header("Tuning")]
    [SerializeField] private float roamWeight = 1f;
    [SerializeField] private float scanInterval = 0.25f;
    [SerializeField] private float distanceToTargetThreshold = 5f;

    [Header("Objective Selection")]
    [SerializeField] private float objectiveThinkInterval = 0.35f;

    [Header("Health Thresholds")]
    [SerializeField] private float lowHealthFraction = 0.4f;
    [SerializeField] private float healedHealthFraction = 0.95f;

    [Header("Debug")]
    [SerializeField] private bool logTransitions = true;
    [SerializeField] private bool logHeartbeat = false;
    [SerializeField] private float heartbeatInterval = 1f;

    #endregion

    #region Runtime State

    private AIEventsManager eventsManager;
    private Blackboard blackboard;

    private NodeSelector behaviourRoot;

    private Node idleNode;
    private Node moveToCaptureNode;
    private Node captureHoldNode;
    private Node combatNode;
    private Node retreatNode;

    private CaptureZoneManager captureZoneManager;

    private bool isInitialized;
    private bool isAlive;
    private bool healingLocked;

    private float maxHealth;
    private float currentHealth;

    private int currentObjectiveZoneId;
    private int savedObjectiveZoneId;

    private float nextObjectiveThinkTime;
    private float nextHeartbeatTime;
    private Node lastHeartbeatNode;

    #endregion

    #region Network Lifecycle

    protected override void OnSpawned()
    {
        if (!isServer)
        {
            return;
        }

        InitializeServerIfNeeded();
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!isServer)
        {
            return;
        }

        InitializeServerIfNeeded();
    }

    private void OnDisable()
    {
        if (!isServer)
        {
            return;
        }

        ShutdownServerIfNeeded();
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (!isFullySpawned)
        {
            return;
        }

        if (!isInitialized)
        {
            InitializeServerIfNeeded();
            if (!isInitialized)
            {
                return;
            }
        }

        if (!isAlive)
        {
            return;
        }

        UpdateHealingLock();
        TrySelectCaptureObjectiveWhenIdle();

        if (behaviourRoot != null)
        {
            behaviourRoot.Evaluate();
        }

        HeartbeatLog();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes all server-side references, builds the behaviour tree and subscribes events.
    /// </summary>
    private void InitializeServerIfNeeded()
    {
        if (isInitialized)
        {
            return;
        }

        ResolveReferences();

        if (shipHolder == null)
        {
            return;
        }

        eventsManager = shipHolder.GetOrCreateAIEventsManager();
        if (eventsManager == null)
        {
            return;
        }

        if (moverAgent == null)
        {
            return;
        }

        maxHealth = shipHolder.GetAIMaxHealth();
        currentHealth = maxHealth;

        isAlive = true;
        healingLocked = false;

        captureZoneManager = null;
        nextObjectiveThinkTime = 0f;

        currentObjectiveZoneId = -1;
        savedObjectiveZoneId = -1;

        nextHeartbeatTime = 0f;
        lastHeartbeatNode = null;

        blackboard = BuildBlackboard();
        behaviourRoot = BuildBehaviourTree(blackboard);

        SubscribeEvents();

        isInitialized = true;

        SwitchToIdleRoam();
    }

    /// <summary>
    /// Unsubscribes server-side events.
    /// </summary>
    private void ShutdownServerIfNeeded()
    {
        if (!isInitialized)
        {
            return;
        }

        if (eventsManager != null)
        {
            UnsubscribeEvents();
        }

        isInitialized = false;
    }

    /// <summary>
    /// Resolves component references from the hierarchy when not set in the inspector.
    /// </summary>
    private void ResolveReferences()
    {
        if (shipHolder == null)
        {
            shipHolder = GetComponentInParent<AIShipHolder>();
        }

        if (moverAgent == null)
        {
            moverAgent = GetComponent<OctreeMoverAgent>();
        }

    }

    /// <summary>
    /// Builds the blackboard used by nodes.
    /// </summary>
    /// <returns>Initialized blackboard.</returns>
    private Blackboard BuildBlackboard()
    {
        Blackboard bb = new Blackboard();

        bb.SetAIEventsManager(eventsManager);
        bb.SetOctreeMoverAgent(moverAgent);

        bb.SetScanInterval(scanInterval);
        bb.SetRoamWeight(roamWeight);
        bb.SetDistanceToTargetThreshold(distanceToTargetThreshold);
        bb.SetTargetPosition(Vector3.zero);

        return bb;
    }

    /// <summary>
    /// Builds the behaviour tree node set and returns the selector root.
    /// </summary>
    /// <param name="bb">Shared blackboard.</param>
    /// <returns>Root selector.</returns>
    private NodeSelector BuildBehaviourTree(Blackboard bb)
    {
        idleNode = new AIIdleRoamNode(bb);
        moveToCaptureNode = new AIMoveToCapturePointNode(bb);
        captureHoldNode = new AICapturePointNode(bb);

        List<Node> nodes = new List<Node>
        {
            retreatNode,
            combatNode,
            captureHoldNode,
            moveToCaptureNode,
            idleNode
        };

        NodeSelector selector = new NodeSelector(nodes);
        return selector;
    }

    #endregion

    #region Event Wiring

    /// <summary>
    /// Subscribes event handlers to the AI event hub.
    /// </summary>
    private void SubscribeEvents()
    {
        eventsManager.SubscribeOnIdleRoaming(SwitchToIdleRoam);
        eventsManager.SubscribeOnTargetDetected(SwitchToCombat);
        eventsManager.SubscribeOnTargetLost(HandleTargetLost);
        eventsManager.SubscribeOnAIDeathRespawnTriggered(SetAliveState);

        eventsManager.SubscribeOnCapturePointAvailableForCapturing(SwitchToMoveToCapture);
        eventsManager.SubscribeOnArrivedAtCapturePoint(SwitchToCaptureHold);

        eventsManager.SubscribeOnAIHealthValueChanged(HandleHealthChanged);
    }

    /// <summary>
    /// Unsubscribes event handlers from the AI event hub.
    /// </summary>
    private void UnsubscribeEvents()
    {
        eventsManager.UnSubscribeOnIdleRoaming(SwitchToIdleRoam);
        eventsManager.UnSubscribeOnTargetDetected(SwitchToCombat);
        eventsManager.UnSubscribeOnTargetLost(HandleTargetLost);
        eventsManager.UnSubscribeOnAIDeathRespawnTriggered(SetAliveState);

        eventsManager.UnSubscribeOnCapturePointAvailableForCapturing(SwitchToMoveToCapture);
        eventsManager.UnSubscribeOnArrivedAtCapturePoint(SwitchToCaptureHold);

        eventsManager.UnSubscribeOnAIHealthValueChanged(HandleHealthChanged);
    }

    #endregion

    #region Objective Selection

    /// <summary>
    /// Attempts to pick a capture objective while idling, based on a think interval.
    /// </summary>
    private void TrySelectCaptureObjectiveWhenIdle()
    {
        if (healingLocked)
        {
            return;
        }

        if (behaviourRoot == null)
        {
            return;
        }

        if (behaviourRoot.GetActiveNode() != idleNode)
        {
            return;
        }

        if (Time.time < nextObjectiveThinkTime)
        {
            return;
        }

        nextObjectiveThinkTime = Time.time + Mathf.Max(0.05f, objectiveThinkInterval);

        int bestZoneId = SelectBestCaptureZoneId();
        if (bestZoneId < 0)
        {
            return;
        }

        eventsManager.TriggerOnCapturePointAvailableForCapturing(bestZoneId);
    }

    /// <summary>
    /// Selects the nearest registered neutral capture zone.
    /// </summary>
    /// <returns>Zone id or -1 if none is available.</returns>
    private int SelectBestCaptureZoneId()
    {
        if (!EnsureCaptureZoneManager())
        {
            return -1;
        }

        int zoneCount = captureZoneManager.GetCaptureZoneCount();
        if (zoneCount <= 0)
        {
            return -1;
        }

        Vector3 selfPosition = GetSelfPosition();

        int bestZoneId = -1;
        float bestDistance = float.PositiveInfinity;

        int zoneId = 0;
        while (zoneId < zoneCount)
        {
            CurrentCaptureState zoneState;
            if (!captureZoneManager.TryGetCurrentCaptureState(zoneId, out zoneState))
            {
                zoneId++;
                continue;
            }

            if (zoneState != CurrentCaptureState.Neutral)
            {
                zoneId++;
                continue;
            }

            Vector3 zonePosition;
            if (!captureZoneManager.TryGetCaptureZonePosition(zoneId, out zonePosition))
            {
                zoneId++;
                continue;
            }

            float distance = Vector3.Distance(selfPosition, zonePosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestZoneId = zoneId;
            }

            zoneId++;
        }

        return bestZoneId;
    }

    /// <summary>
    /// Ensures the CaptureZoneManager reference is available.
    /// </summary>
    /// <returns>True if available.</returns>
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
    /// Gets this AI position, using the mover when available.
    /// </summary>
    /// <returns>World position.</returns>
    private Vector3 GetSelfPosition()
    {
        if (moverAgent != null)
        {
            return moverAgent.GetServerPosition();
        }

        return transform.position;
    }

    #endregion

    #region Health Handling

    /// <summary>
    /// Updates healing lock state based on current health.
    /// </summary>
    private void UpdateHealingLock()
    {
        if (!healingLocked)
        {
            return;
        }

        float healedThreshold = maxHealth * Mathf.Clamp01(healedHealthFraction);
        if (currentHealth >= healedThreshold)
        {
            EndHealingLockAndResume();
        }
    }

    /// <summary>
    /// Receives health updates from external systems.
    /// </summary>
    /// <param name="health">New health value.</param>
    private void HandleHealthChanged(float health)
    {
        currentHealth = health;

        float lowThreshold = maxHealth * Mathf.Clamp01(lowHealthFraction);
        float healedThreshold = maxHealth * Mathf.Clamp01(healedHealthFraction);

        if (!healingLocked && currentHealth <= lowThreshold)
        {
            BeginHealingLock();
            return;
        }

        if (healingLocked && currentHealth >= healedThreshold)
        {
            EndHealingLockAndResume();
        }
    }

    /// <summary>
    /// Enters healing lock state and forces retreat behaviour.
    /// </summary>
    private void BeginHealingLock()
    {
        healingLocked = true;

        SaveObjectiveForResume();

        if (moverAgent != null)
        {
            moverAgent.Stop();
        }

        eventsManager.TriggerOnAIAttemptingFire(false, null, null);
        eventsManager.TriggerOnRequestToStopTicking();

        SetActiveNode(retreatNode, "LowHealth");
    }

    /// <summary>
    /// Exits healing lock state and resumes non-combat behaviour.
    /// </summary>
    private void EndHealingLockAndResume()
    {
        healingLocked = false;
        ResumeNonCombatBehaviour();
    }

    #endregion

    #region Node Switching

    /// <summary>
    /// Switches the AI to idle roaming.
    /// </summary>
    private void SwitchToIdleRoam()
    {
        if (healingLocked)
        {
            return;
        }

        AIIdleRoamNode roam = idleNode as AIIdleRoamNode;
        if (roam != null)
        {
            roam.ResetState();
        }

        currentObjectiveZoneId = -1;

        SetActiveNode(idleNode, "Idle");
    }

    /// <summary>
    /// Switches the AI to move-to-capture behaviour.
    /// </summary>
    /// <param name="zoneId">Capture zone id.</param>
    private void SwitchToMoveToCapture(int zoneId)
    {
        if (healingLocked)
        {
            return;
        }

        if (behaviourRoot != null && behaviourRoot.GetActiveNode() == combatNode)
        {
            return;
        }

        if (moverAgent != null)
        {
            moverAgent.Stop();
        }

        AIMoveToCapturePointNode moveNode = moveToCaptureNode as AIMoveToCapturePointNode;
        if (moveNode != null)
        {
            moveNode.SetZoneID(zoneId);
        }

        currentObjectiveZoneId = zoneId;
        SetActiveNode(moveToCaptureNode, "CaptureTarget");
    }

    /// <summary>
    /// Switches the AI to capture-hold behaviour.
    /// </summary>
    /// <param name="zoneId">Capture zone id.</param>
    private void SwitchToCaptureHold(int zoneId)
    {
        if (healingLocked)
        {
            return;
        }

        if (behaviourRoot != null && behaviourRoot.GetActiveNode() == combatNode)
        {
            return;
        }

        if (moverAgent != null)
        {
            moverAgent.Stop();
        }

        AICapturePointNode captureNode = captureHoldNode as AICapturePointNode;
        if (captureNode != null)
        {
            captureNode.SetCaptureZoneID(zoneId);
        }

        currentObjectiveZoneId = zoneId;
        SetActiveNode(captureHoldNode, "ArrivedAtZone");
    }

    /// <summary>
    /// Switches the AI to combat behaviour.
    /// </summary>
    /// <param name="targetTransform">Target transform.</param>
    /// <param name="rb">Target rigidbody.</param>
    private void SwitchToCombat(Transform targetTransform, Rigidbody rb)
    {
        if (healingLocked)
        {
            return;
        }

        if (behaviourRoot == null || behaviourRoot.GetActiveNode() != combatNode)
        {
            SaveObjectiveForResume();
        }

        if (moverAgent != null)
        {
            moverAgent.Stop();
        }

        SetActiveNode(combatNode, "TargetDetected");
    }

    /// <summary>
    /// Handles target lost event.
    /// </summary>
    private void HandleTargetLost()
    {
        if (healingLocked)
        {
            return;
        }

        if (behaviourRoot != null && behaviourRoot.GetActiveNode() == combatNode)
        {
            ResumeNonCombatBehaviour();
        }
    }

    /// <summary>
    /// Switches the currently evaluated node.
    /// </summary>
    /// <param name="node">Node to activate.</param>
    /// <param name="reason">Transition reason.</param>
    private void SetActiveNode(Node node, string reason)
    {
        if (behaviourRoot == null)
        {
            return;
        }

        if (node == null)
        {
            return;
        }

        Node previous = behaviourRoot.GetActiveNode();
        behaviourRoot.SwitchActiveNode(node);

        if (logTransitions && previous != node)
        {
            Debug.Log(BuildLogLine("NodeSwitch", reason), this);
        }
    }

    #endregion

    #region Resume Logic

    /// <summary>
    /// Saves the current objective for later resumption.
    /// </summary>
    private void SaveObjectiveForResume()
    {
        if (currentObjectiveZoneId < 0)
        {
            return;
        }

        savedObjectiveZoneId = currentObjectiveZoneId;
    }

    /// <summary>
    /// Attempts to resume a saved objective or picks a new one.
    /// </summary>
    private void ResumeNonCombatBehaviour()
    {
        int preferredZone = savedObjectiveZoneId;
        savedObjectiveZoneId = -1;

        if (preferredZone >= 0)
        {
            eventsManager.TriggerOnCapturePointAvailableForCapturing(preferredZone);
            return;
        }

        SwitchToIdleRoam();
    }

    #endregion

    #region Alive State

    /// <summary>
    /// Sets alive state for server tick gating.
    /// </summary>
    /// <param name="alive">Alive state.</param>
    private void SetAliveState(bool alive)
    {
        isAlive = alive;

        if (logTransitions)
        {
            Debug.Log(BuildLogLine("AliveState", alive ? "Alive" : "Dead"), this);
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// Emits heartbeat logs at a fixed interval.
    /// </summary>
    private void HeartbeatLog()
    {
        if (!logHeartbeat)
        {
            return;
        }

        if (Time.time < nextHeartbeatTime)
        {
            return;
        }

        nextHeartbeatTime = Time.time + Mathf.Max(0.1f, heartbeatInterval);

        Node active = behaviourRoot != null ? behaviourRoot.GetActiveNode() : null;
        if (active != lastHeartbeatNode)
        {
            lastHeartbeatNode = active;
            Debug.Log(BuildLogLine("Heartbeat", "Changed"), this);
            return;
        }

        Debug.Log(BuildLogLine("Heartbeat", "Tick"), this);
    }

    /// <summary>
    /// Builds a compact debug line describing the current AI state.
    /// </summary>
    /// <param name="tag">Log tag.</param>
    /// <param name="details">Log details.</param>
    /// <returns>Formatted log line.</returns>
    private string BuildLogLine(string tag, string details)
    {
        string nodeName = GetNodeName(behaviourRoot != null ? behaviourRoot.GetActiveNode() : null);

        return "[AIController][" + name + "] " + tag + " " + details + " hp=" + currentHealth.ToString("0.0") + "/" + maxHealth.ToString("0.0") +
               " healing=" + healingLocked + " node=" + nodeName + " obj=" + currentObjectiveZoneId + " saved=" + savedObjectiveZoneId;
    }

    /// <summary>
    /// Gets a readable node name.
    /// </summary>
    /// <param name="node">Node instance.</param>
    /// <returns>Node name or null.</returns>
    private string GetNodeName(Node node)
    {
        if (node == null)
        {
            return "<null>";
        }

        return node.GetType().Name;
    }

    #endregion
}