using UnityEngine;
using System.Collections;

/// <summary>
/// Interaction trigger for the Yatai Shop.
/// Opens the YataiShopUI when the player enters the trigger area.
/// </summary>
public class YataiShopInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private YataiShopUI shopUI;
    [SerializeField, Tooltip("Optional: Visual object (e.g. 'Press F', Glowing Circle) to hide when shop opens")]
    private GameObject interactionIndicator;

    [Header("Settings")]
    [SerializeField, Tooltip("Delay before the shop interaction becomes available again after closing")]
    private float cooldownDuration = 1.0f;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    private Behaviour[] indicatorBehaviours;
    private bool useComponentToggling = false;
    private bool isCoolingDown = false;

    private void Start()
    {
        if (shopUI == null)
            shopUI = FindFirstObjectByType<YataiShopUI>();

        // Check for common physics trap: Script on Parent, Trigger on Child, missing Rigidbody.
        // If this Setup is detected, OnTriggerEnter will NEVER fire.
        bool hasCollider = GetComponent<Collider>() != null;
        if (!hasCollider && GetComponent<Rigidbody>() == null)
        {
            if (debugLog) Debug.LogWarning("[YataiShopInteraction] Warning: Script is on an object without a Rigidbody or Collider. " +
                             "If you rely on a Child Collider, you MUST add a Rigidbody to this GameObject (IsKinematic=true)!");
        }

        // Smart Check: If the indicator has a Collider, triggering SetActive(false) would 
        // kill the trigger physics! In that case, we only hide the visuals (Behaviours).
        if (interactionIndicator != null)
        {
            if (interactionIndicator.GetComponent<Collider>() != null)
            {
                useComponentToggling = true;
                var allBehaviours = interactionIndicator.GetComponents<Behaviour>();
                indicatorBehaviours = allBehaviours;
                
                if (debugLog) Debug.Log($"[YataiShopInteraction] Indicator triggers safely. Toggling {indicatorBehaviours.Length} components.");
            }
            ToggleIndicator(true);
        }
    }

    private void ToggleIndicator(bool show)
    {
        if (interactionIndicator == null) return;

        if (useComponentToggling)
        {
            if (indicatorBehaviours != null)
            {
                foreach (var b in indicatorBehaviours)
                    if (b != null) b.enabled = show;
            }
        }
        else
        {
            interactionIndicator.SetActive(show);
        }
    }

    private IEnumerator CooldownRoutine()
    {
        isCoolingDown = true;
        ToggleIndicator(false);
        
        yield return new WaitForSeconds(cooldownDuration); 
        
        isCoolingDown = false;
        ToggleIndicator(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCoolingDown) return;

        if (debugLog) Debug.Log($"[YataiShopInteraction] Trigger Enter: {other.gameObject.name} (Tag: {other.tag})");

        if (other.CompareTag("Player"))
        {
            if (debugLog) Debug.Log("[YataiShopInteraction] Valid Player detected! Opening Shop...");
            if (shopUI != null)
            {
                shopUI.OpenShop();
                ToggleIndicator(false);
            }
            else
            {
                Debug.LogError("[YataiShopInteraction] YataiShopUI not assigned or found!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (shopUI != null && shopUI.IsOpen)
            {
                shopUI.CloseShop();
            }
            
            StartCoroutine(CooldownRoutine());
        }
    }
}
