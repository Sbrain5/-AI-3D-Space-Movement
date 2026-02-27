using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OctreePathfinding
{
    /// <summary>
    /// Graph structure for pathfinding over octree nodes using A* algorithm.
    /// </summary>
    public sealed class OctreeGraph
    {
        #region Nested Types

        /// <summary>
        /// Represents a node in the pathfinding graph.
        /// </summary>
        private sealed class Node
        {
            private static int nextId;

            private readonly int id;
            private readonly OctreeNode octreeNode;

            private float f;
            private float g;
            private float h;
            private Node from;

            private readonly List<Edge> edges;

            private int lastSearchId;

            public int Id => id;
            public OctreeNode OctreeNode => octreeNode;
            public float F { get => f; set => f = value; }
            public float G { get => g; set => g = value; }
            public float H { get => h; set => h = value; }
            public Node From { get => from; set => from = value; }
            public List<Edge> Edges => edges;
            public int LastSearchId { get => lastSearchId; set => lastSearchId = value; }

            /// <summary>
            /// Creates a new pathfinding node for the given octree node.
            /// </summary>
            /// <param name="node"> The octree node this graph node represents.</param>
            public Node(OctreeNode node)
            {
                id = nextId++;
                octreeNode = node;

                f = float.PositiveInfinity;
                g = float.PositiveInfinity;
                h = 0f;
                from = null;

                edges = new List<Edge>(16);
                lastSearchId = -1;
            }

            /// <summary>
            /// Resets the node's pathfinding data for a new search.
            /// </summary>
            /// <param name="searchId"> The current search identifier.</param>
            public void ResetForSearch(int searchId)
            {
                lastSearchId = searchId;
                f = float.PositiveInfinity;
                g = float.PositiveInfinity;
                h = 0f;
                from = null;
            }
        }

        /// <summary>
        /// Represents an edge connecting two nodes in the graph.
        /// </summary>
        private sealed class Edge
        {
            private readonly Node a;
            private readonly Node b;

            public Node A => a;
            public Node B => b;

            public Edge(Node nodeA, Node nodeB)
            {
                a = nodeA;
                b = nodeB;
            }
        }

        /// <summary>
        /// Comparer for ordering nodes in the open set based on their F cost.
        /// </summary>
        private sealed class NodeComparer : IComparer<Node>
        {
            public int Compare(Node x, Node y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                int compare = x.F.CompareTo(y.F);
                if (compare == 0)
                {
                    return x.Id.CompareTo(y.Id);
                }

                return compare;
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<OctreeNode, Node> nodes;
        private readonly HashSet<(int, int)> edgeKeySet;

        private readonly List<OctreeNode> lastPath;

        private const int maxIterations = 350000;

        private int searchId;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the OctreeGraph class.
        /// </summary>
        public OctreeGraph()
        {
            nodes = new Dictionary<OctreeNode, Node>(4096);
            edgeKeySet = new HashSet<(int, int)>();
            lastPath = new List<OctreeNode>(256);
            searchId = 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clears all nodes and edges from the graph.
        /// </summary>
        public void Clear()
        {
            nodes.Clear();
            edgeKeySet.Clear();
            lastPath.Clear();
        }

        /// <summary>
        /// Adds an octree node to the graph.
        /// </summary>
        /// <param name="octreeNode"> The octree node to add.</param>
        public void AddNode(OctreeNode octreeNode)
        {
            if (octreeNode == null) return;

            if (!nodes.ContainsKey(octreeNode))
            {
                nodes.Add(octreeNode, new Node(octreeNode));
            }
        }

        /// <summary>
        /// Adds an edge between two octree nodes in the graph.
        /// </summary>
        /// <param name="a"> The first octree node.</param>
        /// <param name="b"> The second octree node.</param>
        public void AddEdge(OctreeNode a, OctreeNode b)
        {
            if (a == null || b == null) return;
            if (a == b) return;

            if (!nodes.TryGetValue(a, out Node nodeA)) return;
            if (!nodes.TryGetValue(b, out Node nodeB)) return;

            int idA = nodeA.Id;
            int idB = nodeB.Id;
            (int, int) key = idA < idB ? (idA, idB) : (idB, idA);

            if (edgeKeySet.Contains(key))
            {
                return;
            }

            edgeKeySet.Add(key);

            Edge edge = new Edge(nodeA, nodeB);
            nodeA.Edges.Add(edge);
            nodeB.Edges.Add(edge);
        }

        /// <summary>
        /// Finds a path between two octree nodes using the A* algorithm.
        /// </summary>
        /// <param name="startLeaf"> The starting octree node.</param>
        /// <param name="endLeaf"> The target octree node.</param>
        /// <param name="path"> The resulting path as a list of octree nodes.</param>
        /// <returns> True if a path was found; otherwise, false.</returns>
        public bool AStar(OctreeNode startLeaf, OctreeNode endLeaf, out List<OctreeNode> path)
        {
            return AStar(startLeaf, endLeaf, null, out path);
        }

        /// <summary>
        /// Finds a path between two octree nodes using the A* algorithm.
        /// </summary>
        /// <param name="startLeaf"> The starting octree node.</param>
        /// <param name="endLeaf"> The target octree node.</param>
        /// <param name="path"> The resulting path as a list of octree nodes.</param>
        /// <returns> True if a path was found; otherwise, false.</returns>
        public bool AStar(OctreeNode startLeaf, OctreeNode endLeaf, Func<OctreeNode, float> enterPenalty, out List<OctreeNode> path)
        {
            lastPath.Clear();
            path = lastPath;

            if (startLeaf == null || endLeaf == null) return false;
            if (!nodes.TryGetValue(startLeaf, out Node start)) return false;
            if (!nodes.TryGetValue(endLeaf, out Node end)) return false;

            searchId++;
            if (searchId == int.MaxValue) searchId = 1;

            SortedSet<Node> openSet = new SortedSet<Node>(new NodeComparer());
            HashSet<int> closedSet = new HashSet<int>();

            PrepareNode(start);
            PrepareNode(end);

            start.G = 0f;
            start.H = Heuristic(start, end);
            start.F = start.G + start.H;
            start.From = null;

            openSet.Add(start);

            Node bestSoFar = start;
            float bestSoFarH = start.H;

            int iterations = 0;

            while (openSet.Count > 0)
            {
                iterations++;
                if (iterations > maxIterations) break;

                Node current = openSet.Min;
                openSet.Remove(current);

                if (current.Id == end.Id)
                {
                    ReconstructPath(current, lastPath);
                    return lastPath.Count > 0;
                }

                closedSet.Add(current.Id);

                if (current.H < bestSoFarH || (Mathf.Approximately(current.H, bestSoFarH) && current.F < bestSoFar.F))
                {
                    bestSoFar = current;
                    bestSoFarH = current.H;
                }

                for (int i = 0; i < current.Edges.Count; i++)
                {
                    Edge edge = current.Edges[i];
                    Node neighbor = edge.A.Id == current.Id ? edge.B : edge.A;

                    PrepareNode(neighbor);

                    if (closedSet.Contains(neighbor.Id)) continue;

                    float penalty = 0f;
                    if (enterPenalty != null)
                    {
                        penalty = Mathf.Max(0f, enterPenalty(neighbor.OctreeNode));
                    }

                    float tentativeG = current.G + Cost(current, neighbor) + penalty;

                    bool inOpen = openSet.Contains(neighbor);

                    if (!inOpen || tentativeG < neighbor.G)
                    {
                        if (inOpen) openSet.Remove(neighbor);

                        neighbor.From = current;
                        neighbor.G = tentativeG;
                        neighbor.H = Heuristic(neighbor, end);
                        neighbor.F = neighbor.G + neighbor.H;

                        openSet.Add(neighbor);

                        if (neighbor.H < bestSoFarH || (Mathf.Approximately(neighbor.H, bestSoFarH) && neighbor.F < bestSoFar.F))
                        {
                            bestSoFar = neighbor;
                            bestSoFarH = neighbor.H;
                        }
                    }
                }
            }

            if (bestSoFar != null)
            {
                ReconstructPath(bestSoFar, lastPath);
                return lastPath.Count > 0;
            }

            return false;
        }

        /// <summary>
        /// Attempts to get a random octree leaf node from the graph.
        /// </summary>
        /// <param name="leaf"> The randomly selected octree leaf node.</param>
        /// <returns> True if a leaf node was found; otherwise, false.</returns>
        public bool TryGetRandomLeaf(out OctreeNode leaf)
        {
            leaf = null;

            if (nodes.Count == 0)
            {
                return false;
            }

            int index = UnityEngine.Random.Range(0, nodes.Count);
            leaf = nodes.Keys.ElementAt(index);
            return leaf != null;
        }

        /// <summary>
        /// Estimates the number of internal nodes in the graph.
        /// </summary>
        /// <returns> The estimated count of internal nodes.</returns>
        public int InternalNodeCountEstimate()
        {
            return nodes.Count;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Prepares a node for the current search by resetting its data if needed.
        /// </summary>
        /// <param name="n"> The node to prepare.</param>
        private void PrepareNode(Node n)
        {
            if (n.LastSearchId != searchId)
            {
                n.ResetForSearch(searchId);
            }
        }

        /// <summary>
        /// Reconstructs the path from the current node back to the start node.
        /// </summary>
        /// <param name="current"> The current node at the end of the path.</param>
        /// <param name="pathOut"> The output list to store the reconstructed path.</param>
        private void ReconstructPath(Node current, List<OctreeNode> pathOut)
        {
            Node it = current;

            while (it != null)
            {
                pathOut.Add(it.OctreeNode);
                it = it.From;
            }

            pathOut.Reverse();
        }

        /// <summary>
        /// Calculates the heuristic cost between two nodes.
        /// </summary>
        /// <param name="a"> The first node.</param>
        /// <param name="b"> The second node.</param>
        /// <returns> The heuristic cost.</returns>
        private float Heuristic(Node a, Node b)
        {
            return Vector3.Distance(a.OctreeNode.Bounds.center, b.OctreeNode.Bounds.center);
        }

        /// <summary>
        /// Calculates the cost between two nodes.
        /// </summary>
        /// <param name="a"> The first node.</param>
        /// <param name="b"> The second node.</param>
        /// <returns> The cost.</returns>
        private float Cost(Node a, Node b)
        {
            return Vector3.Distance(a.OctreeNode.Bounds.center, b.OctreeNode.Bounds.center);
        }

        #endregion
    }
}