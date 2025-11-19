using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float speed = 12f;
    [SerializeField, Tooltip("Seconds before auto-destroy")] private float lifetime = 3f;
    [SerializeField, Tooltip("Destroy only when hitting an Enemy")] private bool destroyOnHit = true;

    private Vector3 moveDir = Vector3.forward;
    private float lifeTimer;
    private Rigidbody rb;

    private void OnEnable()
    {
        lifeTimer = lifetime;
        if (rb == null) rb = GetComponent<Rigidbody>();
        // Ensure projectiles never physically push the player
        var cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].isTrigger = true;
        }
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true; // guarantee no impulses
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        ApplyVelocity();
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude > 1e-6f)
        {
            moveDir = dir.normalized;
            transform.forward = moveDir;
            ApplyVelocity();
        }
    }
    
    public void SetSpeed(float newSpeed)
    {
        if (newSpeed > 0f)
        {
            speed = newSpeed;
            ApplyVelocity();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        // Always move via Transform (RB is kinematic to avoid impulses)
        transform.position += moveDir * speed * dt;

        lifeTimer -= dt;
        if (lifeTimer <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!destroyOnHit) return;
        if (other == null) return;
        if (other.CompareTag("Player")) return;
        if (!other.CompareTag("Enemy")) return;
        Destroy(gameObject);
    }

    private void ApplyVelocity()
    {
        // No-op when kinematic; movement handled in Update
    }
}
