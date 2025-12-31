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
            xpSystem = FindObjectOfType<XPSystem>();
        
        if (prayerWheelUI == null)
            prayerWheelUI = FindObjectOfType<PrayerWheelUI>();
        
        if (prayerWheelController == null)
            prayerWheelController = FindObjectOfType<PrayerWheelController>();

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
        }

        if (prayerWheelUI != null)
        {
            prayerWheelUI.OnTalentSelected.AddListener(OnTalentSelected);
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
            // Common Talents
            { "MovementSpeed", () => playerAbilities?.GrantMovementSpeed() },
            { "OneTimeShield", () => playerAbilities?.GrantOneTimeShield() },
            { "MaxHpPlus", () => playerAbilities?.GrantMaxHpPlus() },
            { "HealthHeal", () => playerAbilities?.GrantHealthHeal() },
            { "HazardResistance", () => playerAbilities?.GrantHazardResistance() },
            { "FreezePotion", () => SpawnPotion("Freeze") },
            { "VenomPotion", () => SpawnPotion("Venom") },

            // Rare Talents
            { "Piercing", () => playerAbilities?.GrantPiercing() },
            { "BouncingBullets", () => playerAbilities?.GrantBouncingArrows() },
            { "Ricochet", () => playerAbilities?.GrantBouncingArrows() }, // Alias
            { "FreezeShot", () => playerAbilities?.GrantFreezeShot() },
            { "VenomShot", () => playerAbilities?.GrantVenomShot() },
            { "MaximumFreeze", () => playerAbilities?.GrantMaximumFreeze() },
            { "MaxFreeze", () => playerAbilities?.GrantMaximumFreeze() }, // Alias
            { "MaximumVenom", () => playerAbilities?.GrantMaximumVenom() },
            { "MaxVenom", () => playerAbilities?.GrantMaximumVenom() }, // Alias

            // Legendary Talents
            { "Multishot", () => playerAbilities?.GrantMultishot() },
            { "MultiShot", () => playerAbilities?.GrantMultishot() }, // Alias
            { "TripleShot", () => playerAbilities?.GrantTripleShot() },
            { "MaxHpPlusPlus", () => playerAbilities?.GrantMaxHpPlusPlus() },
            { "MaxHP+++", () => playerAbilities?.GrantMaxHpPlusPlus() }, // Alias
            { "AttackSpeed", () => playerAbilities?.GrantAttackSpeed() },
            { "InvulnerabilityPotion", () => playerAbilities?.GrantInvulnerabilityPotion() },
            { "InvulnerabilityPotion2S", () => playerAbilities?.GrantInvulnerabilityPotion() }, // Alias
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

        Debug.Log($"[TalentSelectionManager] Applying talent: {talent.talentName} (ID: {talent.talentId})");

        if (playerAbilities == null)
        {
            Debug.LogError("[TalentSelectionManager] PlayerAbilities not found! Cannot apply talent.");
            return;
        }

        // Look up and execute the talent action
        if (talentActions.TryGetValue(talent.talentId, out System.Action action))
        {
            action.Invoke();
            Debug.Log($"[TalentSelectionManager] Talent '{talent.talentName}' applied successfully!");
        }
        else
        {
            Debug.LogWarning($"[TalentSelectionManager] No action found for talent ID: {talent.talentId}");
        }
    }

    private void SpawnPotion(string potionType)
    {
        // TODO: Implement potion spawning near player
        Debug.Log($"[TalentSelectionManager] Would spawn {potionType} potion near player");
        
        // For now, apply effect directly
        if (potionType == "Freeze" && playerAbilities != null)
        {
            playerAbilities.GrantFreezeShot();
        }
        else if (potionType == "Venom" && playerAbilities != null)
        {
            playerAbilities.GrantVenomShot();
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
