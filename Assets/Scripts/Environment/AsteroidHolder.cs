using PurrNet;
using UnityEngine;

/// <summary>
/// AsteroidHolder is responsible for managing the visual representation and rotation of an asteroid in the game world.
/// </summary>
[DisallowMultipleComponent]
public sealed class AsteroidHolder : NetworkBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [SerializeField] private GameObject asteroidModel;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Header("Runtime Hierarchy")]
    [SerializeField] private bool keepUnderEnvironmentFolder = true;
    [SerializeField] private string environmentRootName = "Environment";
    [SerializeField] private string asteroidContainerName = "Asteroids";
    [SerializeField] private bool preserveWorldTransformOnReparent = true;

    #endregion

    #region Networked Rotation State

    private readonly SyncVar<double> rotationStartTick = new SyncVar<double>(0.0);
    private readonly SyncVar<float> rotationStartAngleDegrees = new SyncVar<float>(0f);

    #endregion

    #region Runtime Fields

    private bool needsHierarchyAttach;
    private Quaternion initialModelLocalRotation;
    private bool cachedInitialRotation;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheInitialModelRotationIfNeeded();
    }

    private void OnDisable()
    {
        rotationStartTick.onChanged -= HandleRotationSeedChanged;
        rotationStartAngleDegrees.onChanged -= HandleRotationSeedChanged;
    }

    #endregion

    #region Network Lifecycle

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        CacheInitialModelRotationIfNeeded();

        rotationStartTick.onChanged -= HandleRotationSeedChanged;
        rotationStartAngleDegrees.onChanged -= HandleRotationSeedChanged;

        rotationStartTick.onChanged += HandleRotationSeedChanged;
        rotationStartAngleDegrees.onChanged += HandleRotationSeedChanged;

        needsHierarchyAttach = keepUnderEnvironmentFolder;
        TryAttachToEnvironmentContainer();

        if (asServer)
        {
            SeedRotationOnServer();
        }

        ApplySynchronizedRotation();
    }

    #endregion

    #region Unity Methods

    private void Update()
    {
        if (needsHierarchyAttach)
        {
            TryAttachToEnvironmentContainer();
        }

        ApplySynchronizedRotation();
    }

    #endregion

    #region Rotation Sync

    /// <summary>
    /// Seeds the rotation start tick and a random start angle on the server using network tick time.
    /// </summary>
    [ServerOnly]
    private void SeedRotationOnServer()
    {
        double currentTick;
        if (!TryGetCurrentNetworkTick(out currentTick))
        {
            currentTick = 0.0;
        }

        rotationStartTick.value = currentTick;
        rotationStartAngleDegrees.value = Random.Range(0f, 360f);
    }

    /// <summary>
    /// Called when rotation seed SyncVars change; forces an immediate rotation refresh.
    /// </summary>
    /// <param name="unused">New value of the changed SyncVar (ignored).</param>
    private void HandleRotationSeedChanged(double unused)
    {
        ApplySynchronizedRotation();
    }

    /// <summary>
    /// Called when rotation seed SyncVars change; forces an immediate rotation refresh.
    /// </summary>
    /// <param name="unused">New value of the changed SyncVar (ignored).</param>
    private void HandleRotationSeedChanged(float unused)
    {
        ApplySynchronizedRotation();
    }

    /// <summary>
    /// Computes and applies the asteroid's rotation from synchronized network tick time.
    /// </summary>
    private void ApplySynchronizedRotation()
    {
        if (asteroidModel == null)
        {
            return;
        }

        CacheInitialModelRotationIfNeeded();
        if (!cachedInitialRotation)
        {
            return;
        }

        Vector3 axis = rotationAxis;
        if (axis.sqrMagnitude <= 0.0001f)
        {
            axis = Vector3.up;
        }

        axis = axis.normalized;

        double currentTick;
        bool hasTick = TryGetCurrentNetworkTick(out currentTick);

        double startTick = rotationStartTick.value;
        double elapsedTicks = currentTick - startTick;

        if (!hasTick)
        {
            elapsedTicks = Time.time;
        }

        if (elapsedTicks < 0.0)
        {
            elapsedTicks = 0.0;
        }

        float tickDeltaSeconds;
        double elapsedSeconds;

        if (TryGetTickDeltaSeconds(out tickDeltaSeconds))
        {
            elapsedSeconds = elapsedTicks * tickDeltaSeconds;
        }
        else
        {
            elapsedSeconds = elapsedTicks;
        }

        double degrees = rotationStartAngleDegrees.value + (rotationSpeed * elapsedSeconds);
        degrees = degrees % 360.0;

        Quaternion spin = Quaternion.AngleAxis((float)degrees, axis);
        asteroidModel.transform.localRotation = initialModelLocalRotation * spin;
    }

    /// <summary>
    /// Attempts to read the current synchronized network tick time from the NetworkManager.
    /// </summary>
    /// <param name="tick">Output current tick.</param>
    /// <returns>True if network tick is available.</returns>
    private bool TryGetCurrentNetworkTick(out double tick)
    {
        tick = 0.0;

        NetworkManager manager = NetworkManager.main;
        if (manager == null)
        {
            return false;
        }

        if (manager.tickModule == null)
        {
            return false;
        }

        tick = manager.tickModule.rollbackTick;
        return true;
    }

    /// <summary>
    /// Attempts to read the current tick delta in seconds from the NetworkManager.
    /// </summary>
    /// <param name="tickDeltaSeconds">Output tick delta in seconds.</param>
    /// <returns>True if tick delta is available.</returns>
    private bool TryGetTickDeltaSeconds(out float tickDeltaSeconds)
    {
        tickDeltaSeconds = 0f;

        NetworkManager manager = NetworkManager.main;
        if (manager == null)
        {
            return false;
        }

        if (manager.tickModule == null)
        {
            return false;
        }

        tickDeltaSeconds = manager.tickModule.tickDelta;
        return tickDeltaSeconds > 0.000001f;
    }

    /// <summary>
    /// Caches the model's initial local rotation if possible.
    /// </summary>
    private void CacheInitialModelRotationIfNeeded()
    {
        if (cachedInitialRotation)
        {
            return;
        }

        if (asteroidModel == null)
        {
            return;
        }

        initialModelLocalRotation = asteroidModel.transform.localRotation;
        cachedInitialRotation = true;
    }

    #endregion

    #region Hierarchy

    /// <summary>
    /// Tries to attach this asteroid under Environment/Asteroids locally.
    /// </summary>
    private void TryAttachToEnvironmentContainer()
    {
        if (!keepUnderEnvironmentFolder)
        {
            needsHierarchyAttach = false;
            return;
        }

        Transform container = GetOrCreateAsteroidContainer();
        if (container == null)
        {
            return;
        }

        Transform currentParent = transform.parent;
        if (currentParent == container)
        {
            needsHierarchyAttach = false;
            return;
        }

        transform.SetParent(container, preserveWorldTransformOnReparent);
        needsHierarchyAttach = false;
    }

    /// <summary>
    /// Gets or creates the Environment/Asteroids runtime container.
    /// </summary>
    /// <returns>Asteroid container transform or null if creation failed.</returns>
    private Transform GetOrCreateAsteroidContainer()
    {
        GameObject environmentRootObject = GameObject.Find(environmentRootName);
        if (environmentRootObject == null)
        {
            environmentRootObject = new GameObject(environmentRootName);
        }

        if (environmentRootObject == null)
        {
            return null;
        }

        Transform environmentRoot = environmentRootObject.transform;
        Transform asteroidContainer = environmentRoot.Find(asteroidContainerName);

        if (asteroidContainer != null)
        {
            return asteroidContainer;
        }

        GameObject containerObject = new GameObject(asteroidContainerName);
        if (containerObject == null)
        {
            return null;
        }

        containerObject.transform.SetParent(environmentRoot, false);
        return containerObject.transform;
    }

    #endregion

    #region Observer Methods

    /// <summary>
    /// Enables or disables asteroid visuals for all connected observers.
    /// </summary>
    /// <param name="value">True to enable visuals, false to disable.</param>
    [ObserversRpc]
    public void HandleActivationOnObservers(bool value)
    {
        if (asteroidModel == null)
        {
            return;
        }

        asteroidModel.SetActive(value);
    }

    #endregion
}