using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Coordinates the talent selection system between XP, Prayer Wheel, and PlayerAbilities.
/// </summary>
public class TalentSelectionManager : MonoBehaviour
{
    public static TalentSelectionManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private XPSystem xpSystem;
    [SerializeField] private PrayerWheelUI prayerWheelUI;
    [SerializeField] private PrayerWheelController prayerWheelController;
    [SerializeField] private PlayerAbilities playerAbilities;

    [Header("Configuration")]
    [SerializeField, Tooltip("If true, finds PlayerAbilities on tagged 'Player' object")]
    private bool autoFindPlayer = true;

    private Dictionary<string, System.Action> talentActions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupTalentActions();
    }

    private void Start()
    {
        // Auto-find references if not set
        if (xpSystem == null)
            xpSystem = FindFirstObjectByType<XPSystem>();
        
        if (prayerWheelUI == null)
            prayerWheelUI = FindFirstObjectByType<PrayerWheelUI>();
        
        if (prayerWheelController == null)
            prayerWheelController = FindFirstObjectByType<PrayerWheelController>();

        if (autoFindPlayer && playerAbilities == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerAbilities = player.GetComponent<PlayerAbilities>();
            }
        }

        // Subscribe to events
        if (xpSystem != null)
        {
            xpSystem.OnXPFilled.AddListener(OnXPFilled);
            Debug.Log("[TalentSelectionManager] Subscribed to XPSystem.OnXPFilled");
        }
        else
        {
            Debug.LogWarning("[TalentSelectionManager] XPSystem is NULL - cannot subscribe!");
        }

        if (prayerWheelUI != null)
        {
            prayerWheelUI.OnTalentSelected.AddListener(OnTalentSelected);
            Debug.Log("[TalentSelectionManager] Subscribed to PrayerWheelUI.OnTalentSelected");
        }
        else
        {
            Debug.LogError("[TalentSelectionManager] PrayerWheelUI is NULL - TALENTS WILL NOT BE APPLIED!");
        }

        if (playerAbilities != null)
        {
            Debug.Log($"[TalentSelectionManager] PlayerAbilities found on: {playerAbilities.gameObject.name}");
        }
        else
        {
            Debug.LogError("[TalentSelectionManager] PlayerAbilities is NULL - CANNOT APPLY TALENTS!");
        }
    }

    private void OnDestroy()
    {
        if (xpSystem != null)
        {
            xpSystem.OnXPFilled.RemoveListener(OnXPFilled);
        }

        if (prayerWheelUI != null)
        {
            prayerWheelUI.OnTalentSelected.RemoveListener(OnTalentSelected);
        }
    }

    private void SetupTalentActions()
    {
        talentActions = new Dictionary<string, System.Action>
        {
            // ===== COMMON TALENTS =====
            { "MovementSpeed", () => playerAbilities?.GrantMovementSpeed() },
            { "Speed", () => playerAbilities?.GrantMovementSpeed() }, // Alias
            
            { "OneTimeShield", () => playerAbilities?.GrantOneTimeShield() },
            { "Shield", () => playerAbilities?.GrantOneTimeShield() }, // Alias
            
            { "MaxHpPlus", () => playerAbilities?.GrantMaxHpPlus() },
            { "MaxHP+", () => playerAbilities?.GrantMaxHpPlus() }, // Alias
            { "HealthPlus", () => playerAbilities?.GrantMaxHpPlus() }, // Alias
            
            { "HealthHeal", () => playerAbilities?.GrantHealthHeal() },
            { "Heal", () => playerAbilities?.GrantHealthHeal() }, // Alias
            
            { "HazardResistance", () => playerAbilities?.GrantHazardResistance() },
            { "FireResistance", () => playerAbilities?.GrantHazardResistance() }, // Alias

            // ===== RARE TALENTS =====
            { "Piercing", () => playerAbilities?.GrantPiercing() },
            { "Pierce", () => playerAbilities?.GrantPiercing() }, // Alias
            
            { "BouncingBullets", () => playerAbilities?.GrantBouncingArrows() },
            { "BouncingArrows", () => playerAbilities?.GrantBouncingArrows() }, // Alias
            { "Ricochet", () => playerAbilities?.GrantBouncingArrows() }, // Alias
            { "Bounce", () => playerAbilities?.GrantBouncingArrows() }, // Alias
            
            { "FreezeShot", () => playerAbilities?.GrantFreezeShot() },
            { "Freeze", () => playerAbilities?.GrantFreezeShot() }, // Alias
            
            { "VenomShot", () => playerAbilities?.GrantVenomShot() },
            { "Venom", () => playerAbilities?.GrantVenomShot() }, // Alias
            { "Poison", () => playerAbilities?.GrantVenomShot() }, // Alias
            
            { "MaximumFreeze", () => playerAbilities?.GrantMaximumFreeze() },
            { "MaxFreeze", () => playerAbilities?.GrantMaximumFreeze() }, // Alias
            
            { "MaximumVenom", () => playerAbilities?.GrantMaximumVenom() },
            { "MaxVenom", () => playerAbilities?.GrantMaximumVenom() }, // Alias

            // ===== POTION TALENTS (enable spawning system) =====
            { "FreezePotionTalent", () => playerAbilities?.GrantFreezePotionTalent() },
            { "FreezePotion", () => playerAbilities?.GrantFreezePotionTalent() }, // Alias
            
            { "VenomPotionTalent", () => playerAbilities?.GrantVenomPotionTalent() },
            { "VenomPotion", () => playerAbilities?.GrantVenomPotionTalent() }, // Alias
            { "PoisonPotion", () => playerAbilities?.GrantVenomPotionTalent() }, // Alias
            
            { "InvulnerabilityPotionTalent", () => playerAbilities?.GrantInvulnerabilityPotionTalent() },
            { "InvulnerabilityPotion", () => playerAbilities?.GrantInvulnerabilityPotionTalent() }, // FIXED: was calling wrong method!
            { "InvulnPotion", () => playerAbilities?.GrantInvulnerabilityPotionTalent() }, // Alias
            { "InvulnerabilityPotion2S", () => playerAbilities?.GrantInvulnerabilityPotionTalent() }, // Alias

            // ===== LEGENDARY TALENTS =====
            { "Multishot", () => playerAbilities?.GrantMultishot() },
            { "MultiShot", () => playerAbilities?.GrantMultishot() }, // Alias
            { "DoubleShot", () => playerAbilities?.GrantMultishot() }, // Alias
            
            { "TripleShot", () => playerAbilities?.GrantTripleShot() },
            { "Triple", () => playerAbilities?.GrantTripleShot() }, // Alias
            
            { "MaxHpPlusPlus", () => playerAbilities?.GrantMaxHpPlusPlus() },
            { "MaxHP+++", () => playerAbilities?.GrantMaxHpPlusPlus() }, // Alias
            { "MaxHPPlusPlus", () => playerAbilities?.GrantMaxHpPlusPlus() }, // Alias
            
            { "AttackSpeed", () => playerAbilities?.GrantAttackSpeed() },
            { "AttackSpeedPlus", () => playerAbilities?.GrantAttackSpeed() }, // Alias
            { "FireRate", () => playerAbilities?.GrantAttackSpeed() }, // Alias - matches FireRate.asset
            { "FireRate+", () => playerAbilities?.GrantAttackSpeed() }, // Alias
        };
    }

    private void OnXPFilled()
    {
        Debug.Log("[TalentSelectionManager] XP filled - triggering prayer wheel!");
        
        if (prayerWheelUI != null)
        {
            prayerWheelUI.ShowAndSpin();
        }
        else
        {
            Debug.LogError("[TalentSelectionManager] PrayerWheelUI not found!");
        }
    }

    private void OnTalentSelected(TalentData talent)
    {
        if (talent == null)
        {
            Debug.LogWarning("[TalentSelectionManager] Null talent selected!");
            return;
        }

        // Trim whitespace/tabs from talentId to prevent data entry issues
        string talentId = talent.talentId?.Trim() ?? "";
        
        Debug.Log($"[TalentSelectionManager] Applying talent: {talent.talentName} (ID: '{talentId}')");

        if (playerAbilities == null)
        {
            Debug.LogError("[TalentSelectionManager] PlayerAbilities not found! Cannot apply talent.");
            return;
        }

        // Look up and execute the talent action
        if (talentActions.TryGetValue(talentId, out System.Action action))
        {
            action.Invoke();
            Debug.Log($"[TalentSelectionManager] Talent '{talent.talentName}' applied successfully!");
        }
        else
        {
            Debug.LogWarning($"[TalentSelectionManager] No action found for talent ID: {talent.talentId}");
        }
    }

    /// <summary>
    /// Manually triggers the prayer wheel (for testing).
    /// </summary>
    public void TriggerPrayerWheel()
    {
        OnXPFilled();
    }

    #region Debug
    [ContextMenu("Debug: Trigger Prayer Wheel")]
    public void DebugTriggerPrayerWheel() => TriggerPrayerWheel();

    [ContextMenu("Debug: Add 100 XP")]
    public void DebugAddXP()
    {
        if (xpSystem != null)
        {
            xpSystem.AddXP(100);
        }
    }
    #endregion
}
