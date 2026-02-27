using System;
using UnityEngine;

/// <summary>
/// Manager for tracking capture zones in the scene, their states, progress, and positions.
/// </summary>
public sealed class CaptureZoneManager : MonoBehaviour
{
    #region Variables

    [SerializeField] private CaptureZone[] captureZones;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        ReferenceManager.RegisterManager<CaptureZoneManager>(this);
        BuildZoneArrayFromSceneControllers();
    }

    private void Start()
    {
        BuildZoneArrayFromSceneControllers();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Updates capture zone progress/state for a zone id.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <param name="captureProgress">Progress value.</param>
    /// <param name="currentCaptureState">Current capture state.</param>
    public void UpdateCaptureZoneStats(int captureZoneID, float captureProgress, CurrentCaptureState currentCaptureState)
    {
        EnsureZoneSlotExists(captureZoneID);

        if (!IsValidZoneId(captureZoneID))
        {
            return;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return;
        }

        zone.SetZoneId(captureZoneID);
        zone.SetProgress(captureProgress);
        zone.SetState(currentCaptureState);
    }

    /// <summary>
    /// Sets a zone world position and marks it as registered.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <param name="captureZonePosition">World position.</param>
    public void SetCaptureZonePosition(int captureZoneID, Vector3 captureZonePosition)
    {
        EnsureZoneSlotExists(captureZoneID);

        if (!IsValidZoneId(captureZoneID))
        {
            return;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return;
        }

        zone.SetZoneId(captureZoneID);
        zone.SetPosition(captureZonePosition);
        zone.SetRegistered(true);
    }

    /// <summary>
    /// Gets capture progress for a zone.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <returns>Capture progress.</returns>
    public float GetCurrentCaptureProgress(int captureZoneID)
    {
        TryBuildZoneArrayIfEmpty();

        if (!IsValidZoneId(captureZoneID))
        {
            return 0f;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return 0f;
        }

        return zone.GetProgress();
    }

    /// <summary>
    /// Gets capture state for a zone.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <returns>Current capture state.</returns>
    public CurrentCaptureState GetCurrentCaptureState(int captureZoneID)
    {
        TryBuildZoneArrayIfEmpty();

        if (!IsValidZoneId(captureZoneID))
        {
            return CurrentCaptureState.Neutral;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return CurrentCaptureState.Neutral;
        }

        return zone.GetState();
    }

    /// <summary>
    /// Gets world position for a zone.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <returns>Zone world position.</returns>
    public Vector3 GetCaptureZonePosition(int captureZoneID)
    {
        TryBuildZoneArrayIfEmpty();

        if (!IsValidZoneId(captureZoneID))
        {
            return Vector3.zero;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return Vector3.zero;
        }

        return zone.GetPosition();
    }

    #endregion

    #region Zone Queries

    /// <summary>
    /// Returns the number of configured capture zone slots.
    /// </summary>
    /// <returns>Zone count.</returns>
    public int GetCaptureZoneCount()
    {
        TryBuildZoneArrayIfEmpty();

        if (captureZones == null)
        {
            return 0;
        }

        return captureZones.Length;
    }

    /// <summary>
    /// Tries to read the capture state for a zone.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <param name="state">Zone state output.</param>
    /// <returns>True if the zone id is valid and registered.</returns>
    public bool TryGetCurrentCaptureState(int captureZoneID, out CurrentCaptureState state)
    {
        state = CurrentCaptureState.Neutral;

        TryBuildZoneArrayIfEmpty();

        if (!IsValidZoneId(captureZoneID))
        {
            return false;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return false;
        }

        if (!zone.IsRegistered())
        {
            return false;
        }

        state = zone.GetState();
        return true;
    }

    /// <summary>
    /// Tries to read the capture zone world position.
    /// </summary>
    /// <param name="captureZoneID">Zone id.</param>
    /// <param name="position">Position output.</param>
    /// <returns>True if the zone id is valid and registered.</returns>
    public bool TryGetCaptureZonePosition(int captureZoneID, out Vector3 position)
    {
        position = Vector3.zero;

        TryBuildZoneArrayIfEmpty();

        if (!IsValidZoneId(captureZoneID))
        {
            return false;
        }

        CaptureZone zone = captureZones[captureZoneID];
        if (zone == null)
        {
            return false;
        }

        if (!zone.IsRegistered())
        {
            return false;
        }

        position = zone.GetPosition();
        return true;
    }

    #endregion

    #region Auto Detection

    /// <summary>
    /// Builds or rebuilds the zone slots by scanning scene CaptureZoneController components.
    /// </summary>
    private void BuildZoneArrayFromSceneControllers()
    {
        CaptureZoneController[] zoneControllers = UnityEngine.Object.FindObjectsByType<CaptureZoneController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (zoneControllers == null || zoneControllers.Length == 0)
        {
            if (captureZones == null)
            {
                captureZones = new CaptureZone[0];
            }

            return;
        }

        int maxZoneId = -1;

        int index = 0;
        while (index < zoneControllers.Length)
        {
            CaptureZoneController controller = zoneControllers[index];

            if (controller != null)
            {
                int zoneId = (int)controller.GetCaptureZoneID();
                if (zoneId > maxZoneId)
                {
                    maxZoneId = zoneId;
                }
            }

            index++;
        }

        if (maxZoneId < 0)
        {
            return;
        }

        EnsureArrayCapacity(maxZoneId + 1);
        EnsureAllSlotsCreated();

        int clearIndex = 0;
        while (clearIndex < captureZones.Length)
        {
            if (captureZones[clearIndex] != null)
            {
                captureZones[clearIndex].SetRegistered(false);
                captureZones[clearIndex].SetZoneId(clearIndex);
            }

            clearIndex++;
        }

        int controllerIndex = 0;
        while (controllerIndex < zoneControllers.Length)
        {
            CaptureZoneController controller = zoneControllers[controllerIndex];

            if (controller != null)
            {
                int zoneId = (int)controller.GetCaptureZoneID();

                if (zoneId >= 0 && zoneId < captureZones.Length)
                {
                    CaptureZone zone = captureZones[zoneId];

                    if (zone != null)
                    {
                        zone.SetZoneId(zoneId);
                        zone.SetPosition(controller.transform.position);
                        zone.SetRegistered(true);
                    }
                }
            }

            controllerIndex++;
        }
    }

    /// <summary>
    /// Attempts a scene scan only when the zone array is missing or empty.
    /// </summary>
    private void TryBuildZoneArrayIfEmpty()
    {
        if (captureZones != null && captureZones.Length > 0)
        {
            return;
        }

        BuildZoneArrayFromSceneControllers();
    }

    #endregion

    #region Validation and Allocation

    /// <summary>
    /// Ensures a zone slot exists for the given id by expanding the array if needed.
    /// </summary>
    /// <param name="zoneId">Zone id.</param>
    private void EnsureZoneSlotExists(int zoneId)
    {
        if (zoneId < 0)
        {
            return;
        }

        EnsureArrayCapacity(zoneId + 1);
        EnsureAllSlotsCreated();

        if (captureZones[zoneId] != null)
        {
            captureZones[zoneId].SetZoneId(zoneId);
        }
    }

    /// <summary>
    /// Ensures the internal array has at least the requested capacity.
    /// </summary>
    /// <param name="requiredLength">Required array length.</param>
    private void EnsureArrayCapacity(int requiredLength)
    {
        if (requiredLength <= 0)
        {
            return;
        }

        if (captureZones == null)
        {
            captureZones = new CaptureZone[requiredLength];
            return;
        }

        if (captureZones.Length >= requiredLength)
        {
            return;
        }

        CaptureZone[] newArray = new CaptureZone[requiredLength];

        int index = 0;
        while (index < captureZones.Length)
        {
            newArray[index] = captureZones[index];
            index++;
        }

        captureZones = newArray;
    }

    /// <summary>
    /// Creates missing zone slot objects in the internal array.
    /// </summary>
    private void EnsureAllSlotsCreated()
    {
        if (captureZones == null)
        {
            return;
        }

        int index = 0;
        while (index < captureZones.Length)
        {
            if (captureZones[index] == null)
            {
                CaptureZone newZone = new CaptureZone();
                newZone.SetZoneId(index);
                newZone.SetState(CurrentCaptureState.Neutral);
                newZone.SetProgress(0f);
                newZone.SetRegistered(false);

                captureZones[index] = newZone;
            }

            index++;
        }
    }

    /// <summary>
    /// Checks if a zone id is valid for the current array.
    /// </summary>
    /// <param name="zoneId">Zone id.</param>
    /// <returns>True if valid, otherwise false.</returns>
    private bool IsValidZoneId(int zoneId)
    {
        if (captureZones == null)
        {
            return false;
        }

        if (zoneId < 0)
        {
            return false;
        }

        if (zoneId >= captureZones.Length)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Nested Types

    [Serializable]
    private sealed class CaptureZone
    {
        #region Variables

        [SerializeField] private Vector3 captureZonePosition;
        [SerializeField] private CurrentCaptureState captureState;
        [SerializeField] private float captureProgress;
        [SerializeField] private int captureZoneID;
        [SerializeField] private bool isRegistered;

        #endregion

        #region Setters

        /// <summary>
        /// Sets the zone position.
        /// </summary>
        /// <param name="position">World position.</param>
        public void SetPosition(Vector3 position)
        {
            captureZonePosition = position;
        }

        /// <summary>
        /// Sets the current capture state.
        /// </summary>
        /// <param name="state">Capture state.</param>
        public void SetState(CurrentCaptureState state)
        {
            captureState = state;
        }

        /// <summary>
        /// Sets the capture progress.
        /// </summary>
        /// <param name="progress">Progress value.</param>
        public void SetProgress(float progress)
        {
            captureProgress = progress;
        }

        /// <summary>
        /// Sets the zone identifier.
        /// </summary>
        /// <param name="id">Zone id.</param>
        public void SetZoneId(int id)
        {
            captureZoneID = id;
        }

        /// <summary>
        /// Sets whether the zone is registered by a live controller.
        /// </summary>
        /// <param name="value">Registration state.</param>
        public void SetRegistered(bool value)
        {
            isRegistered = value;
        }

        #endregion

        #region Getters

        /// <summary>
        /// Gets the zone position.
        /// </summary>
        /// <returns>World position.</returns>
        public Vector3 GetPosition()
        {
            return captureZonePosition;
        }

        /// <summary>
        /// Gets the capture state.
        /// </summary>
        /// <returns>Capture state.</returns>
        public CurrentCaptureState GetState()
        {
            return captureState;
        }

        /// <summary>
        /// Gets the capture progress.
        /// </summary>
        /// <returns>Progress value.</returns>
        public float GetProgress()
        {
            return captureProgress;
        }

        /// <summary>
        /// Gets the zone id.
        /// </summary>
        /// <returns>Zone id.</returns>
        public int GetZoneId()
        {
            return captureZoneID;
        }

        /// <summary>
        /// Returns true if this zone has been registered by a controller.
        /// </summary>
        /// <returns>True if registered.</returns>
        public bool IsRegistered()
        {
            return isRegistered;
        }

        #endregion
    }

    #endregion
}