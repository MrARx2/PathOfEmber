using UnityEngine;
using UnityEngine.UI;

namespace Boss
{
    /// <summary>
    /// UI component that shows health bar above a Titan body part when it takes damage.
    /// Auto-hides after a delay. Billboard faces camera.
    /// </summary>
    public class TitanPartHealthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The health component this UI displays")]
        private TitanHealth healthPart;
        
        [SerializeField, Tooltip("The target socket (for hit detection callback)")]
        private TitanTargetSocket targetSocket;
        
        [Header("UI Elements")]
        [SerializeField, Tooltip("Root canvas/container that gets shown/hidden")]
        private GameObject uiContainer;
        
        [SerializeField, Tooltip("Health bar fill image (uses Image.fillAmount)")]
        private Image healthFill;
        
        [SerializeField, Tooltip("Optional: outline/frame image")]
        private Image healthFrame;
        
        [SerializeField, Tooltip("Optional: body part name text")]
        private TMPro.TextMeshProUGUI partNameText;
        
        [Header("Appearance")]
        [SerializeField] private Color healthColor = Color.green;
        [SerializeField] private Color damagedColor = Color.red;
        [SerializeField, Tooltip("Time before UI fades/hides after last damage")]
        private float hideDelay = 2f;
        
        [Header("Position")]
        [SerializeField, Tooltip("Offset above the part")]
        private Vector3 positionOffset = new Vector3(0, 1f, 0);
        
        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        
        private float hideTimer;
        private bool isShowing;
        private Camera mainCamera;
        
        private void Awake()
        {
            mainCamera = Camera.main;
            
            // Auto-find references if not assigned
            if (healthPart == null)
                healthPart = GetComponentInParent<TitanHealth>();
            
            if (targetSocket == null)
                targetSocket = GetComponentInParent<TitanTargetSocket>();
            
            // Subscribe to health events
            if (healthPart != null)
            {
                healthPart.OnDamage.AddListener(OnPartDamaged);
                healthPart.OnHealthChanged.AddListener(OnHealthChanged);
                healthPart.OnHealed.AddListener(OnPartHealed);
                healthPart.OnDeath.AddListener(OnPartDestroyed);
            }
            
            // Set initial name
            if (partNameText != null && healthPart != null)
            {
                partNameText.text = GetPartDisplayName(healthPart.BodyPart);
            }
            
            // Hide initially
            Hide();
        }
        
        private void OnDestroy()
        {
            // Unsubscribe
            if (healthPart != null)
            {
                healthPart.OnDamage.RemoveListener(OnPartDamaged);
                healthPart.OnHealthChanged.RemoveListener(OnHealthChanged);
                healthPart.OnHealed.RemoveListener(OnPartHealed);
                healthPart.OnDeath.RemoveListener(OnPartDestroyed);
            }
        }
        
        private void LateUpdate()
        {
            // Billboard - face camera
            if (mainCamera != null && uiContainer != null && uiContainer.activeSelf)
            {
                transform.LookAt(transform.position + mainCamera.transform.forward);
            }
            
            // Position above target
            if (targetSocket != null)
            {
                transform.position = targetSocket.VisualCenter + positionOffset;
            }
            
            // Hide timer
            if (isShowing)
            {
                hideTimer -= Time.deltaTime;
                if (hideTimer <= 0f)
                {
                    Hide();
                }
            }
        }
        
        private void OnPartDamaged(int damage)
        {
            Show();
            
            // Flash red briefly
            if (healthFill != null)
            {
                healthFill.color = damagedColor;
                Invoke(nameof(ResetHealthColor), 0.1f);
            }
            
            if (debugLog)
                Debug.Log($"[TitanPartHealthUI] {healthPart.BodyPart} damaged for {damage}");
        }
        
        private void OnHealthChanged(float normalizedHealth)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = normalizedHealth;
            }
        }
        
        private void OnPartHealed()
        {
            Show();
            UpdateHealthBar();
        }
        
        private void OnPartDestroyed()
        {
            // Keep showing for a moment then hide
            if (healthFill != null)
            {
                healthFill.fillAmount = 0f;
                healthFill.color = damagedColor;
            }
            
            hideTimer = hideDelay;
        }
        
        private void Show()
        {
            if (uiContainer != null)
                uiContainer.SetActive(true);
            
            isShowing = true;
            hideTimer = hideDelay;
            
            UpdateHealthBar();
        }
        
        private void Hide()
        {
            if (uiContainer != null)
                uiContainer.SetActive(false);
            
            isShowing = false;
        }
        
        private void UpdateHealthBar()
        {
            if (healthFill != null && healthPart != null)
            {
                healthFill.fillAmount = healthPart.HealthPercent;
                healthFill.color = healthColor;
            }
        }
        
        private void ResetHealthColor()
        {
            if (healthFill != null)
                healthFill.color = healthColor;
        }
        
        private string GetPartDisplayName(TitanBodyPart part)
        {
            switch (part)
            {
                case TitanBodyPart.RightHand: return "Right Hand";
                case TitanBodyPart.LeftHand: return "Left Hand";
                case TitanBodyPart.Core: return "Core";
                default: return part.ToString();
            }
        }
        
        #region Public Methods
        /// <summary>
        /// Force show the UI (e.g., when player targets this part).
        /// </summary>
        public void ForceShow()
        {
            Show();
        }
        
        /// <summary>
        /// Force hide the UI.
        /// </summary>
        public void ForceHide()
        {
            Hide();
        }
        #endregion
    }
}
