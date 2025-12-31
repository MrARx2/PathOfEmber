using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hazards
{
    [RequireComponent(typeof(Collider))]
    public class LavaController : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int damagePerSecond = 10;
        [SerializeField] private bool ignitePlayer = true;
        [SerializeField] private float fireTickRate = 1.0f;

        [Header("Visual Settings")]
        [SerializeField] private bool scrollTexture = true;
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.1f, 0.05f);
        [SerializeField] private string texturePropertyName = "_MainTex";

        [Header("Tide Settings")]
        [SerializeField] private bool riseAndFall = true;
        [SerializeField] private float tideHeight = 0.5f;
        [SerializeField] private float tideSpeed = 0.5f;

        private Renderer _renderer;
        private Vector3 _initialPosition;
        private Coroutine _damageCoroutine;
        private PlayerHealth _playerInLava;

        private void Start()
        {
            _renderer = GetComponent<Renderer>();
            _initialPosition = transform.position;
            
            // Ensure collider is a trigger
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void Update()
        {
            HandleVisuals();
            HandleTide();
        }

        private void HandleVisuals()
        {
            if (scrollTexture && _renderer != null)
            {
                Vector2 offset = Time.time * scrollSpeed;
                _renderer.material.SetTextureOffset(texturePropertyName, offset);
            }
        }

        private void HandleTide()
        {
            if (riseAndFall)
            {
                float yOffset = Mathf.Sin(Time.time * tideSpeed) * tideHeight;
                transform.position = _initialPosition + Vector3.up * yOffset;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerHealth health = other.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    _playerInLava = health;
                    if (ignitePlayer)
                    {
                        health.SetOnFire(true);
                    }
                    if (_damageCoroutine == null)
                    {
                        _damageCoroutine = StartCoroutine(DamageRoutine(health));
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (_playerInLava != null && other.gameObject == _playerInLava.gameObject)
                {
                    if (ignitePlayer)
                    {
                        // Optional: Stop fire immediately or let it linger?
                        // For now, let's stop it when leaving the immediate lava source 
                        // effectively saying "you are no longer IN the fire".
                        // Logic in PlayerHealth might keep it on if intended, but SetOnFire(false) toggles the visual.
                        _playerInLava.SetOnFire(false); 
                    }
                    _playerInLava = null;
                    if (_damageCoroutine != null)
                    {
                        StopCoroutine(_damageCoroutine);
                        _damageCoroutine = null;
                    }
                }
            }
        }

        private IEnumerator DamageRoutine(PlayerHealth health)
        {
            while (health != null && !health.IsDead)
            {
                health.TakeDamage(damagePerSecond);
                yield return new WaitForSeconds(fireTickRate);
            }
        }
    }
}
