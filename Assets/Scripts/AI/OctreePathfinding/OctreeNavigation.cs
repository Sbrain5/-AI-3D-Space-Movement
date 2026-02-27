using System;
using System.Collections.Generic;
using UnityEngine;

namespace OctreePathfinding
{
    /// <summary>
    /// Manages the octree structure and navigation graph for pathfinding.
    /// </summary>
    public sealed class OctreeNavigation
    {
        #region Fields

        private readonly OctreeNode root;
        private readonly Bounds worldBounds;
        private readonly float minNodeSize;

        private readonly List<OctreeNode> emptyLeaves;
        private readonly OctreeGraph graph;
        private readonly Func<Bounds, bool> leafBlockedCheck;

        private readonly float faceEps;
        private readonly float faceKeyStep;

        private readonly float hashCellSize;

        #endregion

        #region Properties

        public OctreeNode Root => root;
        public OctreeGraph Graph => graph;
        public Bounds WorldBounds => worldBounds;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the octree navigation system.
        /// </summary>
        /// <param name="bounds">World bounds for the octree.</param>
        /// <param name="obstacles">List of obstacle bounds to insert into the octree.</param>
        /// <param name="minSize">Minimum size for octree leaf nodes.</param>
        /// <param name="fullSubdivision">If true, builds a full octree; otherwise uses adaptive subdivision.</param>
        /// <param name="meshLeafBlockedCheck">Optional exact leaf occupancy check using physics shapes.</param>
        public OctreeNavigation(Bounds bounds, List<Bounds> obstacles, float minSize, bool fullSubdivision, Func<Bounds, bool> meshLeafBlockedCheck)
        {
            worldBounds = bounds;
            minNodeSize = minSize;
            leafBlockedCheck = meshLeafBlockedCheck;

            faceEps = Mathf.Max(0.02f, minNodeSize * 0.0025f);
            faceKeyStep = faceEps * 2f;
            hashCellSize = Mathf.Max(minNodeSize * 1.25f, minNodeSize + faceEps);

            graph = new OctreeGraph();
            emptyLeaves = new List<OctreeNode>(4096);

            root = new OctreeNode(worldBounds, minNodeSize);

            if (fullSubdivision)
            {
                root.BuildFullTree();

                int obstacleIndex = 0;
                while (obstacleIndex < obstacles.Count)
                {
                    OctreeObject octreeObstacle = new OctreeObject(obstacles[obstacleIndex]);
                    root.InsertIntoFullTree(octreeObstacle);
                    obstacleIndex++;
                }
            }
            else
            {
                int obstacleIndex = 0;
                while (obstacleIndex < obstacles.Count)
                {
                    OctreeObject octreeObstacle = new OctreeObject(obstacles[obstacleIndex]);
                    root.InsertAdaptive(octreeObstacle);
                    obstacleIndex++;
                }
            }

            emptyLeaves.Clear();
            graph.Clear();

            CollectNavigableLeaves(root);

            BuildEdges_FaceNeighborsOnly();
            BuildEdges_AdjacencyHash();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Finds the closest empty leaf node to a given position.
        /// </summary>
        /// <param name="position"></param>
        /// <returns> The closest empty leaf node.</returns>
        public OctreeNode FindClosestLeaf(Vector3 position)
        {
            if (!worldBounds.Contains(position))
            {
                position = worldBounds.ClosestPoint(position);
            }

            OctreeNode leaf = FindClosestLeafRecursive(root, position);
            if (leaf != null && leaf.IsLeaf && leaf.Objects.Count == 0)
            {
                return leaf;
            }

            OctreeNode best = null;
            float bestSqr = float.PositiveInfinity;

            for (int i = 0; i < emptyLeaves.Count; i++)
            {
                OctreeNode l = emptyLeaves[i];
                float sqr = (l.Bounds.center - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = l;
                }
            }

            return best;
        }

        /// <summary>
        /// Estimates the number of leaf nodes in the navigation graph.
        /// </summary>
        /// <returns> Estimated leaf node count.</returns>
        public int GraphLeafCountEstimate()
        {
            if (graph == null) return 0;
            return graph.InternalNodeCountEstimate();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Recursively finds the closest leaf node to a given position.
        /// </summary>
        /// <param name="node"> Current octree node.</param>
        /// <param name="position"> Target position.</param>
        /// <returns> The closest leaf node.</returns>
        private OctreeNode FindClosestLeafRecursive(OctreeNode node, Vector3 position)
        {
            if (node == null) return null;

            if (node.IsLeaf)
            {
                return node;
            }

            OctreeNode[] children = node.Children;
            if (children == null)
            {
                return node;
            }

            for (int i = 0; i < children.Length; i++)
            {
                OctreeNode child = children[i];
                if (child == null) continue;

                if (child.Bounds.Contains(position))
                {
                    return FindClosestLeafRecursive(child, position);
                }
            }

            return node;
        }

        /// <summary>
        /// Collects all navigable leaf nodes in the octree.
        /// </summary>
        /// <param name="node">Current octree node.</param>
        private void CollectNavigableLeaves(OctreeNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsLeaf)
            {
                bool broadPhaseBlocked = node.Objects.Count > 0;
                bool finalBlocked = broadPhaseBlocked;

                if (broadPhaseBlocked && leafBlockedCheck != null)
                {
                    finalBlocked = leafBlockedCheck(node.Bounds);
                }

                if (!finalBlocked)
                {
                    emptyLeaves.Add(node);
                    graph.AddNode(node);
                }

                return;
            }

            OctreeNode[] childNodes = node.Children;
            if (childNodes == null)
            {
                return;
            }

            int childIndex = 0;
            while (childIndex < childNodes.Length)
            {
                CollectNavigableLeaves(childNodes[childIndex]);
                childIndex++;
            }
        }

        /// <summary>
        /// Computes a spatial key for a given float value.
        /// </summary>
        /// <param name="v"> Float value.</param>
        /// <returns> Spatial key.</returns>
        private int Key(float v)
        {
            return Mathf.RoundToInt(v / faceKeyStep);
        }

        /// <summary>
        /// Adds a node to the spatial map under the specified key.
        /// </summary>
        /// <param name="map"> Spatial map.</param>
        /// <param name="key"> Spatial key.</param>
        /// <param name="node"> Octree node to add.</param>
        private static void AddToMap(Dictionary<int, List<OctreeNode>> map, int key, OctreeNode node)
        {
            if (!map.TryGetValue(key, out List<OctreeNode> list))
            {
                list = new List<OctreeNode>(16);
                map.Add(key, list);
            }

            list.Add(node);
        }

        /// <summary>
        /// Checks if two 1D intervals overlap within tolerance.
        /// </summary>
        /// <param name="aMin"> The minimum of interval A.</param>
        /// <param name="aMax"> The maximum of interval A.</param>
        /// <param name="bMin"> The minimum of interval B.</param>
        /// <param name="bMax"> The maximum of interval B.</param>
        /// <returns></returns>
        private bool Overlap1D(float aMin, float aMax, float bMin, float bMax)
        {
            float overlap = Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin);

            // Allow tiny negative overlap due to float precision / bounds noise
            return overlap > -faceEps;
        }

