using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// Miniboss enemy - shoots projectiles and spawns meteors.
    /// </summary>
    public class MinibossAI : EnemyAIBase
    {
        [Header("=== PROJECTILE ATTACK ===")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private int projectileDamage = 25;
        [SerializeField] private float shootsPerSecond = 1f;
        [SerializeField] private float shootingPause = 0.5f;

        [Header("=== METEOR ATTACK ===")]
        [SerializeField] private GameObject meteorStrikePrefab;
        [SerializeField] private float meteorsPerSecond = 0.2f;
        [SerializeField] private float meteorPause = 0.5f;
        [SerializeField] private float meteorSpawnRadius = 2f;

        [Header("=== ANIMATION TRIGGERS ===")]
        [SerializeField] private string shootTrigger = "Shoot";
        [SerializeField] private string meteorTrigger = "CastMeteor";

        [Header("=== POSITIONING ===")]
        [SerializeField] private float preferredRange = 5f;

        private float shootCooldown;
        private float meteorCooldown;
        private float pauseTimer;

        protected override void Awake()
        {
            base.Awake();
            
            // Miniboss priority: low (others move around it)
            // if (agent != null)
            //     agent.avoidancePriority = 75; // REMOVED
        }

        protected override void Start()
        {
            base.Start();
            if (projectileSpawnPoint == null)
                projectileSpawnPoint = transform;
        }

        protected override void Update()
        {
            if (health != null && (health.IsDead || health.IsFrozen))
            {
                StopMovement();
                return;
            }

            if (target == null)
            {
                StopMovement();
                return;
            }

            // Handle pause
            if (pauseTimer > 0)
            {
                pauseTimer -= Time.deltaTime;
                StopMovement();
                LookAtTarget();
                UpdateAnimator();
                return;
            }

            float distance = Vector3.Distance(VisualPosition, target.position);
            LookAtTarget();

            // Move to preferred range
            if (distance > preferredRange + 2f)
            {
                ChasePlayer();
            }
            else
            {
                StopMovement();
                HandleCombat();
            }

            UpdateAnimator();
        }

        private void HandleCombat()
        {
            // Update cooldowns
            shootCooldown -= Time.deltaTime;
            meteorCooldown -= Time.deltaTime;

            // Prioritize meteor (less frequent)
            if (meteorCooldown <= 0f && meteorStrikePrefab != null)
            {
                SpawnMeteor();
                meteorCooldown = 1f / Mathf.Max(0.01f, meteorsPerSecond);
                pauseTimer = meteorPause;
            }
            else if (shootCooldown <= 0f && projectilePrefab != null)
            {
                ShootProjectile();
                shootCooldown = 1f / Mathf.Max(0.01f, shootsPerSecond);
                pauseTimer = shootingPause;
            }
        }

        private void ShootProjectile()
        {
            if (animator != null)
                animator.SetTrigger(shootTrigger);

            Vector3 spawnPos = projectileSpawnPoint.position;
            Vector3 targetPos = target.position + Vector3.up * 1f;
            Vector3 direction = (targetPos - spawnPos).normalized;

            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            
            EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
            if (ep != null)
            {
                ep.SetDirection(direction);
                ep.SetSpeed(projectileSpeed);
                ep.SetDamage(projectileDamage);
            }
        }

        private void SpawnMeteor()
        {
            if (animator != null)
                animator.SetTrigger(meteorTrigger);

            Vector2 offset = Random.insideUnitCircle * meteorSpawnRadius;
            Vector3 spawnPos = target.position + new Vector3(offset.x, 0, offset.y);
            spawnPos.y = 0f;

            Instantiate(meteorStrikePrefab, spawnPos, Quaternion.identity);
        }

        protected override void OnAttack() { } // Miniboss uses custom attack system
    }
}
