using UnityEngine;

/// <summary>
/// Central script for managing all player power-up abilities.
/// Attach to the Player GameObject. Use Context Menu items to debug/test abilities.
/// </summary>
public class PlayerAbilities : MonoBehaviour
{
    #region References
    [Header("Script References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerShooting playerShooting;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PotionSpawner potionSpawner;
    #endregion

    #region Common Abilities (60% - Green)
    [Header("Common Abilities (Green)")]
    [SerializeField, Range(0, 10), Tooltip("Stacks: x1.1 movement speed per stack")]
    private int movementSpeedStacks = 0;

    [SerializeField, Tooltip("Blocks one hit then disappears")]
    private bool hasOneTimeShield = false;

    [SerializeField, Range(0, 10), Tooltip("Stacks: +10% max HP per stack")]
    private int maxHpPlusStacks = 0;

    [SerializeField, Range(0, 3), Tooltip("Stacks: 25% fire damage reduction per stack (max 3 = 75%)")]
    private int hazardResistanceStacks = 0;
    private const int MAX_HAZARD_RESISTANCE_STACKS = 3;
    #endregion

    #region Rare Abilities (30% - Blue)
    [Header("Rare Abilities (Blue)")]
    [SerializeField, Range(0, 3), Tooltip("Stacks: Arrows pierce enemies. Stack 2+ adds +2s lifetime per enemy hit per stack.")]
    private int piercingStacks = 0;
    private const int MAX_PIERCING_STACKS = 3;

    [SerializeField, Range(0, 10), Tooltip("Stacks: +1 bounce per stack (0 = no bouncing)")]
    private int bouncingArrowsStacks = 0;

    [SerializeField, Range(0, 5), Tooltip("Stacks: Arrows freeze enemies. Stack 2+ triggers AOE freeze. Stack 3+=larger radius.")]
    private int freezeShotStacks = 0;

    [SerializeField, Range(0, 5), Tooltip("Stacks: Arrows apply venom DoT. Stack 2+ triggers AOE venom. Stack 3+=larger radius.")]
    private int venomShotStacks = 0;
    private const int MAX_SHOT_STACKS = 5;

    [SerializeField, Range(0, 10), Tooltip("Stacks: +1s freeze duration per stack")]
    private int maximumFreezeStacks = 0;

    [SerializeField, Range(0, 10), Tooltip("Stacks: +100 damage/s per stack")]
    private int maximumVenomStacks = 0;

    [SerializeField, Range(0, 4), Tooltip("Stacks: spawns freeze potions. Each stack reduces spawn interval by 2s (min 1s).")]
    private int freezePotionStacks = 0;

    [SerializeField, Range(0, 4), Tooltip("Stacks: spawns venom potions. Each stack reduces spawn interval by 2s (min 1s).")]
    private int venomPotionStacks = 0;

    [SerializeField, Range(0, 4), Tooltip("Stacks: spawns invulnerability potions. Each stack reduces spawn interval by 2s (min 1s).")]
    private int invulnerabilityPotionStacks = 0;
    private const int MAX_POTION_STACKS = 4;
    #endregion

    #region Legendary Abilities (10% - Red)
    [Header("Legendary Abilities (Red)")]
    [SerializeField, Range(0, 10), Tooltip("Stacks: +1 additional arrow per stack")]
    private int multishotStacks = 0;

    [SerializeField, Range(0, 10), Tooltip("Stacks: +2 arrows per stack (1, 3, 5, 7...)")]
    private int tripleshotStacks = 0;

    [SerializeField, Range(0, 10), Tooltip("Stacks: +30% max HP per stack")]
    private int maxHpPlusPlusStacks = 0;

    [SerializeField, Range(0, 10), Tooltip("Stacks: x1.2 attack speed per stack")]
    private int attackSpeedStacks = 0;
    #endregion

    #region Configuration
    [Header("Configuration")]
    [SerializeField, Tooltip("Base freeze duration in seconds")]
    private float baseFreezeDuration = 1f;

    [SerializeField, Tooltip("Freeze duration added per Maximum Freeze stack")]
    private float freezeDurationPerStack = 1f;

    [SerializeField, Tooltip("Base venom damage per second")]
    private int baseVenomDamage = 100;

    [SerializeField, Tooltip("Venom damage added per Maximum Venom stack")]
    private int venomDamagePerStack = 100;

    [SerializeField, Tooltip("Venom duration in seconds")]
    private float venomDuration = 3f;

    [SerializeField, Tooltip("Triple shot angle spread in degrees")]
    private float tripleShotAngle = 25f;
    #endregion

    #region Calculated Values (Read-only in Inspector)
    [Header("Current Stats (Read-Only)")]
    [SerializeField] private float currentMoveSpeedMultiplier = 1f;
    [SerializeField] private float currentAttackSpeedMultiplier = 1f;
    [SerializeField] private float currentFreezeDuration = 1f;
    [SerializeField] private int currentVenomDamagePerSecond = 100;
    #endregion

    private bool isInitialized = false;

    #region Public Properties
    public bool HasPiercing => piercingStacks > 0;
    public int PiercingStacks => piercingStacks;
    public bool HasBouncingArrows => bouncingArrowsStacks > 0;
    public int BouncingArrowsStacks => bouncingArrowsStacks;
    public bool HasFreezeShot => freezeShotStacks > 0;
    public int FreezeShotStacks => freezeShotStacks;
    public bool HasVenomShot => venomShotStacks > 0;
    public int VenomShotStacks => venomShotStacks;
    public int MultishotStacks => multishotStacks;
    public int TripleshotStacks => tripleshotStacks;
    public bool HasOneTimeShield => hasOneTimeShield;
    public int HazardResistanceStacks => hazardResistanceStacks;
    
    public float FreezeDuration => currentFreezeDuration;
    public int VenomDamagePerSecond => currentVenomDamagePerSecond;
    public float VenomDuration => venomDuration;
    public float TripleShotAngle => tripleShotAngle;
    public bool HasFreezePotionTalent => freezePotionStacks > 0;
    public bool HasVenomPotionTalent => venomPotionStacks > 0;
    public bool HasInvulnerabilityPotionTalent => invulnerabilityPotionStacks > 0;
    public int FreezePotionStacks => freezePotionStacks;
    public int VenomPotionStacks => venomPotionStacks;
    public int InvulnerabilityPotionStacks => invulnerabilityPotionStacks;
    #endregion

    private void Awake()
    {
        // Auto-wire references if not set
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (playerShooting == null)
            playerShooting = GetComponent<PlayerShooting>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (potionSpawner == null)
            potionSpawner = GetComponent<PotionSpawner>();
    }

    private void Start()
    {
        isInitialized = true;
        // Apply initial state only after all scripts have initialized
        RecalculateAllStats();
    }

    private void OnValidate()
    {
        // Only recalculate display values in Inspector, don't apply to scripts during edit mode
        // This prevents breaking other scripts before they're initialized
        currentMoveSpeedMultiplier = Mathf.Pow(1.1f, movementSpeedStacks);
        currentAttackSpeedMultiplier = Mathf.Pow(1.2f, attackSpeedStacks);
        currentFreezeDuration = baseFreezeDuration + (maximumFreezeStacks * freezeDurationPerStack);
        currentVenomDamagePerSecond = baseVenomDamage + (maximumVenomStacks * venomDamagePerStack);
        
        // Only apply to scripts if we're in play mode and initialized
        if (isInitialized && Application.isPlaying)
        {
            ApplyToScripts();
            
            // Sync potion talents with PotionSpawner (handles both enable AND disable)
            if (potionSpawner != null)
            {
                potionSpawner.SyncWithAbilities(freezePotionStacks, venomPotionStacks, invulnerabilityPotionStacks);
            }
        }
    }

    #region Stat Calculation
    private void RecalculateAllStats()
    {
        // Movement Speed: x1.1 per stack
        currentMoveSpeedMultiplier = Mathf.Pow(1.1f, movementSpeedStacks);
        
        // Attack Speed: x1.2 per stack
        currentAttackSpeedMultiplier = Mathf.Pow(1.2f, attackSpeedStacks);
        
        // Freeze Duration: base + stacks
        currentFreezeDuration = baseFreezeDuration + (maximumFreezeStacks * freezeDurationPerStack);
        
        // Venom Damage: base + stacks
        currentVenomDamagePerSecond = baseVenomDamage + (maximumVenomStacks * venomDamagePerStack);

        ApplyToScripts();
    }

    private void ApplyToScripts()
    {
        if (playerMovement != null)
        {
            playerMovement.SetSpeedMultiplier(currentMoveSpeedMultiplier);
        }

        if (playerShooting != null)
        {
            playerShooting.SetTempoMultiplier(currentAttackSpeedMultiplier);
            playerShooting.SetMultishotStacks(multishotStacks);
            playerShooting.SetTripleshotStacks(tripleshotStacks, tripleShotAngle);
        }

        if (playerHealth != null)
        {
            playerHealth.SetOneTimeShield(hasOneTimeShield);
        }
    }
    #endregion

    #region Grant Ability Methods (Called by PowerUp System)
    
    // ===== COMMON =====
    public void GrantMovementSpeed()
    {
        movementSpeedStacks++;
        RecalculateAllStats();
    }

    public void GrantOneTimeShield()
    {
        hasOneTimeShield = true;
        ApplyToScripts();
    }

    public void GrantMaxHpPlus()
    {
        maxHpPlusStacks++;
        if (playerHealth != null)
        {
            playerHealth.IncreaseMaxHealth(0.1f);
        }
    }

    public void GrantHealthHeal()
    {
        if (playerHealth != null)
        {
            // Random heal between 30% and 100% of max HP
            float healPercent = Random.Range(0.30f, 1.00f);
            int healAmount = Mathf.RoundToInt(playerHealth.MaxHealth * healPercent);
            playerHealth.Heal(healAmount);
        }
        else
        {
            Debug.LogWarning("[PlayerAbilities] Health Heal - PlayerHealth is null!");
        }
    }

    public void GrantHazardResistance()
    {
        if (hazardResistanceStacks < MAX_HAZARD_RESISTANCE_STACKS)
        {
            hazardResistanceStacks++;
        }
    }

    // ===== RARE =====
    public void GrantPiercing()
    {
        if (piercingStacks < MAX_PIERCING_STACKS)
            piercingStacks++;
        Debug.Log($"[PlayerAbilities] GrantPiercing: stacks now {piercingStacks}");
    }

    public void GrantBouncingArrows()
    {
        bouncingArrowsStacks++;
    }

    public void GrantFreezeShot()
    {
        if (freezeShotStacks < MAX_SHOT_STACKS)
            freezeShotStacks++;
        Debug.Log($"[PlayerAbilities] GrantFreezeShot: stacks now {freezeShotStacks}");
    }

    public void GrantVenomShot()
    {
        if (venomShotStacks < MAX_SHOT_STACKS)
            venomShotStacks++;
        Debug.Log($"[PlayerAbilities] GrantVenomShot: stacks now {venomShotStacks}");
    }

    public void GrantMaximumFreeze()
    {
        maximumFreezeStacks++;
        RecalculateAllStats();
    }

    public void GrantMaximumVenom()
    {
        maximumVenomStacks++;
        RecalculateAllStats();
    }

    public void GrantFreezePotionTalent()
    {
        int oldStacks = freezePotionStacks;
        if (freezePotionStacks < MAX_POTION_STACKS)
            freezePotionStacks++;
        Debug.Log($"[PlayerAbilities] GrantFreezePotionTalent: {oldStacks} → {freezePotionStacks}, PotionSpawner: {(potionSpawner != null ? "OK" : "NULL")}");
        if (potionSpawner != null)
            potionSpawner.SetFreezePotionStacks(freezePotionStacks);
    }

    public void GrantVenomPotionTalent()
    {
        int oldStacks = venomPotionStacks;
        if (venomPotionStacks < MAX_POTION_STACKS)
            venomPotionStacks++;
        Debug.Log($"[PlayerAbilities] GrantVenomPotionTalent: {oldStacks} → {venomPotionStacks}, PotionSpawner: {(potionSpawner != null ? "OK" : "NULL")}");
        if (potionSpawner != null)
            potionSpawner.SetVenomPotionStacks(venomPotionStacks);
    }

    public void GrantInvulnerabilityPotionTalent()
    {
        int oldStacks = invulnerabilityPotionStacks;
        if (invulnerabilityPotionStacks < MAX_POTION_STACKS)
            invulnerabilityPotionStacks++;
        Debug.Log($"[PlayerAbilities] GrantInvulnerabilityPotionTalent: {oldStacks} → {invulnerabilityPotionStacks}, PotionSpawner: {(potionSpawner != null ? "OK" : "NULL")}");
        if (potionSpawner != null)
            potionSpawner.SetInvulnerabilityPotionStacks(invulnerabilityPotionStacks);
    }

    // ===== LEGENDARY =====
    public void GrantMultishot()
    {
        multishotStacks++;
        ApplyToScripts();
    }

    public void GrantTripleShot()
    {
        tripleshotStacks++;
        ApplyToScripts();
    }

    public void GrantMaxHpPlusPlus()
    {
        maxHpPlusPlusStacks++;
        if (playerHealth != null)
        {
            playerHealth.IncreaseMaxHealth(0.3f);
        }
    }

    public void GrantAttackSpeed()
    {
        attackSpeedStacks++;
        RecalculateAllStats();
    }

    public void GrantInvulnerabilityPotion()
    {
        // TODO: Spawn invulnerability potion in map
        // For debug, apply directly
        if (playerHealth != null)
        {
            playerHealth.SetInvulnerable(2f);
        }
    }
    #endregion

    #region Shield Callback
    public void OnShieldConsumed()
    {
        hasOneTimeShield = false;
    }
    #endregion

    #region Debug Context Menu
    [ContextMenu("Debug: Grant Movement Speed")]
    public void DebugGrantMovementSpeed() => GrantMovementSpeed();

    [ContextMenu("Debug: Grant One-Time Shield")]
    public void DebugGrantOneTimeShield() => GrantOneTimeShield();

    [ContextMenu("Debug: Grant Max HP+ (10%)")]
    public void DebugGrantMaxHpPlus() => GrantMaxHpPlus();

    [ContextMenu("Debug: Grant Hazard Resistance (25%)")]
    public void DebugGrantHazardResistance() => GrantHazardResistance();

    [ContextMenu("Debug: Grant Health Heal")]
    public void DebugGrantHealthHeal() => GrantHealthHeal();

    [ContextMenu("Debug: Grant Piercing")]
    public void DebugGrantPiercing() => GrantPiercing();

    [ContextMenu("Debug: Grant Bouncing Arrows")]
    public void DebugGrantBouncingArrows() => GrantBouncingArrows();

    [ContextMenu("Debug: Grant Freeze Shot")]
    public void DebugGrantFreezeShot() => GrantFreezeShot();

    [ContextMenu("Debug: Grant Venom Shot")]
    public void DebugGrantVenomShot() => GrantVenomShot();

    [ContextMenu("Debug: Grant Maximum Freeze (+1s)")]
    public void DebugGrantMaximumFreeze() => GrantMaximumFreeze();

    [ContextMenu("Debug: Grant Maximum Venom (+100 dmg/s)")]
    public void DebugGrantMaximumVenom() => GrantMaximumVenom();

    [ContextMenu("Debug: Grant Freeze Potion Talent")]
    public void DebugGrantFreezePotionTalent() => GrantFreezePotionTalent();

    [ContextMenu("Debug: Grant Venom Potion Talent")]
    public void DebugGrantVenomPotionTalent() => GrantVenomPotionTalent();

    [ContextMenu("Debug: Grant Invulnerability Potion Talent")]
    public void DebugGrantInvulnerabilityPotionTalent() => GrantInvulnerabilityPotionTalent();

    [ContextMenu("Debug: Grant Multishot")]
    public void DebugGrantMultishot() => GrantMultishot();

    [ContextMenu("Debug: Grant Triple Shot")]
    public void DebugGrantTripleShot() => GrantTripleShot();

    [ContextMenu("Debug: Grant Max HP+++ (30%)")]
    public void DebugGrantMaxHpPlusPlus() => GrantMaxHpPlusPlus();

    [ContextMenu("Debug: Grant Attack Speed+ (x1.2)")]
    public void DebugGrantAttackSpeed() => GrantAttackSpeed();

    [ContextMenu("Debug: Grant Invulnerability (2s)")]
    public void DebugGrantInvulnerability() => GrantInvulnerabilityPotion();

    [ContextMenu("Debug: Reset All Abilities")]
    public void DebugResetAllAbilities()
    {
        movementSpeedStacks = 0;
        hasOneTimeShield = false;
        maxHpPlusStacks = 0;
        hazardResistanceStacks = 0;
        piercingStacks = 0;
        bouncingArrowsStacks = 0;
        freezeShotStacks = 0;
        venomShotStacks = 0;
        maximumFreezeStacks = 0;
        maximumVenomStacks = 0;
        multishotStacks = 0;
        tripleshotStacks = 0;
        maxHpPlusPlusStacks = 0;
        attackSpeedStacks = 0;
        freezePotionStacks = 0;
        venomPotionStacks = 0;
        invulnerabilityPotionStacks = 0;
        if (potionSpawner != null)
            potionSpawner.DisableAllPotions();
        RecalculateAllStats();
    }
    #endregion
}
