using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static registry of all active enemies in the scene.
/// Eliminates the need for FindGameObjectsWithTag("Enemy") which allocates memory every call.
/// Enemies self-register via EnemyHealth.OnEnable/OnDisable.
/// </summary>
public static class EnemyRegistry
{
    private static readonly List<Transform> _enemies = new List<Transform>(64);

    /// <summary>
    /// All currently active enemy transforms. Do not modify this list directly.
    /// </summary>
    public static IReadOnlyList<Transform> Enemies => _enemies;

    /// <summary>
    /// Number of currently active enemies.
    /// </summary>
    public static int Count => _enemies.Count;

    /// <summary>
    /// Registers an enemy. Called automatically by EnemyHealth.OnEnable.
    /// </summary>
    public static void Register(Transform enemy)
    {
        if (enemy != null && !_enemies.Contains(enemy))
        {
            _enemies.Add(enemy);
        }
    }

    /// <summary>
    /// Unregisters an enemy. Called automatically by EnemyHealth.OnDisable.
    /// </summary>
    public static void Unregister(Transform enemy)
    {
        if (enemy != null)
        {
            _enemies.Remove(enemy);
        }
    }

    /// <summary>
    /// Clears all registered enemies. Call when changing scenes.
    /// </summary>
    public static void Clear()
    {
        _enemies.Clear();
    }

    /// <summary>
    /// Finds the nearest enemy to a position. Returns null if no enemies exist.
    /// This is an O(n) search but allocates ZERO memory (unlike FindGameObjectsWithTag).
    /// </summary>
    public static Transform GetNearestEnemy(Vector3 position)
    {
        if (_enemies.Count == 0) return null;

        Transform nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Transform enemy = _enemies[i];
            
            // Clean up destroyed enemies (safety check)
            if (enemy == null)
            {
                _enemies.RemoveAt(i);
                continue;
            }

            Vector3 diff = enemy.position - position;
            diff.y = 0f; // XZ plane distance
            float distSqr = diff.sqrMagnitude;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = enemy;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Finds the nearest enemy within a maximum range. Returns null if none in range.
    /// </summary>
    public static Transform GetNearestEnemyInRange(Vector3 position, float maxRange)
    {
        if (_enemies.Count == 0) return null;

        Transform nearest = null;
        float maxRangeSqr = maxRange * maxRange;
        float bestDistSqr = float.MaxValue;

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Transform enemy = _enemies[i];
            
            if (enemy == null)
            {
                _enemies.RemoveAt(i);
                continue;
            }

            Vector3 diff = enemy.position - position;
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;

            if (distSqr <= maxRangeSqr && distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = enemy;
            }
        }

        return nearest;
    }
}
