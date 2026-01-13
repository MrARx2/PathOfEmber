using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hazards
{
    /// <summary>
    /// Lava hazard that ignites the player. Fire damage is handled centrally by PlayerHealth.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LavaController : MonoBehaviour
    {
        [Header("Fire Settings")]
        [SerializeField, Tooltip("Set player on fire when they enter lava")]
        private bool ignitePlayer = true;

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
            if (scrollTexture && _renderer != null && _renderer.material != null)
            {
                // Only set offset if the shader has this property
                if (_renderer.material.HasProperty(texturePropertyName))
                {
                    Vector2 offset = Time.time * scrollSpeed;
                    _renderer.material.SetTextureOffset(texturePropertyName, offset);
                }
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
            if (!ignitePlayer) return;
            if (!other.CompareTag("Player")) return;
            
            // Find PlayerHealth in hierarchy
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null)
                health = other.GetComponentInChildren<PlayerHealth>();
            if (health == null)
                health = other.GetComponentInParent<PlayerHealth>();
            
            if (health != null)
            {
                _playerInLava = health;
                // Fire damage is handled centrally by PlayerHealth
                health.SetOnFire(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            if (_playerInLava != null && other.gameObject == _playerInLava.gameObject)
            {
                _playerInLava.SetOnFire(false);
                _playerInLava = null;
            }
        }
    }
}
