using System.Collections.Generic;
using PurrNet;
using UnityEngine;

/// <summary>
/// Controls capture logic and visuals for a single capture zone. Tracks AI ships inside the zone and updates progress accordingly.
/// </summary>
public sealed class CaptureZoneController : NetworkBehaviour
{
    #region Enums

    public enum CaptureZoneID
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3,
        E = 4
    }

    private enum ZoneVisualMode
    {
        SolidNeutral = 0,
        PulsingCapture = 1,
        SolidCaptured = 2,
        PulsingRelease = 3
    }

    #endregion

    #region Serialized Fields

    [Header("Zone Setup")]
    [SerializeField] private CaptureZoneID captureZoneID;
    [SerializeField] private Renderer mainRenderer;
    [SerializeField] private Renderer glowRenderer;

    [Header("Capture Tuning")]
    [SerializeField] private float captureTickInterval = 0.2f;
    [SerializeField] private float captureProgressPerTick = 4f;
    [SerializeField] private float autoReleaseDelay = 3f;
    [SerializeField] private float releaseTickInterval = 1.2f;
    [SerializeField] private float releaseProgressPerTick = 0.5f;

    [Header("Visual Materials")]
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField] private Material neutralZoneMaterial;
    [SerializeField] private Material capturedZoneMaterial;
    [SerializeField] private Material regressedZoneMaterial;

    [Header("Runtime Debug")]
    [SerializeField] private CurrentCaptureState captureState = CurrentCaptureState.Neutral;
    [SerializeField] private float captureProgress = 0f;
    [SerializeField] private int aiShipsInsideCount = 0;
    [SerializeField] private bool isAutoReleasing = false;

    #endregion

    #region Runtime Fields

    private CaptureZoneManager captureZoneManager;
    private readonly HashSet<int> aiShipKeysInside = new HashSet<int>();

    private float nextCaptureTickTime = 0f;
    private float autoReleaseStartTime = -1f;

    private ZoneVisualMode currentVisualMode = ZoneVisualMode.SolidNeutral;
    private bool pulseCurrentlyUsingSecondaryMaterial = false;

    #endregion

    #region Network Lifecycle

    protected override void OnSpawned(bool asServer)
    {
        ResetZoneRuntimeState();

        if (asServer)
        {
            ResolveCaptureZoneManager();
            RegisterZonePosition();
            PushZoneStatsToManager();
            ApplySolidNeutralVisualsLocal();
            SetVisualModeServer(ZoneVisualMode.SolidNeutral);
            return;
        }

        ApplySolidNeutralVisualsLocal();
    }

    #endregion

    #region Unity Methods

    private void Update()
    {
        UpdateVisualsLocal();

        if (!isServer)
        {
            return;
        }

        TickServerCaptureLogic();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer)
        {
            return;
        }

        int shipKey;
        if (!TryGetAIShipKey(other, out shipKey))
        {
            return;
        }

        if (!aiShipKeysInside.Contains(shipKey))
        {
            aiShipKeysInside.Add(shipKey);
            aiShipsInsideCount = aiShipKeysInside.Count;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isServer)
        {
            return;
        }

        int shipKey;
        if (!TryGetAIShipKey(other, out shipKey))
        {
            return;
        }

        if (aiShipKeysInside.Contains(shipKey))
        {
            aiShipKeysInside.Remove(shipKey);
            aiShipsInsideCount = aiShipKeysInside.Count;
        }
    }

    #endregion

    #region Server Logic

    /// <summary>
    /// Runs the capture/release loop on a fixed tick interval.
    /// </summary>
    private void TickServerCaptureLogic()
    {
        if (Time.time < nextCaptureTickTime)
        {
            return;
        }

        nextCaptureTickTime = Time.time + GetCurrentServerTickInterval();

        if (isAutoReleasing)
        {
            TickAutoRelease();
            return;
        }

        if (captureState == CurrentCaptureState.CapturedTeamA)
        {
            TryStartAutoRelease();
            return;
        }

        if (aiShipsInsideCount <= 0)
        {
            return;
        }

        TickCaptureForward();
    }

    /// <summary>
    /// Returns the active server tick interval based on whether the zone is capturing or auto-releasing.
    /// </summary>
    /// <returns>Tick interval in seconds.</returns>
    private float GetCurrentServerTickInterval()
    {
        if (isAutoReleasing)
        {
            return Mathf.Max(0.02f, releaseTickInterval);
        }

        return Mathf.Max(0.02f, captureTickInterval);
    }

    /// <summary>
    /// Increases progress toward full capture while AI ships are inside the zone.
    /// </summary>
    private void TickCaptureForward()
    {
        float step = Mathf.Max(0.01f, captureProgressPerTick);
        float previousProgress = captureProgress;

        captureProgress = Mathf.Clamp(captureProgress + step, 0f, 100f);

        if (Mathf.Approximately(previousProgress, captureProgress))
        {
            return;
        }

        if (captureProgress >= 100f)
        {
            captureState = CurrentCaptureState.CapturedTeamA;
            autoReleaseStartTime = Time.time + Mathf.Max(0f, autoReleaseDelay);
            SetVisualModeServer(ZoneVisualMode.SolidCaptured);
        }
        else
        {
            captureState = CurrentCaptureState.CapturingTeamA;
            SetVisualModeServer(ZoneVisualMode.PulsingCapture);
        }

        PushZoneStatsToManager();
    }

    /// <summary>
    /// Starts the automatic reverse phase after the hold delay expires.
    /// </summary>
    private void TryStartAutoRelease()
    {
        if (autoReleaseStartTime < 0f)
        {
            autoReleaseStartTime = Time.time + Mathf.Max(0f, autoReleaseDelay);
            return;
        }

        if (Time.time < autoReleaseStartTime)
        {
            return;
        }

        isAutoReleasing = true;
        SetVisualModeServer(ZoneVisualMode.PulsingRelease);
    }

    /// <summary>
    /// Decreases progress back toward neutral during the automatic reverse phase.
    /// </summary>
    private void TickAutoRelease()
    {
        float step = Mathf.Max(0.01f, releaseProgressPerTick);
        float previousProgress = captureProgress;

        captureProgress = Mathf.Clamp(captureProgress - step, 0f, 100f);

        if (Mathf.Approximately(previousProgress, captureProgress))
        {
            return;
        }

        if (captureProgress <= 0f)
        {
            captureProgress = 0f;
            isAutoReleasing = false;
            autoReleaseStartTime = -1f;
            captureState = CurrentCaptureState.Neutral;
            SetVisualModeServer(ZoneVisualMode.SolidNeutral);
        }
        else
        {
            captureState = CurrentCaptureState.CapturingTeamA;
            SetVisualModeServer(ZoneVisualMode.PulsingRelease);
        }

        PushZoneStatsToManager();
    }

    #endregion

    #region Manager Sync

    /// <summary>
    /// Resolves the capture zone manager reference.
    /// </summary>
    private void ResolveCaptureZoneManager()
    {
        if (captureZoneManager != null)
        {
            return;
        }

        captureZoneManager = ReferenceManager.GetManager<CaptureZoneManager>();
    }

    /// <summary>
    /// Registers this zone world position in the manager.
    /// </summary>
    private void RegisterZonePosition()
    {
        if (captureZoneManager == null)
        {
            ResolveCaptureZoneManager();
        }

        if (captureZoneManager == null)
        {
            return;
        }

        captureZoneManager.SetCaptureZonePosition((int)captureZoneID, transform.position);
    }

    /// <summary>
    /// Pushes the current progress and state to the manager.
    /// </summary>
    private void PushZoneStatsToManager()
    {
        if (captureZoneManager == null)
        {
            ResolveCaptureZoneManager();
        }

        if (captureZoneManager == null)
        {
            return;
        }

        captureZoneManager.UpdateCaptureZoneStats((int)captureZoneID, captureProgress, captureState);
    }

    #endregion

    #region Visuals

    /// <summary>
    /// Sets the zone visual mode on the server and broadcasts it to observers.
    /// </summary>
    /// <param name="newMode">New visual mode.</param>
    private void SetVisualModeServer(ZoneVisualMode newMode)
    {
        if (currentVisualMode == newMode)
        {
            return;
        }

        currentVisualMode = newMode;
        ApplyVisualModeOnObservers((int)newMode);
    }

    /// <summary>
    /// Applies a visual mode on all observers.
    /// </summary>
    /// <param name="modeValue">Visual mode as int.</param>
    [ObserversRpc]
    private void ApplyVisualModeOnObservers(int modeValue)
    {
        currentVisualMode = (ZoneVisualMode)modeValue;
        ApplyVisualModeInstantLocal();
    }

    /// <summary>
    /// Updates local visuals every frame so pulse animation is smooth.
    /// </summary>
    private void UpdateVisualsLocal()
    {
        if (currentVisualMode != ZoneVisualMode.PulsingCapture && currentVisualMode != ZoneVisualMode.PulsingRelease)
        {
            return;
        }

        float pulseValue = Mathf.PingPong(Time.time * Mathf.Max(0.1f, pulseSpeed), 1f);
        bool shouldUseSecondaryMaterial = pulseValue >= 0.5f;

        if (pulseCurrentlyUsingSecondaryMaterial == shouldUseSecondaryMaterial)
        {
            return;
        }

        pulseCurrentlyUsingSecondaryMaterial = shouldUseSecondaryMaterial;
        ApplyPulseVisualStep();
    }

    /// <summary>
    /// Applies the current visual mode immediately on this instance.
    /// </summary>
    private void ApplyVisualModeInstantLocal()
    {
        if (currentVisualMode == ZoneVisualMode.SolidNeutral)
        {
            ApplySolidNeutralVisualsLocal();
            return;
        }

        if (currentVisualMode == ZoneVisualMode.SolidCaptured)
        {
            ApplySolidCapturedVisualsLocal();
            return;
        }

        pulseCurrentlyUsingSecondaryMaterial = false;
        ApplyPulseVisualStep();
    }

    /// <summary>
    /// Applies one pulse step based on the current pulse mode and pulse phase.
    /// </summary>
    private void ApplyPulseVisualStep()
    {
        if (currentVisualMode == ZoneVisualMode.PulsingCapture)
        {
            if (pulseCurrentlyUsingSecondaryMaterial)
            {
                ApplyCapturedMaterialToZoneRenderers();
                return;
            }

            ApplyNeutralMaterialToZoneRenderers();
            return;
        }

        if (currentVisualMode == ZoneVisualMode.PulsingRelease)
        {
            if (pulseCurrentlyUsingSecondaryMaterial)
            {
                ApplyRegressedMaterialToZoneRenderers();
                return;
            }

            ApplyNeutralMaterialToZoneRenderers();
        }
    }

    /// <summary>
    /// Applies the neutral visual state locally.
    /// </summary>
    private void ApplySolidNeutralVisualsLocal()
    {
        pulseCurrentlyUsingSecondaryMaterial = false;
        ApplyNeutralMaterialToZoneRenderers();
    }

    /// <summary>
    /// Applies the captured visual state locally.
    /// </summary>
    private void ApplySolidCapturedVisualsLocal()
    {
        pulseCurrentlyUsingSecondaryMaterial = true;
        ApplyCapturedMaterialToZoneRenderers();
    }

    /// <summary>
    /// Applies the neutral material to all zone renderers.
    /// </summary>
    private void ApplyNeutralMaterialToZoneRenderers()
    {
        ApplyMaterialToRenderer(mainRenderer, neutralZoneMaterial);
        ApplyMaterialToRenderer(glowRenderer, neutralZoneMaterial);
    }

    /// <summary>
    /// Applies the captured material to all zone renderers.
    /// </summary>
    private void ApplyCapturedMaterialToZoneRenderers()
    {
        ApplyMaterialToRenderer(mainRenderer, capturedZoneMaterial);
        ApplyMaterialToRenderer(glowRenderer, capturedZoneMaterial);
    }

    /// <summary>
    /// Applies the regress material to all zone renderers.
    /// </summary>
    private void ApplyRegressedMaterialToZoneRenderers()
    {
        ApplyMaterialToRenderer(mainRenderer, regressedZoneMaterial);
        ApplyMaterialToRenderer(glowRenderer, regressedZoneMaterial);
    }

    /// <summary>
    /// Applies a shared material asset to a renderer.
    /// </summary>
    /// <param name="targetRenderer">Renderer to update.</param>
    /// <param name="targetMaterial">Material asset to assign.</param>
    private void ApplyMaterialToRenderer(Renderer targetRenderer, Material targetMaterial)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (targetMaterial == null)
        {
            return;
        }

        if (targetRenderer.sharedMaterial == targetMaterial)
        {
            return;
        }

        targetRenderer.sharedMaterial = targetMaterial;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resets the zone runtime values to the initial neutral state.
    /// </summary>
    private void ResetZoneRuntimeState()
    {
        captureState = CurrentCaptureState.Neutral;
        captureProgress = 0f;
        aiShipsInsideCount = 0;
        isAutoReleasing = false;

        aiShipKeysInside.Clear();

        nextCaptureTickTime = 0f;
        autoReleaseStartTime = -1f;

        currentVisualMode = ZoneVisualMode.SolidNeutral;
        pulseCurrentlyUsingSecondaryMaterial = false;
    }

    /// <summary>
    /// Tries to resolve a unique AI ship key from a trigger collider.
    /// </summary>
    /// <param name="other">Trigger collider.</param>
    /// <param name="shipKey">Resolved ship key.</param>
    /// <returns>True if an AI ship was found.</returns>
    private bool TryGetAIShipKey(Collider other, out int shipKey)
    {
        shipKey = 0;

        if (other == null)
        {
            return false;
        }

        AIShipHolder shipHolder = other.GetComponentInParent<AIShipHolder>();
        if (shipHolder == null)
        {
            return false;
        }

        shipKey = shipHolder.GetInstanceID();
        return true;
    }

    #endregion

    #region Public Getters

    /// <summary>
    /// Returns this capture zone identifier.
    /// </summary>
    /// <returns>Capture zone id enum value.</returns>
    public CaptureZoneID GetCaptureZoneID()
    {
        return captureZoneID;
    }

    #endregion
}