        /// <summary>
        /// First pass connectivity: Connects leaves that share faces only.
        /// </summary>
        private void BuildEdges_FaceNeighborsOnly()
        {
            Dictionary<int, List<OctreeNode>> minX = new Dictionary<int, List<OctreeNode>>(2048);
            Dictionary<int, List<OctreeNode>> maxX = new Dictionary<int, List<OctreeNode>>(2048);

            Dictionary<int, List<OctreeNode>> minY = new Dictionary<int, List<OctreeNode>>(2048);
            Dictionary<int, List<OctreeNode>> maxY = new Dictionary<int, List<OctreeNode>>(2048);

            Dictionary<int, List<OctreeNode>> minZ = new Dictionary<int, List<OctreeNode>>(2048);
            Dictionary<int, List<OctreeNode>> maxZ = new Dictionary<int, List<OctreeNode>>(2048);

            for (int i = 0; i < emptyLeaves.Count; i++)
            {
                OctreeNode n = emptyLeaves[i];
                Bounds b = n.Bounds;

                AddToMap(minX, Key(b.min.x), n);
                AddToMap(maxX, Key(b.max.x), n);

                AddToMap(minY, Key(b.min.y), n);
                AddToMap(maxY, Key(b.max.y), n);

                AddToMap(minZ, Key(b.min.z), n);
                AddToMap(maxZ, Key(b.max.z), n);
            }

            for (int i = 0; i < emptyLeaves.Count; i++)
            {
                OctreeNode a = emptyLeaves[i];
                Bounds ab = a.Bounds;

                // +X
                int k = Key(ab.max.x);
                if (minX.TryGetValue(k, out List<OctreeNode> cand))
                {
                    for (int c = 0; c < cand.Count; c++)
                    {
                        OctreeNode b = cand[c];
                        if (b == a) continue;

                        Bounds bb = b.Bounds;
                        if (Mathf.Abs(ab.max.x - bb.min.x) > faceEps * 2f) continue;

                        if (Overlap1D(ab.min.y, ab.max.y, bb.min.y, bb.max.y) &&
                            Overlap1D(ab.min.z, ab.max.z, bb.min.z, bb.max.z))
                        {
                            graph.AddEdge(a, b);
                        }
                    }
                }

                // -X
                k = Key(ab.min.x);
                if (maxX.TryGetValue(k, out cand))
                {
                    for (int c = 0; c < cand.Count; c++)
                    {
                        OctreeNode b = cand[c];
                        if (b == a) continue;

                        Bounds bb = b.Bounds;
                        if (Mathf.Abs(ab.min.x - bb.max.x) > faceEps * 2f) continue;

                        if (Overlap1D(ab.min.y, ab.max.y, bb.min.y, bb.max.y) &&
                            Overlap1D(ab.min.z, ab.max.z, bb.min.z, bb.max.z))
                        {
                            graph.AddEdge(a, b);
                        }
                    }
                }

                // +Y
                k = Key(ab.max.y);
                if (minY.TryGetValue(k, out cand))
                {
                    for (int c = 0; c < cand.Count; c++)
                    {
                        OctreeNode b = cand[c];
                        if (b == a) continue;

                        Bounds bb = b.Bounds;
                        if (Mathf.Abs(ab.max.y - bb.min.y) > faceEps * 2f) continue;

                        if (Overlap1D(ab.min.x, ab.max.x, bb.min.x, bb.max.x) &&
                            Overlap1D(ab.min.z, ab.max.z, bb.min.z, bb.max.z))
                        {
                            graph.AddEdge(a, b);
                        }
                    }
                }

                // -Y
                k = Key(ab.min.y);
                if (maxY.TryGetValue(k, out cand))
                {
                    for (int c = 0; c < cand.Count; c++)
                    {
                        OctreeNode b = cand[c];
                        if (b == a) continue;

                        Bounds bb = b.Bounds;
                        if (Mathf.Abs(ab.min.y - bb.max.y) > faceEps * 2f) continue;

                        if (Overlap1D(ab.min.x, ab.max.x, bb.min.x, bb.max.x) &&
                            Overlap1D(ab.min.z, ab.max.z, bb.min.z, bb.max.z))
                        {
                            graph.AddEdge(a, b);
                        }
                    }
                }

                // +Z
                k = Key(ab.max.z);
                if (minZ.TryGetValue(k, out cand))
                {
                    for (int c = 0; c < cand.Count; c++)
                    {
                        OctreeNode b = cand[c];
                        if (b == a) continue;

                        Bounds bb = b.Bounds;
                        if (Mathf.Abs(ab.max.z - bb.min.z) > faceEps * 2f) continue;

                        if (Overlap1D(ab.min.x, ab.max.x, bb.min.x, bb.max.x) &&
                            Overlap1D(ab.min.y, ab.max.y, bb.min.y, bb.max.y))
                        {
                            graph.AddEdge(a, b);
                        }
                    }
                }

                // -Z
                k = Key(ab.min.z);
                if (maxZ.TryGetValue(k, out cand))
                {
                    for (int c = 0; c < cand.Count; c++)
                    {
                        OctreeNode b = cand[c];
                        if (b == a) continue;

                        Bounds bb = b.Bounds;
                        if (Mathf.Abs(ab.min.z - bb.max.z) > faceEps * 2f) continue;

                        if (Overlap1D(ab.min.x, ab.max.x, bb.min.x, bb.max.x) &&
                            Overlap1D(ab.min.y, ab.max.y, bb.min.y, bb.max.y))
                        {
                            graph.AddEdge(a, b);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Integer 3D vector for spatial hashing.
        /// </summary>
        private struct Int3
        {
            public int x, y, z;
            public Int3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        }

        /// <summary>
        /// Computes a hash key for a given Int3 coordinate.
        /// </summary>
        /// <param name="c"> Int3 coordinate.</param>
        /// <returns> Hash key.</returns>
        private int HashKey(Int3 c)
        {
            unchecked
            {
                int h = 17;
                h = (h * 31) ^ c.x;
                h = (h * 31) ^ c.y;
                h = (h * 31) ^ c.z;
                return h;
            }
        }

        /// <summary>
        /// Computes the cell coordinate for a given position.
        /// </summary>
        /// <param name="p"> Position.</param>
        /// <returns> Cell coordinate.</returns>
        private Int3 CellOf(Vector3 p)
        {
            float inv = 1f / hashCellSize;
            return new Int3(Mathf.FloorToInt(p.x * inv), Mathf.FloorToInt(p.y * inv), Mathf.FloorToInt(p.z * inv));
        }

        /// <summary>
        /// Checks if two bounds are adjacent within tolerance.
        /// </summary>
        /// <param name="a"> The first bounds.</param>
        /// <param name="b"> The second bounds.</param>
        /// <returns></returns>
        private bool AreAdjacent(Bounds a, Bounds b)
        {
            // Compute gaps on each axis
            float gapX = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float gapY = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
            float gapZ = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));

            if (gapX > faceEps || gapY > faceEps || gapZ > faceEps)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Second pass connectivity: Connects leaves that are adjacent via edges or corners using spatial hashing.
        /// </summary>
        private void BuildEdges_AdjacencyHash()
        {
            // Build spatial hash buckets
            Dictionary<int, List<OctreeNode>> buckets = new Dictionary<int, List<OctreeNode>>(4096);

            for (int i = 0; i < emptyLeaves.Count; i++)
            {
                OctreeNode n = emptyLeaves[i];
                Int3 cell = CellOf(n.Bounds.center);
                int hk = HashKey(cell);

                if (!buckets.TryGetValue(hk, out List<OctreeNode> list))
                {
                    list = new List<OctreeNode>(16);
                    buckets.Add(hk, list);
                }

                list.Add(n);
            }

            // Connect adjacent nodes
            for (int i = 0; i < emptyLeaves.Count; i++)
            {
                OctreeNode a = emptyLeaves[i];
                Bounds ab = a.Bounds;

                Int3 baseCell = CellOf(ab.center);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Int3 c = new Int3(baseCell.x + dx, baseCell.y + dy, baseCell.z + dz);
                            int hk = HashKey(c);

                            if (!buckets.TryGetValue(hk, out List<OctreeNode> cand))
                            {
                                continue;
                            }

                            for (int j = 0; j < cand.Count; j++)
                            {
                                OctreeNode b = cand[j];
                                if (b == a) continue;

                                Bounds bb = b.Bounds;

                                // Check adjacency
                                if (AreAdjacent(ab, bb))
                                {
                                    graph.AddEdge(a, b);
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}