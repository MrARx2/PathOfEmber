using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CameraSystem
{
    /// <summary>
    /// Updates the URP Camera's Volume Trigger to follow the Player (or specified target)
    /// instead of the Camera itself. This ensures post-processing effects activate based
    /// on where the Player is standing.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraVolumeTrigger : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField, Tooltip("The transform that should trigger post-processing volumes (usually the Player). If null, attempts to find object with Player tag.")]
        private Transform triggerTarget;

        private UniversalAdditionalCameraData _cameraData;
        private bool _isInitialized = false;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Get the URP specific camera data
            _cameraData = GetComponent<UniversalAdditionalCameraData>();
            
            if (_cameraData == null)
            {
                // Try to add it if it doesn't exist (unlikely for URP camera but safe to handle)
                _cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            FindTarget();
            UpdateTrigger();
            
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_isInitialized) UpdateTrigger();
        }

        private void Update()
        {
            // If we don't have a target, try to find one periodically
            if (triggerTarget == null)
            {
                if (Time.frameCount % 60 == 0) // Simple interval check
                {
                    FindTarget();
                }
            }
            
            // Constantly enforce the trigger if we have a target
            // (In case something else resets it, though usually setting it once is enough)
            if (triggerTarget != null && _cameraData != null && _cameraData.volumeTrigger != triggerTarget)
            {
                UpdateTrigger();
            }
        }

        private void FindTarget()
        {
            if (triggerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    triggerTarget = player.transform;
                    UpdateTrigger();
                }
            }
        }

        private void UpdateTrigger()
        {
            if (_cameraData != null && triggerTarget != null)
            {
                _cameraData.volumeTrigger = triggerTarget;
            }
        }
    }
}
