using UnityEngine;

namespace OctreePathfinding
{
    /// <summary>
    /// Wraps a collider bounds volume so it can be inserted into an octree for spatial subdivision.
    /// </summary>
    public sealed class OctreeObject
    {
        #region Fields

        private readonly Bounds bounds;

        #endregion

        #region Properties

        public Bounds Bounds => bounds;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates an OctreeObject from a collider bounds.
        /// </summary>
        /// <param name="sourceBounds">World-space bounds of an obstacle collider.</param>
        public OctreeObject(Bounds sourceBounds)
        {
            bounds = sourceBounds;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if this object's bounds intersects a target bounds.
        /// </summary>
        /// <param name="other">Bounds to test.</param>
        /// <returns>True if intersects; otherwise false.</returns>
        public bool Intersects(Bounds other)
        {
            return bounds.Intersects(other);
        }

        #endregion
    }
}