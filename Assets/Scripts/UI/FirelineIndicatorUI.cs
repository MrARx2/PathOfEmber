using UnityEngine;
using TMPro;
using Hazards;

namespace UI
{
    /// <summary>
    /// Points an arrow UI towards the Hazard Zone's Fireline and displays distance.
    /// Needs to be on the Canvas (SystemsCanvasUI).
    /// </summary>
    public class FirelineIndicatorUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("The pivot object that rotates (IndicatorPivot).")]
        private RectTransform indicatorPivot;
        
        [SerializeField, Tooltip("Optional background object (FireBackground).")]
        private RectTransform backgroundRect;
        
        [SerializeField, Tooltip("Text to display distance (Range).")]
        private TextMeshProUGUI distanceText;

        [Header("Settings")]
        [SerializeField, Tooltip("Name of the child object in HazardZoneMeteors to target. Defaults to 'Fireline'.")]
        private string targetChildName = "Fireline";
        
        [SerializeField, Tooltip("If true, hides the indicator if player is very close.")]
        private bool hideWhenClose = false;
        
        [SerializeField, Tooltip("Distance to hide when in front of the line.")]
        private float hideDistanceFront = 2f;

        [SerializeField, Tooltip("Distance to hide when behind the line.")]
        private float hideDistanceBehind = 2f;
        
        [Header("Adjustment")]
        [SerializeField, Range(-180f, 180f), Tooltip("Manual rotation offset in degrees. 0 = Up, 180 = Down.")]
        private float rotationOffset = 0f;
        
        [SerializeField, Tooltip("Invert the logic of what counts as 'In Front' vs 'Behind'.")]
        private bool invertSideDetection = false;
        
        [SerializeField, Tooltip("Use World Forward (Z+) instead of Target's local forward for side detection.")]
        private bool useWorldForward = true;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        [SerializeField] private bool showGizmos = true;

        private Transform player;
        private Transform targetTransform;
        private Camera mainCam;
        private HazardZoneMeteors hazardZone;

        private void Start()
        {
            // 1. Find Player
            if (player == null)
            {
                var pParams = FindFirstObjectByType<PlayerMovement>();
                if (pParams != null) player = pParams.transform;
                
                // Fallback by tag
                if (player == null)
                {
                    GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                    if (pObj != null) player = pObj.transform;
                }
            }

            // 2. Find Camera
            mainCam = Camera.main;

            // 3. Find Hazard Zone and Target
            FindTarget();
        }

        private void FindTarget()
        {
            if (hazardZone == null)
            {
                hazardZone = FindFirstObjectByType<HazardZoneMeteors>();
            }

            if (hazardZone != null)
            {
                // Try to find specific "Fireline" child
                Transform specificChild = hazardZone.transform.Find(targetChildName);
                if (specificChild != null)
                {
                    targetTransform = specificChild;
                    if (debugLog) Debug.Log($"[FirelineIndicator] Found specific target: {specificChild.name}");
                }
                else
                {
                    // Fallback to the zone itself
                    targetTransform = hazardZone.transform;
                    if (debugLog) Debug.LogWarning($"[FirelineIndicator] Could not find child '{targetChildName}' in HazardZone. Using HazardZone root.");
                }
            }
            else
            {
                if (debugLog) Debug.LogWarning("[FirelineIndicator] Could not find HazardZoneMeteors in scene.");
            }
        }

        private int lastDistanceInt = -1;
        private bool wasVisible = true;

        private void Update()
        {
            // Retry finding references if missing (e.g. if loaded async)
            if (player == null || targetTransform == null || mainCam == null)
            {
                if (Time.frameCount % 60 == 0) Start(); // slow retry
                return;
            }

            UpdateIndicator();
        }

        private void UpdateIndicator()
        {
            Vector3 playerPos = player.position;
            Vector3 targetPos = targetTransform.position;

            // 1. Calculate Distance
            // Use 3D distance but ignore Y if needed. For hazard zones, flat distance is usually best.
            Vector3 diff = targetPos - playerPos;
            Vector3 diffFlat = new Vector3(diff.x, 0f, diff.z);
            float distSq = diffFlat.sqrMagnitude;
            float distance = Mathf.Sqrt(distSq); // Need actual distance for text

            // 2. Determine Visibility Logic
            
            // Determine forward vector logic
            Vector3 referenceForward = useWorldForward ? Vector3.forward : targetTransform.forward;
            
            // Calculate signed distance along forward axis
            // Dot Product: Positive = In Front (Same direction as Forward), Negative = Behind
            float signedDist = Vector3.Dot(referenceForward, playerPos - targetPos);
            
            // Logic: usually "Positive" means "In Front".
            bool isInFront = invertSideDetection ? (signedDist < 0) : (signedDist > 0);
            
            // Determine which hide threshold to use
            bool shouldHide = false;
            if (hideWhenClose)
            {
                if (isInFront)
                {
                    shouldHide = distance < hideDistanceFront;
                }
                else
                {
                    shouldHide = distance < hideDistanceBehind;
                }
            }
            
            bool shouldShow = !shouldHide;

            // Optimize SetActive calls (only call if state changes)
            if (shouldShow != wasVisible)
            {
                wasVisible = shouldShow;
                if (indicatorPivot != null) indicatorPivot.gameObject.SetActive(shouldShow);
                if (backgroundRect != null) backgroundRect.gameObject.SetActive(shouldShow);
                if (distanceText != null) distanceText.enabled = shouldShow;
            }

            // If hidden, skip the rest of the math to save CPU
            if (!shouldShow) return;

            // 3. Update Text (Only if value changed to avoid string allocation GC)
            if (distanceText != null)
            {
                int currentDistInt = Mathf.RoundToInt(distance);
                if (currentDistInt != lastDistanceInt)
                {
                    lastDistanceInt = currentDistInt;
                    distanceText.text = $"{currentDistInt}m";
                }
            }
            
            // 4. Update Visuals & Rotation
            if (distance > 0.1f && indicatorPivot != null)
            {
                // Direction from player to target
                Vector3 toTargetDir = diffFlat.normalized;
                
                // Camera forward on the ground plane
                Vector3 camForward = mainCam.transform.forward;
                camForward.y = 0f;
                
                if (camForward.sqrMagnitude > 0.001f)
                {
                    camForward.Normalize();

                    // Signed angle from Camera Forward to Target Direction
                    float angle = Vector3.SignedAngle(camForward, toTargetDir, Vector3.up);

                    // Unity UI Rotation Z: 
                    // +Z is Left (CCW). -Z is Right (CW).
                    // If angle is +90 (Target is Right), we want -90 rotation.
                    float zRotation = -angle + rotationOffset;

                    Quaternion rot = Quaternion.Euler(0f, 0f, zRotation);
                    indicatorPivot.localRotation = rot;
                }
            }
            
            if (debugLog && Time.frameCount % 60 == 0)
            {
                 Debug.Log($"[FirelineUI] Dist: {distance:F1}, Signed: {signedDist:F1}, InFront: {isInFront}, Hiding: {shouldHide}"); 
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || player == null || targetTransform == null) return;

            // Draw line from player to target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(player.position, targetTransform.position);
            
            // Draw sphere at target
            Gizmos.DrawWireSphere(targetTransform.position, 1f);
        }
    }
}
