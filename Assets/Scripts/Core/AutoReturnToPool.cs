using UnityEngine;

/// <summary>
/// Lightweight component that returns a pooled object after a delay.
/// Survives independently of the spawner (unlike coroutines on the spawner).
/// </summary>
public class AutoReturnToPool : MonoBehaviour
{
    private float timer;
    private bool isRunning;

    public void ReturnAfter(float delay)
    {
        timer = delay;
        isRunning = true;
    }

    private void Update()
    {
        if (!isRunning) return;
        
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            isRunning = false;
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnEnable()
    {
        // Reset when re-enabled from pool
        isRunning = false;
        timer = 0f;
    }
}
