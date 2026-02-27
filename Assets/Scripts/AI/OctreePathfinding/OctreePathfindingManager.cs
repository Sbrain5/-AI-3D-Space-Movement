using System;
using System.Collections.Generic;
using UnityEngine;

namespace OctreePathfinding
{
    /// <summary>
    /// Manages octree-based pathfinding in the scene.
    /// </summary>
    [ExecuteAlways]
    public sealed class OctreePathfindingManager : MonoBehaviour
    {
        #region Nested Types

        private enum OctreeSubdivisionMode
        {
            Adaptive,
            Full
        }

        private struct LeafReservation
        {
            public int teamId;
            public int ownerKey;   // unique per agent
            public float until;
            public int count;      // number of reservations
        }

        #endregion

        #region Singleton

        private static OctreePathfindingManager instance;
        public static OctreePathfindingManager Instance => instance;

        #endregion

        #region Serialized Fields

        [Header("World Bounds")]
        [SerializeField] private BoxCollider worldBoundsSource;

        [Header("Octree Settings")]
        [SerializeField] private float minNodeSize = 25f;

        [Header("Obstacle Collection")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private OctreeSubdivisionMode subdivisionMode = OctreeSubdivisionMode.Adaptive;

        [Header("Mesh Accurate Leaf Validation")]
        [SerializeField] private bool useMeshAccurateLeafValidation = true;
        [SerializeField] private float leafValidationSkin = 0.05f;

        [Header("Movement Safety Queries")]
        [SerializeField] private float movementSafetyMinimumRadius = 0.05f;

        [Header("Agent Clearance (used for endpoint projection)")]
        [SerializeField] private float shipClearanceRadius = 2.0f;

        [Header("Editor Preview")]
        [SerializeField] private bool enableEditorPreview = true;
        [SerializeField] private bool rebuildPreviewNow;
        [SerializeField] private bool drawOctree = true;

        [Header("Safety")]
        [SerializeField] private int maxAllowedLeaves = 200000;

        [Header("Traffic / Path Variety")]
        [SerializeField] private bool enableTrafficAvoidance = true;
        [SerializeField] private bool penalizeSameTeamOnly = true;
        [SerializeField] private float reservedPenalty = 35f;
        [SerializeField] private float reservedDuration = 1.75f;
        [SerializeField] private int reservedPrefixLeaves = 14;
        [SerializeField] private float tieBreakNoise = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool debugPathing = false;

        #endregion

        #region Private Fields

        private OctreeNavigation navigation;
        private bool isBuilt;
        private int lastLeafCount;

        private readonly Collider[] overlapResults = new Collider[64];
        private Collider[] leafValidationHits = new Collider[128];
        private RaycastHit[] movementSweepHits = new RaycastHit[64];

        // Traffic / Reservation Data
        private readonly Dictionary<int, LeafReservation> reservations = new Dictionary<int, LeafReservation>(8192);
        private readonly List<int> reservationRemovals = new List<int>(1024);

        #endregion

        #region Public Properties

        public bool IsBuilt => isBuilt;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }

                return;
            }

