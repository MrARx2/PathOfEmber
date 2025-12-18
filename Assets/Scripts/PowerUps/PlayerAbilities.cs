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
    #endregion

    #region Common Abilities (60% - Green)
    [Header("Common Abilities (Green)")]
    [SerializeField, Tooltip("Stacks: x1.1 movement speed per stack")]
    private int movementSpeedStacks = 0;

    [SerializeField, Tooltip("Blocks one hit then disappears")]
    private bool hasOneTimeShield = false;

    [SerializeField, Tooltip("Stacks: +10% max HP per stack")]
    private int maxHpPlusStacks = 0;

    [SerializeField, Tooltip("Stacks: 25% fire damage reduction per stack (max 3 = 75%)")]
    private int hazardResistanceStacks = 0;
    private const int MAX_HAZARD_RESISTANCE_STACKS = 3;
    #endregion

    #region Rare Abilities (30% - Blue)
    [Header("Rare Abilities (Blue)")]
    [SerializeField, Tooltip("Arrows pass through enemies")]
    private bool hasPiercing = false;

    [SerializeField, Tooltip("Arrows bounce off walls")]
    private bool hasBouncingBullets = false;

    [SerializeField, Tooltip("Arrows freeze enemies for 1s")]
    private bool hasFreezeShot = false;

    [SerializeField, Tooltip("Arrows apply poison DoT")]
    private bool hasVenomShot = false;

    [SerializeField, Tooltip("Stacks: +1s freeze duration per stack")]
    private int maximumFreezeStacks = 0;

    [SerializeField, Tooltip("Stacks: +100 damage/s per stack")]
    private int maximumVenomStacks = 0;
    #endregion

    #region Legendary Abilities (10% - Red)
    [Header("Legendary Abilities (Red)")]
    [SerializeField, Tooltip("Fires 2x the current projectile count")]
    private bool hasMultishot = false;

    [SerializeField, Tooltip("Fires 3 projectiles in a spread")]
    private bool hasTripleShot = false;

    [SerializeField, Tooltip("Stacks: +30% max HP per stack")]
    private int maxHpPlusPlusStacks = 0;

    [SerializeField, Tooltip("Stacks: x1.2 attack speed per stack")]
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
    public bool HasPiercing => hasPiercing;
    public bool HasBouncingBullets => hasBouncingBullets;
    public bool HasFreezeShot => hasFreezeShot;
    public bool HasVenomShot => hasVenomShot;
    public bool HasMultishot => hasMultishot;
    public bool HasTripleShot => hasTripleShot;
    public bool HasOneTimeShield => hasOneTimeShield;
    public int HazardResistanceStacks => hazardResistanceStacks;
    
    public float FreezeDuration => currentFreezeDuration;
    public int VenomDamagePerSecond => currentVenomDamagePerSecond;
    public float VenomDuration => venomDuration;
    public float TripleShotAngle => tripleShotAngle;
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
    }

    private void Start()
    {
        isInitialized = true;
        Debug.Log($"[PlayerAbilities] Start: Applying initial stats with moveMultiplier={currentMoveSpeedMultiplier}, attackMultiplier={currentAttackSpeedMultiplier}");
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
            playerShooting.SetMultishotEnabled(hasMultishot);
            playerShooting.SetTripleShotEnabled(hasTripleShot, tripleShotAngle);
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
        Debug.Log($"[PlayerAbilities] Movement Speed granted. Total stacks: {movementSpeedStacks}, Multiplier: {currentMoveSpeedMultiplier:F2}x");
    }

    public void GrantOneTimeShield()
    {
        hasOneTimeShield = true;
        ApplyToScripts();
        Debug.Log("[PlayerAbilities] One-Time Shield granted.");
    }

    public void GrantMaxHpPlus()
    {
        maxHpPlusStacks++;
        if (playerHealth != null)
        {
            playerHealth.IncreaseMaxHealth(0.1f);
        }
        Debug.Log($"[PlayerAbilities] Max HP+ granted. Total stacks: {maxHpPlusStacks}");
    }

    public void GrantHealthHeal()
    {
        if (playerHealth != null)
        {
            // Heal 20% of max HP
            int healAmount = Mathf.RoundToInt(playerHealth.MaxHealth * 0.2f);
            playerHealth.Heal(healAmount);
        }
        Debug.Log("[PlayerAbilities] Health Heal granted.");
    }

    public void GrantHazardResistance()
    {
        if (hazardResistanceStacks < MAX_HAZARD_RESISTANCE_STACKS)
        {
            hazardResistanceStacks++;
            float reduction = hazardResistanceStacks * 25f;
            Debug.Log($"[PlayerAbilities] Hazard Resistance granted. Stacks: {hazardResistanceStacks}/{MAX_HAZARD_RESISTANCE_STACKS}, Fire damage reduced by {reduction}%");
        }
        else
        {
            Debug.Log("[PlayerAbilities] Hazard Resistance already at max stacks!");
        }
    }

    // ===== RARE =====
    public void GrantPiercing()
    {
        hasPiercing = true;
        Debug.Log("[PlayerAbilities] Piercing granted.");
    }

    public void GrantBouncingBullets()
    {
        hasBouncingBullets = true;
        Debug.Log("[PlayerAbilities] Bouncing Bullets granted.");
    }

    public void GrantFreezeShot()
    {
        hasFreezeShot = true;
        Debug.Log("[PlayerAbilities] Freeze Shot granted.");
    }

    public void GrantVenomShot()
    {
        hasVenomShot = true;
        Debug.Log("[PlayerAbilities] Venom Shot granted.");
    }

    public void GrantMaximumFreeze()
    {
        maximumFreezeStacks++;
        RecalculateAllStats();
        Debug.Log($"[PlayerAbilities] Maximum Freeze granted. Total stacks: {maximumFreezeStacks}, Duration: {currentFreezeDuration}s");
    }

    public void GrantMaximumVenom()
    {
        maximumVenomStacks++;
        RecalculateAllStats();
        Debug.Log($"[PlayerAbilities] Maximum Venom granted. Total stacks: {maximumVenomStacks}, Damage: {currentVenomDamagePerSecond}/s");
    }

    // ===== LEGENDARY =====
    public void GrantMultishot()
    {
        hasMultishot = true;
        ApplyToScripts();
        Debug.Log("[PlayerAbilities] Multishot granted.");
    }

    public void GrantTripleShot()
    {
        hasTripleShot = true;
        ApplyToScripts();
        Debug.Log("[PlayerAbilities] Triple Shot granted.");
    }

    public void GrantMaxHpPlusPlus()
    {
        maxHpPlusPlusStacks++;
        if (playerHealth != null)
        {
            playerHealth.IncreaseMaxHealth(0.3f);
        }
        Debug.Log($"[PlayerAbilities] Max HP+++ granted. Total stacks: {maxHpPlusPlusStacks}");
    }

    public void GrantAttackSpeed()
    {
        attackSpeedStacks++;
        RecalculateAllStats();
        Debug.Log($"[PlayerAbilities] Attack Speed+ granted. Total stacks: {attackSpeedStacks}, Multiplier: {currentAttackSpeedMultiplier:F2}x");
    }

    public void GrantInvulnerabilityPotion()
    {
        // TODO: Spawn invulnerability potion in map
        // For debug, apply directly
        if (playerHealth != null)
        {
            playerHealth.SetInvulnerable(2f);
        }
        Debug.Log("[PlayerAbilities] Invulnerability Potion granted (applied directly for debug).");
    }
    #endregion

    #region Shield Callback
    public void OnShieldConsumed()
    {
        hasOneTimeShield = false;
        Debug.Log("[PlayerAbilities] One-Time Shield consumed!");
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

    [ContextMenu("Debug: Grant Bouncing Bullets")]
    public void DebugGrantBouncingBullets() => GrantBouncingBullets();

    [ContextMenu("Debug: Grant Freeze Shot")]
    public void DebugGrantFreezeShot() => GrantFreezeShot();

    [ContextMenu("Debug: Grant Venom Shot")]
    public void DebugGrantVenomShot() => GrantVenomShot();

    [ContextMenu("Debug: Grant Maximum Freeze (+1s)")]
    public void DebugGrantMaximumFreeze() => GrantMaximumFreeze();

    [ContextMenu("Debug: Grant Maximum Venom (+100 dmg/s)")]
    public void DebugGrantMaximumVenom() => GrantMaximumVenom();

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
        hasPiercing = false;
        hasBouncingBullets = false;
        hasFreezeShot = false;
        hasVenomShot = false;
        maximumFreezeStacks = 0;
        maximumVenomStacks = 0;
        hasMultishot = false;
        hasTripleShot = false;
        maxHpPlusPlusStacks = 0;
        attackSpeedStacks = 0;
        RecalculateAllStats();
        Debug.Log("[PlayerAbilities] All abilities reset.");
    }
    #endregion
}
