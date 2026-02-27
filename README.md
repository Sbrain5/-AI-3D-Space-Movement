# 3D Multiplayer Octree Pathfinding and AI System (Unity)

A fully custom volumetric 3D navigation and AI framework built in Unity using C#.

This system replaces Unity’s NavMesh with a true 3D octree-based pathfinding solution designed for:
1. Large-scale environments
2. Zero-gravity or space-based traversal
3. Multiplayer server-authoritative movement
4. Dynamic obstacle-heavy worlds

# System Overview

This project implements:
1. Octree-based spatial partitioning
2. Custom A* graph search
3. Traffic-aware path diversification
4. Server-authoritative multiplayer movement
5. Physics-validated movement execution
6. Behavior-tree-driven AI logic
7. Interpolation-based network smoothing

# Octree Spatial Partitioning

<p align="center"> <img src="Documents/Images/Spatial-partitioning-of-the-octree-structure-Left-A-voxelized-version-of-the-Stanford.png" width="900"/> </p>

The world is subdivided into a hierarchical octree structure enabling efficient spatial queries and volumetric navigation.

Key features:
1. Adaptive subdivision
2. Configurable minimum leaf size
3. Mesh-aware occupancy validation
4. Broad-phase obstacle insertion
5. Leaf-level connectivity extraction
6. Scalable world bounds

This allows true 3D navigation rather than surface-based traversal.

# Large-Scale Leaf Graph Extraction

<p align="center"> <img src="Documents/Images/qapH8.png" width="900"/> </p>

Navigable leaves are extracted and converted into a graph structure.

Each leaf node:
1. Represents empty traversable space
2. Maintains adjacency links
3. Stores spatial bounds
4. Supports dynamic penalty injection

This graph is used as the foundation for A* search.

# Custom A* Pathfinding

<p align="center"> <img src="Documents/Images/gPeMi.png" width="900"/> </p>

The system implements a fully custom A* algorithm with:
1. Euclidean 3D heuristic
2. SortedSet-based open list
3. Search ID reuse (no per-search allocation resets)
4. Iteration safety caps
5. Best-so-far fallback logic
6. Runtime penalty injection

Designed for high-frequency multiplayer usage.

# Traffic-Aware Path Diversification

Agents reserve early path segments to prevent congestion.

Features:
1. Leaf reservation system
2. Prefix-only path locking
3. Time-based expiration
4. Team-based filtering
5. Deterministic tie-breaking

Result:
1. Reduced clustering
2. Natural traffic flow
3. Emergent movement behavior

# Server-Authoritative Multiplayer Movement

<p align="center"> <img src="Documents/Images/server-authoritative-f91558362b208ca10eae39e25dd9698d.png" width="900"/> </p>

Movement is computed exclusively on the server.

Architecture characteristics:
1. Server-side pathfinding
2. Rigidbody-based physics execution
3. Client observer interpolation
4. State replication via synchronized variables
5. Deterministic tick-based synchronization

Prevents cheating and eliminates client-side authority issues.

# Network Smoothing and Interpolation

<p align="center"> <img src="Documents/Images/Smoothing-and-interpolation-with-P-splines-solid-black-lines-for-different-numbers-of.png" width="900"/> </p>

Clients interpolate toward authoritative server state using exponential smoothing.

Benefits:
1. Eliminates teleport jitter
2. Reduces visual stutter
3. Maintains deterministic state alignment
4. Smooth multi-client rendering

# Behavior Tree AI System

<p align="center"> <img src="Documents/Images/selector1.png" width="900"/> </p>

AI decision logic is implemented using a behavior tree architecture.

Core components:
1. Selector nodes
2. Sequence nodes
3. Blackboard pattern
4. Objective-driven movement nodes
5. Idle roaming fallback logic

Supports:
1. Capture point behavior
2. Repath intervals
3. Arrival thresholds
4. State-based transitions

# Full Octree Visualization

<p align="center"> <img src="Documents/Images/octree_example_screen_mini.png" width="900"/> </p>

Visualization tools allow debugging of:
1. Octree subdivision levels
2. Occupied vs empty leaves
3. Active path nodes
4. Traffic reservations
5. Runtime movement validation

Physics-Based Movement Safety

Each movement step includes:
1. SphereCast sweep checks
2. Overlap collision validation
3. Self-collider filtering
4. Clearance padding
5. Dynamic repathing when blocked

Ensures physically valid navigation even in dynamic multiplayer scenarios.

# Pathfinding Pipeline

1. Collect obstacle colliders
2. Build octree
3. Extract empty leaves
4. Build adjacency graph
5. Run A*
6. Convert leaf path to world waypoints
7. Reserve path prefix
8. Execute server-authoritative movement

# Performance Considerations

1. Non-alloc physics queries
2. Search ID reuse
3. Spatial hashing
4. Iteration caps
5. Dictionary reuse
6. Configurable leaf limits

Designed for large-scale 3D environments with many simultaneous agents.

# Technologies Used

1. Unity (C#)
2. Custom Octree Implementation
3. Custom A* Pathfinding
4. Multiplayer Networking
5. Behavior Trees
6. Rigidbody Physics
7. Deterministic Tick Synchronization
