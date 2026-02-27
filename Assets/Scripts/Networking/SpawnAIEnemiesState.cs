using System.Collections.Generic;
using PurrNet.StateMachine;
using UnityEngine;

/// <summary>
/// State node responsible for spawning AI ships on the server at the start of a match. 
/// It uses predefined spawn points and checks for nearby colliders to avoid spawning inside obstacles or players.
/// </summary>
public sealed class SpawnAIEnemiesState : StateNode
{
    #region Serialized Fields

    [Header("AI Settings")]
    [SerializeField] private AIShipHolder aiShipPrefab;
    [SerializeField] private int aiShipsToSpawn = 10;
    [SerializeField] private float safetyCheckRadius = 8f;
    [SerializeField] private LayerMask spawnBlockingMask = ~0;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> aiSpawnPoints = new List<Transform>();

    #endregion

    #region Runtime State

    private bool hasExecuted;
    private readonly Collider[] spawnCheckResults = new Collider[128];

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

        SpawnFromPointsOnly();
        machine.Next();
    }

    #endregion

    #region Spawning

    /// <summary>
    /// Spawns AI ships from predefined spawn points only. Points that are blocked are skipped.
    /// </summary>
    private void SpawnFromPointsOnly()
    {
        if (aiShipPrefab == null)
        {
            return;
        }

        if (aiSpawnPoints == null || aiSpawnPoints.Count == 0)
        {
            return;
        }

        int targetAiCount = Mathf.Max(0, aiShipsToSpawn);
        if (targetAiCount <= 0)
        {
            return;
        }

        List<Transform> shuffledPoints = BuildShuffledSpawnList(aiSpawnPoints);

        int spawnedCount = 0;
        int pointIndex = 0;

        while (spawnedCount < targetAiCount && pointIndex < shuffledPoints.Count)
        {
            Transform spawnPoint = shuffledPoints[pointIndex];
            pointIndex++;

            if (spawnPoint == null)
            {
                continue;
            }

            Vector3 spawnPosition = spawnPoint.position;
            if (IsSpawnBlocked(spawnPosition))
            {
                continue;
            }

            AIShipHolder spawnedShip = Instantiate(aiShipPrefab, spawnPosition, spawnPoint.rotation);
            if (spawnedShip == null)
            {
                continue;
            }

            string shipName = "AI_Ship_" + (spawnedCount + 1);
            spawnedShip.SetNetworkedShipName(shipName);

            spawnedCount++;
        }
    }

    /// <summary>
    /// Returns a shuffled copy of the input spawn points list.
    /// </summary>
    /// <param name="sourcePoints">Source list.</param>
    /// <returns>Shuffled list.</returns>
    private List<Transform> BuildShuffledSpawnList(List<Transform> sourcePoints)
    {
        List<Transform> shuffledPoints = new List<Transform>(sourcePoints);

        int index = 0;
        while (index < shuffledPoints.Count)
        {
            int randomIndex = Random.Range(index, shuffledPoints.Count);

            Transform cachedPoint = shuffledPoints[index];
            shuffledPoints[index] = shuffledPoints[randomIndex];
            shuffledPoints[randomIndex] = cachedPoint;

            index++;
        }

        return shuffledPoints;
    }

    /// <summary>
    /// Checks whether a spawn position is blocked by a relevant collider.
    /// Asteroids are ignored so they do not reduce the intended AI spawn count.
    /// </summary>
    /// <param name="position">World position to test.</param>
    /// <returns>True if blocked, otherwise false.</returns>
    private bool IsSpawnBlocked(Vector3 position)
    {
        float radius = Mathf.Max(0.1f, safetyCheckRadius);

        int hitCount = Physics.OverlapSphereNonAlloc(position, radius, spawnCheckResults, spawnBlockingMask, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return false;
        }

        int index = 0;
        while (index < hitCount)
        {
            Collider hitCollider = spawnCheckResults[index];
            spawnCheckResults[index] = null;

            if (ShouldColliderBlockSpawn(hitCollider))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a collider should block an AI spawn point.
    /// </summary>
    /// <param name="hitCollider">Collider found in the spawn overlap check.</param>
    /// <returns>True if it should block spawning, otherwise false.</returns>
    private bool ShouldColliderBlockSpawn(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        if (!hitCollider.enabled)
        {
            return false;
        }

        if (hitCollider.isTrigger)
        {
            return false;
        }

        AsteroidHolder asteroid = hitCollider.GetComponentInParent<AsteroidHolder>();
        if (asteroid != null)
        {
            return false;
        }

        return true;
    }

    #endregion
}