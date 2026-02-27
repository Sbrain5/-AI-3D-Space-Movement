using System.Collections.Generic;
using UnityEngine;

namespace OctreePathfinding
{
    /// <summary>
    /// Represents a node in the octree structure.
    /// </summary>
    public sealed class OctreeNode
    {
        #region Static

        private static int nextId;

        #endregion

        #region Fields

        private readonly List<OctreeObject> objects;
        private readonly Bounds bounds;
        private readonly Bounds[] childBounds;
        private readonly float minNodeSize;

        private OctreeNode[] children;
        private readonly int id;

        #endregion

        #region Properties

        public int Id => id;
        public Bounds Bounds => bounds;
        public bool IsLeaf => children == null;
        public OctreeNode[] Children => children;
        public IReadOnlyList<OctreeObject> Objects => objects;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates an octree node with specified bounds and minimum leaf size.
        /// </summary>
        /// <param name="nodeBounds">Node bounds.</param>
        /// <param name="minSize">Minimum leaf size.</param>
        public OctreeNode(Bounds nodeBounds, float minSize)
        {
            id = nextId++;
            bounds = nodeBounds;
            minNodeSize = minSize;

            objects = new List<OctreeObject>(8);
            childBounds = new Bounds[8];

            Vector3 newSize = nodeBounds.size * 0.5f;
            Vector3 centerOffset = nodeBounds.size * 0.25f;
            Vector3 parentCenter = nodeBounds.center;

            for (int i = 0; i < 8; i++)
            {
                Vector3 childCenter = parentCenter;
                childCenter.x += centerOffset.x * ((i & 1) == 0 ? -1f : 1f);
                childCenter.y += centerOffset.y * ((i & 2) == 0 ? -1f : 1f);
                childCenter.z += centerOffset.z * ((i & 4) == 0 ? -1f : 1f);
                childBounds[i] = new Bounds(childCenter, newSize);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Fully subdivides this node down to minNodeSize across the whole volume.
        /// </summary>
        public void BuildFullTree()
        {
            BuildFullTreeInternal();
        }

        /// <summary>
        /// Inserts an obstacle in adaptive mode (subdivide only where needed).
        /// </summary>
        /// <param name="octreeObject">Obstacle wrapper.</param>
        public void InsertAdaptive(OctreeObject octreeObject)
        {
            InsertAdaptiveInternal(octreeObject);
        }

        /// <summary>
        /// Inserts an obstacle in full-tree mode (tree already built). Marks intersecting leaves as blocked.
        /// </summary>
        /// <param name="octreeObject">Obstacle wrapper.</param>
        public void InsertIntoFullTree(OctreeObject octreeObject)
        {
            InsertIntoFullTreeInternal(octreeObject);
        }

        /// <summary>
        /// Draws this node and all children.
        /// </summary>
        public void DrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (children == null)
            {
                return;
            }

            for (int i = 0; i < children.Length; i++)
            {
                OctreeNode child = children[i];
                if (child != null)
                {
                    child.DrawGizmos();
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Recursively subdivides the entire space uniformly until leaf size is reached.
        /// </summary>
        private void BuildFullTreeInternal()
        {
            if (bounds.size.x <= minNodeSize && bounds.size.y <= minNodeSize && bounds.size.z <= minNodeSize)
            {
                return;
            }

            if (children == null)
            {
                children = new OctreeNode[8];
            }

            for (int i = 0; i < 8; i++)
            {
                if (children[i] == null)
                {
                    children[i] = new OctreeNode(childBounds[i], minNodeSize);
                }

                children[i].BuildFullTreeInternal();
            }
        }

        /// <summary>
        /// Adaptive insertion: subdivides only where needed.
        /// </summary>
        /// <param name="octreeObject"> Obstacle wrapper.</param>
        private void InsertAdaptiveInternal(OctreeObject octreeObject)
        {
            if (!octreeObject.Intersects(bounds))
            {
                return;
            }

            // Leaf reached: mark this leaf as blocked by storing the obstacle.
            if (bounds.size.x <= minNodeSize && bounds.size.y <= minNodeSize && bounds.size.z <= minNodeSize)
            {
                objects.Add(octreeObject);
                return;
            }

            if (children == null)
            {
                children = new OctreeNode[8];

                for (int i = 0; i < 8; i++)
                {
                    children[i] = new OctreeNode(childBounds[i], minNodeSize);
                }
            }

            bool intersectedAnyChild = false;

            // Recurse only into intersected children.
            for (int i = 0; i < 8; i++)
            {
                if (octreeObject.Intersects(childBounds[i]))
                {
                    children[i].InsertAdaptiveInternal(octreeObject);
                    intersectedAnyChild = true;
                }
            }

            // If no children were intersected, store in this node.
            if (!intersectedAnyChild)
            {
                objects.Add(octreeObject);
            }
        }

        /// <summary>
        /// Full-tree insertion: marks all intersected leaves.
        /// </summary>
        /// <param name="octreeObject">Obstacle wrapper.</param>
        private void InsertIntoFullTreeInternal(OctreeObject octreeObject)
        {
            if (!octreeObject.Intersects(bounds))
            {
                return;
            }

            if (children == null)
            {
                objects.Add(octreeObject);
                return;
            }

            for (int i = 0; i < children.Length; i++)
            {
                OctreeNode child = children[i];
                if (child != null)
                {
                    child.InsertIntoFullTreeInternal(octreeObject);
                }
            }
        }

        #endregion
    }
}