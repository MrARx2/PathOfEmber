using UnityEngine;

/// <summary>
/// Helper script for spawning VFX/particles via Unity Events.
/// Attach to any enemy, then wire up events in the inspector.
/// </summary>
public class VFXSpawner : MonoBehaviour
{
    [Header("Hit VFX")]
    [SerializeField, Tooltip("VFX prefab to spawn when hit")]
    private GameObject hitVFXPrefab;
    [SerializeField, Tooltip("Offset from enemy position")]
    private Vector3 hitVFXOffset = Vector3.zero;

    [Header("Death VFX")]
    [SerializeField, Tooltip("VFX prefab to spawn on death")]
    private GameObject deathVFXPrefab;
    [SerializeField, Tooltip("Offset from enemy position")]
    private Vector3 deathVFXOffset = Vector3.zero;

    [Header("Settings")]
    [SerializeField, Tooltip("How long before VFX auto-destroys")]
    private float vfxLifetime = 3f;

    /// <summary>
    /// Call this from OnDamage event.
    /// </summary>
    public void SpawnHitVFX(int damage)
    {
        if (hitVFXPrefab == null) return;
        SpawnVFX(hitVFXPrefab, hitVFXOffset);
    }

    /// <summary>
    /// Call this from OnDamage event (no parameter version).
    /// </summary>
    public void SpawnHitVFX()
    {
        if (hitVFXPrefab == null) return;
        SpawnVFX(hitVFXPrefab, hitVFXOffset);
    }

    /// <summary>
    /// Call this from OnDeath event.
    /// </summary>
    public void SpawnDeathVFX()
    {
        if (deathVFXPrefab == null) return;
        SpawnVFX(deathVFXPrefab, deathVFXOffset);
    }

    /// <summary>
    /// Generic spawn method - can be called from any event.
    /// </summary>
    public void SpawnVFXAtPosition(GameObject prefab)
    {
        if (prefab == null) return;
        SpawnVFX(prefab, Vector3.zero);
    }

    private void SpawnVFX(GameObject prefab, Vector3 offset)
    {
        Vector3 spawnPos = transform.position + offset;
        
        if (ObjectPoolManager.Instance != null)
        {
            GameObject vfx = ObjectPoolManager.Instance.Get(prefab, spawnPos, Quaternion.identity);
            StartCoroutine(ReturnVFXDelayed(vfx, vfxLifetime));
        }
        else
        {
            GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }
    }
    
    private System.Collections.IEnumerator ReturnVFXDelayed(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Return(vfx);
        }
    }
}
