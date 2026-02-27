using System.Collections;
using PurrNet;
using UnityEngine;

/// <summary>
/// AIShipHolder is a networked component that manages the state and visuals of an AI-controlled ship in a multiplayer game.
/// </summary>
public sealed class AIShipHolder : NetworkBehaviour
{
    #region Serialized References

    [SerializeField] private Transform partsRoot;
    [SerializeField] private Renderer[] shipRenderers;
    [SerializeField] private Collider[] shipColliders;
    [SerializeField] private Behaviour aiController;
    [SerializeField] private Rigidbody shipBody;

    #endregion

    #region Serialized Settings

    [SerializeField] private float maxHealth = 200f;
    [SerializeField] private bool applySpawnColor = true;
    [SerializeField] private Color shipColor = Color.cyan;

    #endregion

    #region Runtime State

    private AIEventsManager eventsManager;
    private MaterialPropertyBlock colorBlock;

    private readonly SyncVar<string> replicatedShipName = new SyncVar<string>(string.Empty);

    private bool hasSpawnedOnNetwork;
    private string pendingServerName;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        colorBlock = new MaterialPropertyBlock();
        CachePartsIfNeeded();
        CacheRigidBodyIfNeeded();
    }

    private void OnDisable()
    {
        replicatedShipName.onChanged -= HandleReplicatedNameChanged;

        if (!isServer)
        {
            return;
        }

        if (eventsManager == null)
        {
            return;
        }

        eventsManager.UnSubscribeOnAIDeathRespawnTriggered(HandleDeathRespawnServer);
    }

    #endregion

    #region Network Lifecycle

    protected override void OnSpawned(bool asServer)
    {
        hasSpawnedOnNetwork = true;

        CachePartsIfNeeded();
        CacheRigidBodyIfNeeded();

        replicatedShipName.onChanged -= HandleReplicatedNameChanged;
        replicatedShipName.onChanged += HandleReplicatedNameChanged;

        if (asServer && !string.IsNullOrEmpty(pendingServerName))
        {
            replicatedShipName.value = pendingServerName;
        }

        ApplyUnityObjectName(replicatedShipName.value);

        if (!asServer)
        {
            return;
        }

        EnsureEventsManager();

        if (applySpawnColor)
        {
            ApplySpawnColorRequest();
        }

        BeginInterpolationWarmupRequest();
    }

    #endregion

    #region Naming

    /// <summary>
    /// Sets the ship name on the server, which will replicate to all clients and be applied to the GameObject name.
    /// </summary>
    /// <param name="newName">Name to apply.</param>
    [ServerOnly]
    public void SetNetworkedShipName(string newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            return;
        }

        pendingServerName = newName;

        ApplyUnityObjectName(newName);

        if (!hasSpawnedOnNetwork)
        {
            return;
        }

        replicatedShipName.value = newName;
    }

    /// <summary>
    /// Callback invoked when the replicated name changes on any instance.
    /// </summary>
    /// <param name="newValue">New replicated name.</param>
    private void HandleReplicatedNameChanged(string newValue)
    {
        ApplyUnityObjectName(newValue);
    }

    /// <summary>
    /// Applies the Unity GameObject name locally if valid.
    /// </summary>
    /// <param name="newName">Name to apply.</param>
    private void ApplyUnityObjectName(string newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            return;
        }

        name = newName;
    }

    #endregion

    #region Event Hub

    /// <summary>
    /// Creates the event hub if missing and wires required server-side listeners.
    /// </summary>
    private void EnsureEventsManager()
    {
        if (eventsManager != null)
        {
            return;
        }

        eventsManager = new AIEventsManager();
        eventsManager.SubscribeOnAIDeathRespawnTriggered(HandleDeathRespawnServer);
    }

    #endregion

    #region Parts Caching

    /// <summary>
    /// Caches renderers and colliders from the ship parts hierarchy when arrays are empty.
    /// </summary>
    private void CachePartsIfNeeded()
    {
        Transform searchRoot = ResolvePartsRoot();
        CacheRenderersIfNeeded(searchRoot);
        CacheCollidersIfNeeded(searchRoot);
    }

    /// <summary>
    /// Resolves the hierarchy root used for auto-caching ship parts.
    /// </summary>
    /// <returns>Transform used as the search root.</returns>
    private Transform ResolvePartsRoot()
    {
        if (partsRoot != null)
        {
            return partsRoot;
        }

        return transform;
    }

    /// <summary>
    /// Populates the renderer array if it is not assigned in the inspector.
    /// </summary>
    /// <param name="searchRoot">Search root transform.</param>
    private void CacheRenderersIfNeeded(Transform searchRoot)
    {
        if (shipRenderers != null && shipRenderers.Length > 0)
        {
            return;
        }

        if (searchRoot == null)
        {
            shipRenderers = new Renderer[0];
            return;
        }

        shipRenderers = searchRoot.GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// Populates the collider array if it is not assigned in the inspector.
    /// </summary>
    /// <param name="searchRoot">Search root transform.</param>
    private void CacheCollidersIfNeeded(Transform searchRoot)
    {
        if (shipColliders != null && shipColliders.Length > 0)
        {
            return;
        }

        if (searchRoot == null)
        {
            shipColliders = new Collider[0];
            return;
        }

        shipColliders = searchRoot.GetComponentsInChildren<Collider>(true);
    }

    /// <summary>
    /// Resolves the Rigidbody reference if it is not assigned in the inspector.
    /// </summary>
    private void CacheRigidBodyIfNeeded()
    {
        if (shipBody != null)
        {
            return;
        }

        Rigidbody foundBody = GetComponent<Rigidbody>();
        if (foundBody == null)
        {
            return;
        }

        shipBody = foundBody;
    }

    #endregion

    #region Interpolation

    /// <summary>
    /// Requests interpolation warmup on all observers.
    /// </summary>
    [ServerOnly]
    private void BeginInterpolationWarmupRequest()
    {
        StartInterpolationWarmupOnObservers();
    }

    /// <summary>
    /// Starts the interpolation warmup routine on all observers.
    /// </summary>
    [ObserversRpc]
    private void StartInterpolationWarmupOnObservers()
    {
        if (shipBody == null)
        {
            return;
        }

        StartCoroutine(InterpolationWarmupRoutine());
    }

    /// <summary>
    /// Enables Rigidbody interpolation after a short delay to avoid spawn smoothing glitches.
    /// </summary>
    /// <returns>Coroutine enumerator.</returns>
    private IEnumerator InterpolationWarmupRoutine()
    {
        yield return new WaitForSeconds(1f);

        if (shipBody == null)
        {
            yield break;
        }

        shipBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    #endregion

    #region Visuals

    /// <summary>
    /// Requests the configured ship color on all observers.
    /// </summary>
    [ServerOnly]
    private void ApplySpawnColorRequest()
    {
        ApplySpawnColorOnObservers();
    }

    /// <summary>
    /// Applies the configured ship color on all observers.
    /// </summary>
    [ObserversRpc]
    private void ApplySpawnColorOnObservers()
    {
        ApplyColorToAllRenderers(shipColor);
    }

    /// <summary>
    /// Applies a color to all cached ship renderers using a MaterialPropertyBlock.
    /// </summary>
    /// <param name="colorValue">Desired color.</param>
    private void ApplyColorToAllRenderers(Color colorValue)
    {
        if (shipRenderers == null)
        {
            return;
        }

        int index = 0;
        while (index < shipRenderers.Length)
        {
            Renderer targetRenderer = shipRenderers[index];
            ApplyColorToRenderer(targetRenderer, colorValue);
            index++;
        }
    }

    /// <summary>
    /// Applies a color to a single renderer using a MaterialPropertyBlock.
    /// </summary>
    /// <param name="targetRenderer">Target renderer.</param>
    /// <param name="colorValue">Desired color.</param>
    private void ApplyColorToRenderer(Renderer targetRenderer, Color colorValue)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (colorBlock == null)
        {
            colorBlock = new MaterialPropertyBlock();
        }

        targetRenderer.GetPropertyBlock(colorBlock);

        Material sharedMaterial = targetRenderer.sharedMaterial;
        if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId))
        {
            colorBlock.SetColor(BaseColorId, colorValue);
        }
        else
        {
            colorBlock.SetColor(ColorId, colorValue);
        }

        targetRenderer.SetPropertyBlock(colorBlock);
    }

    #endregion

    #region Alive Toggle

    /// <summary>
    /// Server handler for death/respawn toggles coming from the AIEventsManager.
    /// </summary>
    /// <param name="isAliveState">True to enable, false to disable.</param>
    [ServerOnly]
    private void HandleDeathRespawnServer(bool isAliveState)
    {
        SetAliveStateOnObservers(isAliveState);
    }

    /// <summary>
    /// Applies alive/dead state on all observers.
    /// </summary>
    /// <param name="isAliveState">True to enable, false to disable.</param>
    [ObserversRpc]
    private void SetAliveStateOnObservers(bool isAliveState)
    {
        if (aiController != null)
        {
            aiController.enabled = isAliveState;
        }

        SetRenderersEnabled(isAliveState);
        SetCollidersEnabled(isAliveState);

        if (shipBody == null)
        {
            return;
        }

        if (!isAliveState)
        {
            shipBody.interpolation = RigidbodyInterpolation.None;
            shipBody.linearVelocity = Vector3.zero;
            shipBody.angularVelocity = Vector3.zero;
            return;
        }

        StartCoroutine(InterpolationWarmupRoutine());
    }

    /// <summary>
    /// Enables or disables all cached ship renderers.
    /// </summary>
    /// <param name="enabledState">True to enable, false to disable.</param>
    private void SetRenderersEnabled(bool enabledState)
    {
        if (shipRenderers == null)
        {
            return;
        }

        int index = 0;
        while (index < shipRenderers.Length)
        {
            Renderer cachedRenderer = shipRenderers[index];
            if (cachedRenderer != null)
            {
                cachedRenderer.enabled = enabledState;
            }

            index++;
        }
    }

    /// <summary>
    /// Enables or disables all cached ship colliders.
    /// </summary>
    /// <param name="enabledState">True to enable, false to disable.</param>
    private void SetCollidersEnabled(bool enabledState)
    {
        if (shipColliders == null)
        {
            return;
        }

        int index = 0;
        while (index < shipColliders.Length)
        {
            Collider cachedCollider = shipColliders[index];
            if (cachedCollider != null)
            {
                cachedCollider.enabled = enabledState;
            }

            index++;
        }
    }

    #endregion

    #region Public Read Access

    /// <summary>
    /// Returns the AI event hub owned by this ship on the server.
    /// </summary>
    /// <returns>AI events manager instance.</returns>
    public AIEventsManager GetAIEventsManager()
    {
        return eventsManager;
    }

    /// <summary>
    /// Returns the configured max health for this AI ship.
    /// </summary>
    /// <returns>Max health value.</returns>
    public float GetAIMaxHealth()
    {
        return maxHealth;
    }

    /// <summary>
    /// Returns the AI event hub, creating it if needed.
    /// </summary>
    /// <returns>AI events manager instance.</returns>
    public AIEventsManager GetOrCreateAIEventsManager()
    {
        if (eventsManager == null)
        {
            EnsureEventsManager();
        }

        return eventsManager;
    }

    #endregion

    #region Public Server Configuration

    /// <summary>
    /// Sets the max health value for this AI ship.
    /// </summary>
    /// <param name="value">Max health.</param>
    [ServerOnly]
    public void SetMaxHealth(float value)
    {
        if (value <= 0f)
        {
            return;
        }

        maxHealth = value;
    }

    #endregion
}