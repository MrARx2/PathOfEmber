using UnityEngine;

namespace Effects
{
    /// <summary>
    /// Simulates a hanging object swinging in the wind (pendulum motion).
    /// Uses Perlin noise and Sine waves to create natural-looking irregular motion.
    /// </summary>
    public class WindPendulum : MonoBehaviour
    {
        [Header("Motion Settings")]
        [SerializeField, Tooltip("Maximum angle the object swings in degrees.")]
        private float swingAmplitude = 15f;
        
        [SerializeField, Tooltip("Base speed of the swinging motion.")]
        private float swingSpeed = 2f;
        
        [SerializeField, Tooltip("Axis to swing around (e.g., (0, 0, 1) for Z-axis swinging).")]
        private Vector3 swingAxis = Vector3.forward;
        
        [Header("Wind Irregularity")]
        [SerializeField, Tooltip("Adds random variations to the swing speed/amplitude over time.")]
        private float turbulence = 0.5f;
        
        [SerializeField, Tooltip("How fast the wind strength changes.")]
        private float turbulenceFrequency = 0.5f;

        [Header("Optimization")]
        [SerializeField, Tooltip("Distance from camera to disable this script.")]
        private float cullDistance = 50f;
        
        private float timeOffset;
        private Quaternion initialRotation;
        private Transform trans;
        private Transform mainCamera;

        private void Awake()
        {
            trans = transform;
            initialRotation = trans.localRotation;
            
            // Random start time to prevent identical syncing of multiple lamps
            timeOffset = Random.Range(0f, 100f);
        }

        private void Start()
        {
            if (Camera.main != null)
                mainCamera = Camera.main.transform;
        }

        private void Update()
        {
            // Performance check: Distance culling
            if (mainCamera != null)
            {
                if (Vector3.Distance(trans.position, mainCamera.position) > cullDistance)
                    return;
            }

            // Calculate wind factor using Perlin noise (creates natural gusts)
            float noise = Mathf.PerlinNoise(Time.time * turbulenceFrequency + timeOffset, 0f);
            
            // Allow swing to go negative/positive by mapping noise (0-1) to roughly (0.5-1.5) multiplier
            float windStrength = 1f + (noise - 0.5f) * turbulence; 

            // Calculate swing angle
            // Sine wave for base pendulum motion + noise variation
            float time = Time.time * swingSpeed + timeOffset;
            float angle = Mathf.Sin(time) * swingAmplitude * windStrength;

            // Apply noise to the axis slightly for 3D wobble (optional polish)
            // Adds a tiny bit of rotation on the other axes based on noise
            float wobble = Mathf.Cos(time * 0.7f) * (swingAmplitude * 0.1f * turbulence);
            Vector3 finalAxis = swingAxis;
            
            // Apply rotation relative to initial rotation
            Quaternion swingRotation = Quaternion.AngleAxis(angle, finalAxis);
            
            // Add slight secondary wobble perpendicular to main axis
            if (turbulence > 0)
            {
                Vector3 perpendicularAxis = Vector3.Cross(swingAxis, Vector3.up);
                if (perpendicularAxis == Vector3.zero) perpendicularAxis = Vector3.right;
                swingRotation *= Quaternion.AngleAxis(wobble, perpendicularAxis);
            }

            trans.localRotation = initialRotation * swingRotation;
        }
        
        private void OnValidate()
        {
            if (swingAxis == Vector3.zero)
                swingAxis = Vector3.forward;
            
            swingAxis.Normalize();
        }
    }
}
