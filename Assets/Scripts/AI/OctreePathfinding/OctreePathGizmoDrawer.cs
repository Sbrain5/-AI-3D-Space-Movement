using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws gizmos in the Unity Editor to visualize the path of an OctreeMoverAgent.
/// </summary>
[ExecuteAlways]
public sealed class OctreePathGizmoDrawer : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [SerializeField] private OctreeMoverAgent mover;

    [Header("Visibility")]
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawInPlayMode = true;
    [SerializeField] private bool drawInEditMode = true;

    [Header("Path Styling")]
    [SerializeField] private float pointRadius = 0.35f;
    [SerializeField] private float arrowHeadLength = 1.0f;
    [SerializeField] private float arrowHeadAngle = 22.5f;
    [SerializeField] private float maxArrowCount = 512f;

    private List<Vector3> cachedWaypoints;
    private int cachedIndex;
    private bool hasCache;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Validates and initializes references when the script is loaded or a value is changed in the inspector.
    /// </summary>
    private void OnValidate()
    {
        if (mover == null)
        {
            mover = GetComponent<OctreeMoverAgent>();
        }
    }

    /// <summary>
    /// Draws gizmos in the editor.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!ShouldDraw(false)) return;
        DrawPathGizmos();
    }

    /// <summary>
    /// Draws gizmos when the object is selected in the editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!ShouldDraw(true)) return;
        DrawPathGizmos();
    }

    #endregion

    #region Drawing

    /// <summary>
    /// Determines whether to draw the gizmos based on the current state and settings.
    /// </summary>
    /// <param name="isSelectedPass">Indicates if this is the selected gizmo pass.</param>
    /// <returns>True if gizmos should be drawn; otherwise, false.</returns>
    private bool ShouldDraw(bool isSelectedPass)
    {
        if (drawOnlyWhenSelected && !isSelectedPass)
        {
            return false;
        }

        bool playing = Application.isPlaying;

        if (playing && !drawInPlayMode)
        {
            return false;
        }

        if (!playing && !drawInEditMode)
        {
            return false;
        }

        if (mover == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Draws the path gizmos including lines, arrows, and waypoint spheres.
    /// </summary>
    private void DrawPathGizmos()
    {
        if (!TryFetchPath())
        {
            return;
        }

        if (cachedWaypoints == null || cachedWaypoints.Count < 2)
        {
            return;
        }

        Gizmos.color = Color.white;

        int count = cachedWaypoints.Count;
        int max = Mathf.Min(count - 1, Mathf.FloorToInt(maxArrowCount));

        int i = 0;
        while (i < max)
        {
            Vector3 a = cachedWaypoints[i];
            Vector3 b = cachedWaypoints[i + 1];

            Gizmos.DrawLine(a, b);
            DrawArrow(a, b);

            i++;
        }

        DrawWaypointSpheres();
    }

    /// <summary>
    /// Draws spheres at each waypoint in the path.
    /// </summary>
    private void DrawWaypointSpheres()
    {
        float r = Mathf.Max(0.01f, pointRadius);

        int i = 0;
        while (i < cachedWaypoints.Count)
        {
            if (i == cachedIndex)
            {
                Gizmos.color = Color.yellow;
            }
            else
            {
                Gizmos.color = Color.white;
            }

            Gizmos.DrawSphere(cachedWaypoints[i], r);
            i++;
        }

        Gizmos.color = Color.white;
    }

    /// <summary>
    /// Draws an arrow from one point to another.
    /// </summary>
    /// <param name="from">The starting point of the arrow.</param>
    /// <param name="to">The ending point of the arrow.</param>
    private void DrawArrow(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float mag = dir.magnitude;

        if (mag <= 0.0001f)
        {
            return;
        }

        Vector3 forward = dir / mag;

        float headLen = Mathf.Clamp(arrowHeadLength, 0.05f, mag * 0.5f);
        float angle = Mathf.Clamp(arrowHeadAngle, 1f, 80f);

        Vector3 right = Quaternion.AngleAxis(180f + angle, Vector3.up) * forward;
        Vector3 left = Quaternion.AngleAxis(180f - angle, Vector3.up) * forward;

        Vector3 head = to;
        Gizmos.DrawLine(head, head + (right * headLen));
        Gizmos.DrawLine(head, head + (left * headLen));
    }

    #endregion

    #region Data Fetch

    /// <summary>
    /// Attempts to fetch the current path waypoints and index from the mover agent.
    /// </summary>
    /// <returns>True if the path was successfully fetched; otherwise, false.</returns>
    private bool TryFetchPath()
    {
        cachedWaypoints = null;
        cachedIndex = 0;
        hasCache = false;

        List<Vector3> points;
        int index;

        bool ok = mover.TryGetDebugWaypoints(out points, out index);
        if (!ok || points == null)
        {
            return false;
        }

        cachedWaypoints = points;
        cachedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, points.Count - 1));
        hasCache = true;
        return hasCache;
    }

    #endregion
}