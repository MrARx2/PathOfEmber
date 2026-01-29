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

    private Behaviour[] indicatorBehaviours;
    private bool useComponentToggling = false;

    private void Start()
    {
        if (shopUI == null)
            shopUI = FindFirstObjectByType<YataiShopUI>();

        // Check for common physics trap: Script on Parent, Trigger on Child, missing Rigidbody.
        // If this Setup is detected, OnTriggerEnter will NEVER fire.
        bool hasCollider = GetComponent<Collider>() != null;
        if (!hasCollider && GetComponent<Rigidbody>() == null)
        {
            // If we don't have a collider or RB, but we expect to trigger...
            // It's fine if the Child passes the event up, BUT that requires a Rigidbody here!
            Debug.LogWarning("[YataiShopInteraction] Warning: Script is on an object without a Rigidbody or Collider. " +
                             "If you rely on a Child Collider, you MUST add a Rigidbody to this GameObject (IsKinematic=true)!");
        }

        // Smart Check: If the indicator has a Collider, triggering SetActive(false) would 
        // kill the trigger physics! In that case, we only hide the visuals (Behaviours).
        if (interactionIndicator != null)
        {
            if (interactionIndicator.GetComponent<Collider>() != null)
            {
                useComponentToggling = true;
                // Get all enabled behaviours (Renderers, Decals, etc)
                // We exclude Colliders so we don't disable the trigger!
                var allBehaviours = interactionIndicator.GetComponents<Behaviour>();
                System.Collections.Generic.List<Behaviour> visualBehaviours = new System.Collections.Generic.List<Behaviour>();
                
                foreach(var b in allBehaviours)
                {
                    if (b is Collider) continue; // Strings attached!
                    if (b is Transform) continue; // Can't disable transform
                    visualBehaviours.Add(b);
                }
                indicatorBehaviours = visualBehaviours.ToArray();
                
                Debug.Log($"[YataiShopInteraction] Indicator has a Collider. Toggling {indicatorBehaviours.Length} components (Decals/Renderers) instead of GameObject.");
            }
            ToggleIndicator(true);
        }
    }

    private void ToggleIndicator(bool show)
    {
        if (interactionIndicator == null) return;

        if (useComponentToggling)
        {
            // Toggle Visual Components only
            if (indicatorBehaviours != null)
            {
                foreach (var b in indicatorBehaviours)
                    if (b != null) b.enabled = show;
            }
        }
        else
        {
            // Safe to toggle entire object
            interactionIndicator.SetActive(show);
        }
    }

    [Header("Settings")]
    [SerializeField, Tooltip("Delay before the shop interaction becomes available again after closing")]
    private float cooldownDuration = 1.0f;

    private bool isCoolingDown = false;
    private IEnumerator CooldownRoutine()
    {
        isCoolingDown = true;
        // Keep indicator hidden while cooling down
        ToggleIndicator(false);
        
        yield return new WaitForSeconds(cooldownDuration); 
        
        isCoolingDown = false;
        // Show indicator only when ready again
        ToggleIndicator(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCoolingDown) return;

        // Debug: Check if collision is detected at all
        Debug.Log($"[YataiShopInteraction] Trigger Enter: {other.gameObject.name} (Tag: {other.tag})");

        if (other.CompareTag("Player"))
        {
            Debug.Log("[YataiShopInteraction] Valid Player detected! Opening Shop...");
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
            
            // Start cooldown (keeps indicator hidden for duration)
            StartCoroutine(CooldownRoutine());
        }
    }
}