            instance = this;
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!isBuilt)
            {
                Build();
            }
        }

        private void OnValidate()
        {
            if (!enableEditorPreview)
            {
                return;
            }

            if (!rebuildPreviewNow)
            {
                return;
            }

            rebuildPreviewNow = false;
            Build();
        }

        private void OnDrawGizmos()
        {
            if (!enableEditorPreview)
            {
                return;
            }

            if (worldBoundsSource != null)
            {
                Bounds b = worldBoundsSource.bounds;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(b.center, b.size);
            }

            if (!drawOctree)
            {
                return;
            }

            if (!isBuilt || navigation == null)
            {
                return;
            }

            OctreeNode root = navigation.Root;
            if (root != null)
            {
                root.DrawGizmos();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds the octree navigation structure based on the current settings and obstacles in the scene.
        /// </summary>
        public void Build()
        {
            isBuilt = false;

            bool useFullSubdivision = subdivisionMode == OctreeSubdivisionMode.Full;
            lastLeafCount = 0;

            reservations.Clear();
            reservationRemovals.Clear();

            if (worldBoundsSource == null)
            {
                return;
            }

            Bounds worldBounds = worldBoundsSource.bounds;

            Collider[] foundColliders = Physics.OverlapBox(worldBounds.center, worldBounds.extents, Quaternion.identity, obstacleMask, triggerInteraction);

            List<Bounds> obstacleBounds = new List<Bounds>(foundColliders.Length);

            float obstacleInflation = Mathf.Max(0f, shipClearanceRadius) * 2f;

            int colliderIndex = 0;
            while (colliderIndex < foundColliders.Length)
            {
                Collider obstacleCollider = foundColliders[colliderIndex];

                if (ShouldUseColliderAsObstacle(obstacleCollider))
                {
                    Bounds colliderBounds = obstacleCollider.bounds;

                    if (obstacleInflation > 0.0001f)
                    {
                        colliderBounds.Expand(obstacleInflation);
                    }

                    obstacleBounds.Add(colliderBounds);
                }

                colliderIndex++;
            }

            float clampedMinNodeSize = Mathf.Max(0.25f, minNodeSize);

            Func<Bounds, bool> exactLeafBlockedCheck = null;
            if (useMeshAccurateLeafValidation)
            {
                exactLeafBlockedCheck = IsLeafBlockedByActualGeometry;
            }

            navigation = new OctreeNavigation(worldBounds, obstacleBounds, clampedMinNodeSize, useFullSubdivision, exactLeafBlockedCheck);

            if (navigation == null || navigation.Graph == null)
            {
                isBuilt = false;
                navigation = null;
                return;
            }

            lastLeafCount = GetLeafCountEstimate();

            if (maxAllowedLeaves > 0 && lastLeafCount > maxAllowedLeaves)
            {
                isBuilt = false;
                navigation = null;
                return;
            }

            isBuilt = true;
        }

        /// <summary>
        /// Attempts to get a path between the start and end positions, returning waypoints if successful.
        /// </summary>
        public bool TryGetPath(Vector3 start, Vector3 end, out List<Vector3> waypoints)
        {
            return TryGetPathInternal(start, end, teamId: 0, ownerKey: 0, useTraffic: false, out waypoints);
        }

        /// <summary>
        /// Attempts to get a path between the start and end positions for an agent, applying traffic penalties to avoid recently used paths.
        /// </summary>
        public bool TryGetPathForAgent(Vector3 start, Vector3 end, int teamId, int ownerKey, out List<Vector3> waypoints)
        {
            return TryGetPathInternal(start, end, teamId, ownerKey, useTraffic: true, out waypoints);
        }

        /// <summary>
        /// Attempts to get a random navigable position within the octree.
        /// </summary>
        public bool TryGetRandomNavigablePosition(out Vector3 position)
        {
            position = Vector3.zero;

            if (!isBuilt || navigation == null)
            {
                return false;
            }

            OctreeNode leaf;
            bool gotLeaf = navigation.Graph.TryGetRandomLeaf(out leaf);
            if (!gotLeaf)
            {
                return false;
            }

            position = leaf.Bounds.center;
            return true;
        }

        /// <summary>
        /// Projects a world position onto the nearest navigable point in the octree.
        /// </summary>
        public bool TryProjectToNavigable(Vector3 worldPosition, out Vector3 projectedPosition)
        {
            projectedPosition = Vector3.zero;

            if (!isBuilt || navigation == null || worldBoundsSource == null)
            {
                return false;
            }

            Vector3 p = ClampPointToWorldBounds(worldPosition);
            p = PushOutOfObstacles(p);

            OctreeNode leaf = navigation.FindClosestLeaf(p);
            if (leaf == null)
            {
                return false;
            }

            projectedPosition = leaf.Bounds.ClosestPoint(p);
            return true;
        }

        /// <summary>
        /// Checks whether a movement step is safe against configured obstacle colliders.
        /// </summary>
        /// <param name="fromPosition">Step start position.</param>
        /// <param name="toPosition">Step end position.</param>
        /// <param name="clearanceRadius">Clearance radius used for sweep and overlap checks.</param>
        /// <param name="selfBody">Agent rigidbody used to ignore self-collisions.</param>
        /// <returns>True if the step is safe; otherwise false.</returns>
        public bool IsMovementStepSafe(Vector3 fromPosition, Vector3 toPosition, float clearanceRadius, Rigidbody selfBody)
        {
            float clampedRadius = Mathf.Max(movementSafetyMinimumRadius, clearanceRadius);

            if (HasBlockingSweepHit(fromPosition, toPosition, clampedRadius, selfBody))
            {
                return false;
            }

            if (HasBlockingOverlapAtPosition(toPosition, clampedRadius, selfBody))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Private Path Method

        /// <summary>
        /// Internal method to get a path with optional traffic penalties.
        /// </summary>
        /// <param name="start"> The start position.</param>
        /// <param name="end"> The end position.</param>
        /// <param name="teamId"> The team ID of the requesting agent.</param>
        /// <param name="ownerKey"> A unique key identifying the requesting agent.</param>
        /// <param name="useTraffic"> Whether to apply traffic penalties.</param>
        /// <param name="waypoints"> The resulting waypoints if a path is found.</param>
        /// <returns> True if a path is found; otherwise, false.</returns>
        private bool TryGetPathInternal(Vector3 start, Vector3 end, int teamId, int ownerKey, bool useTraffic, out List<Vector3> waypoints)
        {
            waypoints = null;

            if (!isBuilt || navigation == null)
            {
                return false;
            }

            Vector3 projectedStart;
            Vector3 projectedEnd;

            if (!TryProjectToNavigable(start, out projectedStart))
            {
                return false;
            }

            if (!TryProjectToNavigable(end, out projectedEnd))
            {
                return false;
            }

            OctreeNode startLeaf = navigation.FindClosestLeaf(projectedStart);
            OctreeNode endLeaf = navigation.FindClosestLeaf(projectedEnd);

            if (startLeaf == null || endLeaf == null)
            {
                return false;
            }

            if (useTraffic && enableTrafficAvoidance)
            {
                CleanupReservations();
            }

            Func<OctreeNode, float> penaltyFunc = null;

            if (useTraffic && enableTrafficAvoidance)
            {
                penaltyFunc = (leaf) => GetEnterPenalty(leaf, teamId, ownerKey);
            }

            // Perform A* search between start and end leaves.
            bool found = penaltyFunc == null
                ? navigation.Graph.AStar(startLeaf, endLeaf, out List<OctreeNode> leafPath)
                : navigation.Graph.AStar(startLeaf, endLeaf, penaltyFunc, out leafPath);

            if (!found)
            {
                return false;
            }

            if (leafPath == null || leafPath.Count == 0)
            {
                return false;
            }

            if (useTraffic && enableTrafficAvoidance)
            {
                ReserveLeafPath(leafPath, teamId, ownerKey);
            }

            // Check if we reached the desired end leaf.
            OctreeNode lastLeaf = leafPath[leafPath.Count - 1];
            bool reachedEndLeaf = lastLeaf == endLeaf;

            Vector3 startPoint = startLeaf.Bounds.ClosestPoint(projectedStart);

            // Use last leaf for endpoint if we didn't reach the desired end leaf.
            Vector3 endPoint = lastLeaf.Bounds.ClosestPoint(projectedEnd);

            List<Vector3> wp = new List<Vector3>(leafPath.Count + 2);
            wp.Add(startPoint);

            int i = 0;
            while (i < leafPath.Count - 1)
            {
                Bounds a = leafPath[i].Bounds;
                Bounds b = leafPath[i + 1].Bounds;

                Vector3 p = a.ClosestPoint(b.center);
                p = b.ClosestPoint(p);

                wp.Add(p);
                i++;
            }

            wp.Add(endPoint);

            waypoints = wp;

            if (debugPathing && !reachedEndLeaf)
            {
            }

            return wp.Count > 1;
        }

        #endregion

        #region Traffic / Variety Helpers

        /// <summary>
        /// Removes expired reservations from the reservation dictionary. 
        /// This should be called periodically to prevent stale reservations from affecting pathfinding.
        /// </summary>
        private void CleanupReservations()
        {
            reservationRemovals.Clear();

            foreach (var kv in reservations)
            {
                if (Time.time > kv.Value.until)
                {
                    reservationRemovals.Add(kv.Key);
                }
            }

            for (int i = 0; i < reservationRemovals.Count; i++)
            {
                reservations.Remove(reservationRemovals[i]);
            }
        }

        /// <summary>
        /// Calculates a penalty for entering a leaf based on current reservations. This encourages pathfinding to avoid recently used paths, 
        /// helping to distribute traffic more evenly across the navigation graph.
        /// </summary>
        /// <param name="leaf"> The octree leaf being evaluated for entry.</param>
        /// <param name="teamId"> The team ID of the agent attempting to enter the leaf.</param>
        /// <param name="ownerKey"> A unique key identifying the agent attempting to enter the leaf.</param>
        /// <returns> A penalty value to be added to the pathfinding cost for entering the leaf.</returns>
        private float GetEnterPenalty(OctreeNode leaf, int teamId, int ownerKey)
        {
            if (leaf == null)
            {
                return 0f;
            }

            int leafId = leaf.Id;

            if (!reservations.TryGetValue(leafId, out LeafReservation r))
            {
                return TieBreakNoise(ownerKey, leafId);
            }

            if (Time.time > r.until)
            {
                return TieBreakNoise(ownerKey, leafId);
            }

            if (penalizeSameTeamOnly && r.teamId != teamId)
            {
                return TieBreakNoise(ownerKey, leafId);
            }

            // Owner gets no penalty.
            if (r.ownerKey == ownerKey)
            {
                return TieBreakNoise(ownerKey, leafId);
            }

            float strength = Mathf.Max(1, r.count);
            return (Mathf.Max(0f, reservedPenalty) * strength) + TieBreakNoise(ownerKey, leafId);
        }

        /// <summary>
        /// Reserves a path of leaves for a given agent, preventing other agents from using the same path for a short duration. 
        /// This is used to implement traffic avoidance, encouraging agents to find alternative routes and reducing congestion on popular paths.
        /// </summary>
        /// <param name="leafPath"> The list of octree leaves that form the path to be reserved.</param>
        /// <param name="teamId"> The team ID of the agent for which the path is being reserved.</param>
        /// <param name="ownerKey"> A unique key identifying the agent for which the path is being reserved.</param>
        private void ReserveLeafPath(List<OctreeNode> leafPath, int teamId, int ownerKey)
        {
            if (leafPath == null || leafPath.Count == 0)
            {
                return;
            }

            float until = Time.time + Mathf.Max(0.1f, reservedDuration);
            int max = Mathf.Clamp(reservedPrefixLeaves, 1, leafPath.Count);

            for (int i = 0; i < max; i++)
            {
                OctreeNode leaf = leafPath[i];
                if (leaf == null) continue;

                int id = leaf.Id;

                if (reservations.TryGetValue(id, out LeafReservation r) && Time.time <= r.until)
                {
                    // Extend existing reservation.
                    r.until = Mathf.Max(r.until, until);
                    r.count = Mathf.Clamp(r.count + 1, 1, 8);
                    reservations[id] = r;
                }
                else
                {
                    reservations[id] = new LeafReservation
                    {
                        teamId = teamId,
                        ownerKey = ownerKey,
                        until = until,
                        count = 1
                    };
                }
            }
        }

        /// <summary>
        /// Generates a small random noise value based on the owner key and leaf ID to be used as a tie-breaker in pathfinding costs. 
        /// This helps to add variability to path selection, preventing agents from always choosing the same path when costs are equal.
        /// </summary>
        /// <param name="ownerKey"> A unique key identifying the agent for which the noise is being generated.</param>
        /// <param name="leafId"> The ID of the octree leaf for which the noise is being generated.</param>
        /// <returns> A small random noise value to be added to pathfinding costs for tie-breaking purposes.</returns>
        private float TieBreakNoise(int ownerKey, int leafId)
        {
            float n = Hash01(ownerKey, leafId);
            return Mathf.Max(0f, tieBreakNoise) * n;
        }

        /// <summary>
        /// Generates a pseudo-random value between 0 and 1 based on two integer inputs. 
        /// This is used to create consistent tie-breaker noise for pathfinding costs based on agent and leaf identifiers.
        /// </summary>
        /// <param name="a"> First integer input, typically the agent's unique key.</param>
        /// <param name="b"> Second integer input, typically the octree leaf's ID.</param>
        /// <returns> A pseudo-random float value between 0 and 1 derived from the inputs.</returns>
        private static float Hash01(int a, int b)
        {
            unchecked
            {
                uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ 0x9E3779B9u;
                x ^= x >> 16;
                x *= 2246822519u;
                x ^= x >> 13;
                x *= 3266489917u;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f; // 0..1
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets an estimate of the number of leaves in the octree.
        /// </summary>
        private int GetLeafCountEstimate()
        {
            if (navigation == null || navigation.Graph == null)
            {
                return 0;
            }

            return navigation.GraphLeafCountEstimate();
        }

        /// <summary>
        /// Clamps a point to be within the defined world bounds.
        /// </summary>
        private Vector3 ClampPointToWorldBounds(Vector3 p)
        {
            Bounds b = worldBoundsSource.bounds;
            return b.ClosestPoint(p);
        }

        /// <summary>
        /// Pushes a point out of any overlapping obstacles based on the ship clearance radius.
        /// </summary>
        /// <param name="p">Point to resolve.</param>
        /// <returns>Resolved point.</returns>
        private Vector3 PushOutOfObstacles(Vector3 p)
        {
            float radius = Mathf.Max(0.01f, shipClearanceRadius);
            int maxIterations = 8;

            int iterationIndex = 0;
            while (iterationIndex < maxIterations)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(p, radius, overlapResults, obstacleMask, triggerInteraction);

                if (hitCount <= 0)
                {
                    return p;
                }

                Vector3 resolveVector = Vector3.zero;
                int validHitCount = 0;

                int hitIndex = 0;
                while (hitIndex < hitCount)
                {
                    Collider hitCollider = overlapResults[hitIndex];

                    if (ShouldUseColliderAsObstacle(hitCollider))
                    {
                        Vector3 closestPoint = hitCollider.ClosestPoint(p);
                        Vector3 awayVector = p - closestPoint;

                        float awayDistance = awayVector.magnitude;
                        if (awayDistance > 0.0001f)
                        {
                            Vector3 awayDirection = awayVector / awayDistance;
                            float pushDistance = radius - awayDistance;

                            if (pushDistance > 0f)
                            {
                                resolveVector += awayDirection * pushDistance;
                                validHitCount++;
                            }
                        }
                        else
                        {
                            resolveVector += Vector3.up * radius;
                            validHitCount++;
                        }
                    }

                    overlapResults[hitIndex] = null;
                    hitIndex++;
                }

                if (validHitCount <= 0)
                {
                    return p;
                }

                p += resolveVector;
                p = ClampPointToWorldBounds(p);

                iterationIndex++;
            }

            return p;
        }

        /// <summary>
        /// Returns whether triggers are included based on the configured query mode.
        /// </summary>
        /// <returns>True when triggers should be treated as query hits.</returns>
        private bool AreTriggersIncludedInQueries()
        {
            if (triggerInteraction == QueryTriggerInteraction.Collide)
            {
                return true;
            }

            if (triggerInteraction == QueryTriggerInteraction.Ignore)
            {
                return false;
            }

            return Physics.queriesHitTriggers;
        }

        /// <summary>
        /// Returns whether a collider should be treated as an obstacle by the manager.
        /// </summary>
        /// <param name="candidateCollider">Collider to test.</param>
        /// <returns>True if it should be treated as an obstacle; otherwise false.</returns>
        private bool ShouldUseColliderAsObstacle(Collider candidateCollider)
        {
            if (candidateCollider == null)
            {
                return false;
            }

            if (!candidateCollider.enabled)
            {
                return false;
            }

            if (candidateCollider.isTrigger && !AreTriggersIncludedInQueries())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether a leaf bounds volume is actually blocked by obstacle geometry using physics overlap.
        /// </summary>
        /// <param name="leafBounds">Leaf bounds to validate.</param>
        /// <returns>True if blocked by actual collider geometry; otherwise false.</returns>
        private bool IsLeafBlockedByActualGeometry(Bounds leafBounds)
        {
            Vector3 halfExtents = leafBounds.extents;
            float skin = Mathf.Max(0f, leafValidationSkin);

            halfExtents.x = Mathf.Max(0.001f, halfExtents.x - skin);
            halfExtents.y = Mathf.Max(0.001f, halfExtents.y - skin);
            halfExtents.z = Mathf.Max(0.001f, halfExtents.z - skin);

            int hitCount = Physics.OverlapBoxNonAlloc(leafBounds.center, halfExtents, leafValidationHits, Quaternion.identity, obstacleMask, triggerInteraction);

            if (hitCount <= 0)
            {
                return false;
            }

            int hitIndex = 0;
            while (hitIndex < hitCount)
            {
                Collider hitCollider = leafValidationHits[hitIndex];
                leafValidationHits[hitIndex] = null;

                if (ShouldUseColliderAsObstacle(hitCollider))
                {
                    return true;
                }

                hitIndex++;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a movement sweep from start to end hits a blocking collider.
        /// </summary>
        /// <param name="fromPosition">Sweep start position.</param>
        /// <param name="toPosition">Sweep end position.</param>
        /// <param name="radius">Sweep radius.</param>
        /// <param name="selfBody">Agent rigidbody used to ignore self-collisions.</param>
        /// <returns>True if a blocking hit is found; otherwise false.</returns>
        private bool HasBlockingSweepHit(Vector3 fromPosition, Vector3 toPosition, float radius, Rigidbody selfBody)
        {
            Vector3 delta = toPosition - fromPosition;
            float distance = delta.magnitude;

            if (distance <= 0.0001f)
            {
                return false;
            }

            Vector3 direction = delta / distance;

            int hitCount = Physics.SphereCastNonAlloc(fromPosition, radius, direction, movementSweepHits, distance, obstacleMask, triggerInteraction);

            if (hitCount <= 0)
            {
                return false;
            }

            int hitIndex = 0;
            while (hitIndex < hitCount)
            {
                RaycastHit hitInfo = movementSweepHits[hitIndex];

                if (ShouldUseColliderAsObstacle(hitInfo.collider) &&
                    !IsSelfCollider(hitInfo.collider, selfBody))
                {
                    return true;
                }

                hitIndex++;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a position overlaps a blocking collider.
        /// </summary>
        /// <param name="position">Position to test.</param>
        /// <param name="radius">Overlap radius.</param>
        /// <param name="selfBody">Agent rigidbody used to ignore self-collisions.</param>
        /// <returns>True if overlapping a blocking collider; otherwise false.</returns>
        private bool HasBlockingOverlapAtPosition(Vector3 position, float radius, Rigidbody selfBody)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(position, radius, overlapResults, obstacleMask, triggerInteraction);

            if (hitCount <= 0)
            {
                return false;
            }

            int hitIndex = 0;
            while (hitIndex < hitCount)
            {
                Collider hitCollider = overlapResults[hitIndex];
                overlapResults[hitIndex] = null;

                if (ShouldUseColliderAsObstacle(hitCollider) &&
                    !IsSelfCollider(hitCollider, selfBody))
                {
                    return true;
                }

                hitIndex++;
            }

            return false;
        }

        /// <summary>
        /// Returns whether a collider belongs to the same agent rigidbody hierarchy.
        /// </summary>
        /// <param name="candidateCollider">Collider to test.</param>
        /// <param name="selfBody">Agent rigidbody.</param>
        /// <returns>True if it belongs to the same agent; otherwise false.</returns>
        private bool IsSelfCollider(Collider candidateCollider, Rigidbody selfBody)
        {
            if (candidateCollider == null || selfBody == null)
            {
                return false;
            }

            if (candidateCollider.attachedRigidbody == selfBody)
            {
                return true;
            }

            return candidateCollider.transform.IsChildOf(selfBody.transform);
        }

        #endregion
    }
}
