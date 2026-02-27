# 3D Multiplayer Octree Pathfinding and AI System (Unity)

A fully custom volumetric 3D navigation and AI framework built in Unity using C#.

This system replaces Unity’s NavMesh with a true 3D octree-based pathfinding solution designed for:
1. Large-scale environments
2. Zero-gravity or space-based traversal
3. Multiplayer server-authoritative movement
4. Dynamic obstacle-heavy worlds

---

## System Overview

This project implements:
1. Octree-based spatial partitioning (adaptive + full subdivision)
2. Leaf graph extraction and connectivity building
3. Custom 3D A* search with optional dynamic penalties
4. Traffic-aware path diversification (reservation-based)
5. Server-authoritative physics movement with client smoothing
6. Behavior-tree-driven AI using a blackboard pattern
7. Match flow support (map generation, AI spawning, capture zones)
8. Steam lobby / UDP connection startup flow

---

## Why Not Unity NavMesh (for this use case)

Unity NavMesh is excellent for surface traversal, but it is not designed for true volumetric 3D movement (e.g., space / zero-gravity). This project targets 3D navigation through volume, not across a surface.

<p align="center">
  <img src="Documents/Images/7F0CZ.png" width="900" />
</p>

---

## Multiplayer Architecture (Server Authority)

Movement and pathing are computed on the server. Clients receive state updates and smoothly interpolate toward authoritative positions.

<p align="center">
  <img src="Documents/Images/Hybrid-architecture-for-client-server-mobile-multiplayer-games.png" width="900" />
</p>

<p align="center">
  <img src="Documents/Images/server-authoritative-f91558362b208ca10eae39e25dd9698d.png" width="900" />
</p>

---

## Octree Spatial Partitioning

The world volume is subdivided into hierarchical nodes, enabling efficient spatial queries, obstacle insertion, and volumetric navigation.

<p align="center">
  <img src="Documents/Images/Figure-Octree-structure-image-a-shows-the-subdivision-in-cubes-image-b-the-tree.png" width="900" />
</p>

Octree decomposition is used to represent navigable free space and blocked regions at different resolutions.

<p align="center">
  <img src="Documents/Images/Example-of-octree-decomposition.png" width="900" />
</p>

---

## Runtime Octree Debug View

This project includes visualization to inspect subdivision levels and leaf occupancy during development.

<p align="center">
  <img src="Documents/Images/octree_example_screen_mini.png" width="900" />
</p>

A reference-style visualization of dense subdivision in complex geometry (useful to communicate the concept clearly in documentation).

<p align="center">
  <img src="Documents/Images/Spatial-partitioning-of-the-octree-structure-Left-A-voxelized-version-of-the-Stanford.png" width="900" />
</p>

---

## Obstacle Collection and Leaf Validation

Obstacles are collected from physics colliders inside a defined world bounds volume. Leaves can be validated using physics overlap checks for geometry-accurate occupancy.

<p align="center">
  <img src="Documents/Images/fig1-1.png" width="900" />
</p>

---

## Leaf Graph Extraction (Connectivity)

Empty leaves are collected and converted into a connectivity graph (face neighbors plus additional adjacency linking). This graph becomes the base structure for A*.

<p align="center">
  <img src="Documents/Images/qapH8.png" width="900" />
</p>

---

## Custom 3D A* Pathfinding

A* runs over the octree leaf graph using a Euclidean 3D heuristic and a custom graph implementation.

<p align="center">
  <img src="Documents/Images/gPeMi.png" width="900" />
</p>

The implementation supports:
1. Euclidean 3D heuristic
2. SortedSet-based open set
3. Search-ID reuse to avoid per-search allocations
4. Iteration safety caps
5. Best-so-far fallback (partial solution when needed)
6. Optional dynamic cost injection (used for traffic avoidance)

---

## Volumetric Path Example (Multi-Level Traversal)

A representative example showing how paths can route through 3D volume across different heights and around obstacles.

<p align="center">
  <img src="Documents/Images/aerospace-12-00085-g014.png" width="900" />
</p>

---

## Traffic-Aware Path Diversification

To avoid many agents selecting the same shortest path, the system supports leaf “reservations” that temporarily penalize recently used paths (optionally same-team only).

Result:
1. Less clustering
2. Better path variety
3. More natural agent distribution

---

## Server-Authoritative Physics Movement

Agents move using Rigidbody motion on the server. Clients receive pose updates and render smooth motion via interpolation.

The approach prioritizes:
1. Correctness (authority)
2. Stability (no tunneling through obstacles)
3. Visual smoothness (client interpolation)

---

## Network Smoothing and Interpolation

Clients interpolate toward the most recent authoritative server state to reduce jitter and teleport artifacts.

<p align="center">
  <img src="Documents/Images/Smoothing-and-interpolation-with-P-splines-solid-black-lines-for-different-numbers-of.png" width="900" />
</p>

---

## Behavior Tree AI

AI decision-making is implemented using a behavior tree structure.

<p align="center">
  <img src="Documents/Images/selector1.png" width="900" />
</p>

An example editor-style view of a selector/sequence layout (used to communicate the structure clearly).

<p align="center">
  <img src="Documents/Images/19ee3f120a6df87879aabb765080f8ed8c5a370d_2_690x458.png" width="900" />
</p>

---

## Blackboard Pattern

Behavior tree nodes share state through a blackboard pattern. This keeps the AI modular and reduces coupling between systems.

<p align="center">
  <img src="Documents/Images/blackboard-pattern-components.png" width="900" />
</p>

A reference-style architecture diagram showing the blackboard concept at a higher level.

<p align="center">
  <img src="Documents/Images/Blackboard-architecture.png" width="900" />
</p>

---

## Physics-Based Movement Safety

Each movement step can be validated against obstacle colliders using:
1. SphereCast sweep checks
2. Overlap collision checks
3. Self-collider filtering
4. Clearance padding
5. Fast repath when blocked

This keeps movement physically valid in dynamic environments and multiplayer conditions.

---

## Execution Pipeline

1. Collect obstacle colliders (inside world bounds)
2. Build octree (adaptive or full subdivision)
3. Validate leaves (optional geometry-accurate checks)
4. Collect navigable leaves
5. Build graph connectivity (face + adjacency hash)
6. Run A* (optionally with traffic penalties)
7. Convert leaf path to world waypoints
8. Move using server-authoritative Rigidbody steps
9. Replicate pose to clients and interpolate on observers
10. Drive AI decisions via behavior tree + blackboard

---

## Code Areas Covered (high level)

- Octree generation, occupancy checks, and graph building
- Custom A* graph search and waypoint output
- Agent movement with safety checks and fast repathing
- Server-authoritative replication + client smoothing
- Behavior tree AI nodes and event-driven transitions
- Capture zone controller + manager sync
- Map generation + AI spawning state flow
- Steam lobby / UDP connection bootstrapping

---

## Technologies Used

1. Unity (C#)
2. Rigidbody Physics
3. Custom Octree Implementation
4. Custom A* Pathfinding
5. Multiplayer Networking (server authoritative)
6. Behavior Trees
7. Blackboard Architecture
