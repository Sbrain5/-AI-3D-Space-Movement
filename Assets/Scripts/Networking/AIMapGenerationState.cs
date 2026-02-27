using OctreePathfinding;
using PurrNet.StateMachine;
using UnityEngine;

/// <summary>
/// State responsible for procedurally generating the asteroid field and octree pathfinding data on the server before gameplay starts.
/// </summary>
public sealed class AIMapGenerationState : StateNode
{
    #region Serialized Fields

    [Header("Octree")]
    [SerializeField] private GameObject octreePathfindingManagerPrefab;

    [Header("Hierarchy")]
    [SerializeField] private string managersRootName = "Managers";
    [SerializeField] private string environmentRootName = "Environment";

    [Header("Asteroid Generation")]
    [SerializeField] private GameObject[] asteroidPrefabs;
    [SerializeField] private int asteroidCount = 150;
    [SerializeField] private Vector2 asteroidUniformScaleRange = new Vector2(1f, 1f);
    [SerializeField] private float placementClearanceRadius = 0f;
    [SerializeField] private LayerMask placementMask = ~0;

    #endregion

    #region Runtime State

    private bool hasExecuted;

    #endregion

    #region State Lifecycle

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        if (!asServer)
        {
            return;
        }

        if (hasExecuted)
        {
            machine.Next();
            return;
        }

        hasExecuted = true;

        int seed = Random.Range(0, 1000000);

        OctreePathfindingManager manager = EnsureOctreeManagerUnderManagersRoot();
        if (manager != null)
        {
            SpawnAsteroidsInManagerBounds(manager, seed);
            Physics.SyncTransforms();
            manager.Build();
        }

        machine.Next();
    }

    #endregion

    #region Manager Setup

    /// <summary>
    /// Ensures an OctreePathfindingManager exists in the scene, instantiating the prefab as a child of "Managers" if needed.
    /// </summary>
    /// <returns>Resolved OctreePathfindingManager or null if none can be created.</returns>
    private OctreePathfindingManager EnsureOctreeManagerUnderManagersRoot()
    {
        OctreePathfindingManager manager = FindFirstObjectByType<OctreePathfindingManager>();
        if (manager != null)
        {
            return manager;
        }

        if (octreePathfindingManagerPrefab == null)
        {
            return null;
        }

        Transform parent = GetOrCreateManagersRoot();
        GameObject instance = Instantiate(octreePathfindingManagerPrefab, parent);
        if (instance == null)
        {
            return null;
        }

        return instance.GetComponent<OctreePathfindingManager>();
    }

    /// <summary>
    /// Finds the "Managers" object or creates it if missing.
    /// </summary>
    /// <returns>Managers root transform.</returns>
    private Transform GetOrCreateManagersRoot()
    {
        GameObject existing = GameObject.Find(managersRootName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject created = new GameObject(managersRootName);
        return created.transform;
    }

    /// <summary>
    /// Finds the "Environment" object or creates it if missing.
    /// </summary>
    /// <returns>Environment root transform.</returns>
    private Transform GetOrCreateEnvironmentRoot()
    {
        GameObject existing = GameObject.Find(environmentRootName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject created = new GameObject(environmentRootName);
        return created.transform;
    }

    #endregion

    #region Asteroid Spawning

    /// <summary>
    /// Spawns asteroids inside the BoxCollider bounds found on the manager hierarchy.
    /// </summary>
    /// <param name="manager">Octree manager used to locate the spawn bounds.</param>
    /// <param name="seed">Deterministic seed for placement.</param>
    private void SpawnAsteroidsInManagerBounds(OctreePathfindingManager manager, int seed)
    {
        if (asteroidPrefabs == null || asteroidPrefabs.Length == 0)
        {
            return;
        }

        if (asteroidCount <= 0)
        {
            return;
        }

        BoxCollider boundsCollider = GetBoundsCollider(manager);
        if (boundsCollider == null)
        {
            return;
        }

        Transform container = GetOrCreateAsteroidContainerUnderEnvironment();
        ClearExistingAsteroids(container);

        System.Random rng = new System.Random(seed);

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(asteroidCount * 6, 100);

        while (spawned < asteroidCount && attempts < maxAttempts)
        {
            attempts++;

            GameObject prefab = PickPrefab(rng);
            if (prefab == null)
            {
                continue;
            }

            Vector3 position = GetRandomPointInside(boundsCollider, rng);
            if (!IsPlacementValid(position))
            {
                continue;
            }

            Quaternion rotation = GetRandomRotation(rng);
            GameObject asteroid = Instantiate(prefab, position, rotation, container);

            float scale = GetRandomUniformScale(rng);
            asteroid.transform.localScale = asteroid.transform.localScale * scale;

            spawned++;
        }
    }

    /// <summary>
    /// Finds the BoxCollider used as the asteroid spawn volume under the manager.
    /// </summary>
    /// <param name="manager">Manager root transform.</param>
    /// <returns>BoxCollider if found, otherwise null.</returns>
    private BoxCollider GetBoundsCollider(OctreePathfindingManager manager)
    {
        if (manager == null)
        {
            return null;
        }

        return manager.GetComponentInChildren<BoxCollider>(true);
    }

    /// <summary>
    /// Gets or creates a stable container for runtime asteroids under "Environment".
    /// </summary>
    /// <returns>Asteroid container transform.</returns>
    private Transform GetOrCreateAsteroidContainerUnderEnvironment()
    {
        Transform environmentRoot = GetOrCreateEnvironmentRoot();

        Transform existing = environmentRoot.Find("Asteroids");
        if (existing != null)
        {
            return existing;
        }

        GameObject container = new GameObject("Asteroids");
        container.transform.SetParent(environmentRoot, false);
        return container.transform;
    }

    /// <summary>
    /// Destroys children under the container to avoid duplicating asteroids.
    /// </summary>
    /// <param name="container">Asteroid container transform.</param>
    private void ClearExistingAsteroids(Transform container)
    {
        if (container == null)
        {
            return;
        }

        int index = container.childCount - 1;
        while (index >= 0)
        {
            Transform child = container.GetChild(index);
            if (child != null)
            {
                Destroy(child.gameObject);
            }

            index--;
        }
    }

    /// <summary>
    /// Picks a random prefab from the configured array.
    /// </summary>
    /// <param name="rng">Random source.</param>
    /// <returns>Prefab reference or null.</returns>
    private GameObject PickPrefab(System.Random rng)
    {
        int index = rng.Next(0, asteroidPrefabs.Length);
        return asteroidPrefabs[index];
    }

    /// <summary>
    /// Returns a random point inside the BoxCollider volume.
    /// </summary>
    /// <param name="box">BoxCollider defining the volume.</param>
    /// <param name="rng">Random source.</param>
    /// <returns>World space position.</returns>
    private Vector3 GetRandomPointInside(BoxCollider box, System.Random rng)
    {
        Vector3 size = box.size;
        Vector3 center = box.center;

        float x = (float)rng.NextDouble() * size.x - (size.x * 0.5f);
        float y = (float)rng.NextDouble() * size.y - (size.y * 0.5f);
        float z = (float)rng.NextDouble() * size.z - (size.z * 0.5f);

        Vector3 localPoint = center + new Vector3(x, y, z);
        return box.transform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Returns a random rotation.
    /// </summary>
    /// <param name="rng">Random source.</param>
    /// <returns>Rotation quaternion.</returns>
    private Quaternion GetRandomRotation(System.Random rng)
    {
        float yaw = (float)rng.NextDouble() * 360f;
        float pitch = (float)rng.NextDouble() * 360f;
        float roll = (float)rng.NextDouble() * 360f;

        return Quaternion.Euler(pitch, yaw, roll);
    }

    /// <summary>
    /// Returns a random uniform scale factor based on the configured range.
    /// </summary>
    /// <param name="rng">Random source.</param>
    /// <returns>Uniform scale multiplier.</returns>
    private float GetRandomUniformScale(System.Random rng)
    {
        float min = Mathf.Min(asteroidUniformScaleRange.x, asteroidUniformScaleRange.y);
        float max = Mathf.Max(asteroidUniformScaleRange.x, asteroidUniformScaleRange.y);

        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        float t = (float)rng.NextDouble();
        return Mathf.Lerp(min, max, t);
    }

    /// <summary>
    /// Validates whether an asteroid can be placed at the given position.
    /// </summary>
    /// <param name="position">World position.</param>
    /// <returns>True if placement is valid.</returns>
    private bool IsPlacementValid(Vector3 position)
    {
        if (placementClearanceRadius <= 0f)
        {
            return true;
        }

        return !Physics.CheckSphere(position, placementClearanceRadius, placementMask, QueryTriggerInteraction.Ignore);
    }

    #endregion
}