using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Audio;

/// <summary>
/// Controls the dual prayer wheel spinning mechanism.
/// Each wheel has 3 floors (Common, Rare, Legendary) with 5 sockets each.
/// </summary>
public class PrayerWheelController : MonoBehaviour
{
    [System.Serializable]
    public class WheelFloor
    {
        [Tooltip("5 socket Image components for talent icons")]
        public Image[] sockets = new Image[5];
        
        [Tooltip("Material to tint during spin")]
        public Material floorMaterial;
    }

    [System.Serializable]
    public class PrayerWheel
    {
        public string wheelName;
        
        [Tooltip("Root transform of the wheel (rotates on Y axis)")]
        public Transform wheelRoot;
        
        [Tooltip("Common floor sockets (C1-C5)")]
        public WheelFloor commonFloor;
        
        [Tooltip("Rare floor sockets (R1-R5)")]
        public WheelFloor rareFloor;
        
        [Tooltip("Legendary floor sockets (L1-L5)")]
        public WheelFloor legendaryFloor;

        public WheelFloor GetFloor(TalentData.TalentRarity rarity)
        {
            return rarity switch
            {
                TalentData.TalentRarity.Common => commonFloor,
                TalentData.TalentRarity.Rare => rareFloor,
                TalentData.TalentRarity.Legendary => legendaryFloor,
                _ => commonFloor
            };
        }
    }

    [Header("Wheel References")]
    [SerializeField] private PrayerWheel wheel1;
    [SerializeField] private PrayerWheel wheel2;
    
    [Header("Auto-Setup (Optional)")]
    [SerializeField, Tooltip("If assigned, will auto-wire wheel1 from this setup")]
    private PrayerWheelSetup wheel1Setup;
    [SerializeField, Tooltip("If assigned, will auto-wire wheel2 from this setup")]
    private PrayerWheelSetup wheel2Setup;

    [Header("Talent Database")]
    [SerializeField] private TalentDatabase talentDatabase;

    [Header("Text Panel Center (for UI tracking)")]
    [SerializeField, Tooltip("Transform at the center between both wheels (used to position talent name text)")]
    private Transform wheelCenterPoint;

    [Header("Spin Configuration")]
    [SerializeField, Tooltip("Total duration of the spin animation in seconds")]
    private float spinDuration = 3f;
    
    [SerializeField, Tooltip("Speed curve over normalized time (0-1). Y-axis is speed multiplier.")]
    private AnimationCurve speedRamp = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Header("Tint Colors")]
    [SerializeField] private Color commonTintColor = Color.red;
    [SerializeField] private Color rareTintColor = Color.blue;
    [SerializeField] private Color legendaryTintColor = Color.yellow;
    [SerializeField] private string tintPropertyName = "_EmissionColor";

    [Header("Sound Events (Layered by Rarity)")]
    [SerializeField, Tooltip("Common spin sound (always plays)")]
    private SoundEvent spinSoundCommon;
    [SerializeField, Tooltip("Rare spin layer (adds to Common for Rare/Legendary)")]
    private SoundEvent spinSoundRare;
    [SerializeField, Tooltip("Legendary spin layer (adds to Common+Rare for Legendary)")]
    private SoundEvent spinSoundLegendary;
    
    [Space(5)]
    [SerializeField, Tooltip("Common stop sound (always plays)")]
    private SoundEvent stopSoundCommon;
    [SerializeField, Tooltip("Rare stop layer (adds to Common for Rare/Legendary)")]
    private SoundEvent stopSoundRare;
    [SerializeField, Tooltip("Legendary stop layer (adds to Common+Rare for Legendary)")]
    private SoundEvent stopSoundLegendary;
    
    [Space(5)]
    [SerializeField, Tooltip("Single sound that plays when spin ends (in addition to layered stop sounds)")]
    private SoundEvent spinEndSound;

    [Space(5)]
    [SerializeField, Tooltip("Spin noise that plays once at the start of every spin")]
    private SoundEvent spinNoiseSound;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    // Assigned talents for current spin (no repetition)
    private TalentData[,] wheel1Talents = new TalentData[3, 5]; // [rarity, socket]
    private TalentData[,] wheel2Talents = new TalentData[3, 5];

    // Current spin state
    private TalentData.TalentRarity currentRarity;
    private int currentSocketIndex; // 0-4
    private TalentData chosenTalent1;
    private TalentData chosenTalent2;
    private bool isSpinning = false;
    private bool talentsPrepared = false; // Tracks if talents were already assigned via PrepareTalents()

    // Cached audio sources for tier sounds (for volume control during spin)
    private AudioSource commonSpinSource;
    private AudioSource rareSpinSource;
    private AudioSource legendarySpinSource;

    // Track original colors at class level to restore them if disabled
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();

    // Optimization: Lookup Table for integrated progress
    private float[] _progressLUT;
    private const int LUT_RESOLUTION = 100; // 100 samples is plenty for UI

    // Events
    public event System.Action<TalentData, TalentData> OnSpinComplete;

    public bool IsSpinning => isSpinning;
    public TalentData ChosenTalent1 => chosenTalent1;
    public TalentData ChosenTalent2 => chosenTalent2;

    private void Awake()
    {
        // Initialize speed ramp if not set
        if (speedRamp == null || speedRamp.keys.Length == 0)
        {
            speedRamp = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0f)
            );
        }

        // Optimization: Pre-calculate integration curve into LUT
        PreCalculateProgressLUT();

        // Auto-wire from PrayerWheelSetup if assigned
        AutoWireFromSetup();
    }

    private void OnDisable()
    {
        if (isSpinning)
        {
            if (debugLog) Debug.Log("[PrayerWheelController] OnDisable called while spinning - Resetting state and colors.");
            
            // Stop logic
            isSpinning = false;
            StopAllCoroutines();
            
            // Stop sound
            StopLayeredSpinSounds();
            
            // Reset colors
            if (originalColors != null && originalColors.Count > 0)
            {
                ResetMaterialColors(originalColors);
                originalColors.Clear();
            }
            
            // Unmute camera shake if we muted it
            if (CameraShakeManager.Instance != null)
                CameraShakeManager.MuteShakes(false);
        }
    }

    /// <summary>
    /// Auto-wires wheel data from PrayerWheelSetup components.
    /// </summary>
    [ContextMenu("Auto-Wire From Setup")]
    public void AutoWireFromSetup()
    {
        if (wheel1Setup != null)
        {
            wheel1.wheelRoot = wheel1Setup.transform;
            wheel1.commonFloor = new WheelFloor { sockets = wheel1Setup.CommonImages, floorMaterial = wheel1Setup.commonMaterial };
            wheel1.rareFloor = new WheelFloor { sockets = wheel1Setup.RareImages, floorMaterial = wheel1Setup.rareMaterial };
            wheel1.legendaryFloor = new WheelFloor { sockets = wheel1Setup.LegendaryImages, floorMaterial = wheel1Setup.legendaryMaterial };
            if (debugLog) Debug.Log("[PrayerWheelController] Auto-wired wheel1 from setup");
        }

        if (wheel2Setup != null)
        {
            wheel2.wheelRoot = wheel2Setup.transform;
            wheel2.commonFloor = new WheelFloor { sockets = wheel2Setup.CommonImages, floorMaterial = wheel2Setup.commonMaterial };
            wheel2.rareFloor = new WheelFloor { sockets = wheel2Setup.RareImages, floorMaterial = wheel2Setup.rareMaterial };
            wheel2.legendaryFloor = new WheelFloor { sockets = wheel2Setup.LegendaryImages, floorMaterial = wheel2Setup.legendaryMaterial };
            if (debugLog) Debug.Log("[PrayerWheelController] Auto-wired wheel2 from setup");
        }
    }

    /// <summary>
    /// Starts the prayer wheel spin sequence.
    /// </summary>
    public void StartSpin()
    {
        if (isSpinning)
        {
            if (debugLog) Debug.LogWarning("[PrayerWheelController] Already spinning!");
            return;
        }

        if (talentDatabase == null)
        {
            if (debugLog) Debug.LogError("[PrayerWheelController] TalentDatabase not assigned!");
            return;
        }

        StartCoroutine(SpinSequence());
    }

    [Header("Debug")]
    [SerializeField] private bool useDebugRarity = false;
    [SerializeField] private TalentData.TalentRarity debugRarityOverride = TalentData.TalentRarity.Legendary;

    /// <summary>
    /// Forces the next spin to use a specific rarity (used by Yatai Shop).
    /// </summary>
    public void SetGuaranteedRarity(TalentData.TalentRarity rarity)
    {
        useDebugRarity = true;
        debugRarityOverride = rarity;
        if (debugLog) Debug.Log($"[PrayerWheelController] Guaranteed Rarity Set: {rarity} for next spin.");
    }

    /// <summary>
    /// Clears the guaranteed rarity (call after spin).
    /// </summary>
    public void ClearGuaranteedRarity()
    {
        useDebugRarity = false;
    }

    // ... (existing code)

    [Header("Alignment Calibration")]
    [SerializeField, Tooltip("Adjust if the wheel stops between icons. (Try 36 or -36)")]
    private float calibrationOffset = 0f;
    
    [SerializeField, Tooltip("Check if the calibration logic seems backwards")]
    private bool reverseSpin = false;

    // ... (existing helper methods) ...

    private IEnumerator SpinSequence()
    {
        isSpinning = true;
        
        // Mute camera shake so the wheel is easy to see
        if (CameraShakeManager.Instance != null)
            CameraShakeManager.MuteShakes(true);

        // Step 1: Roll rarity
        if (useDebugRarity)
        {
            currentRarity = debugRarityOverride;
        }
        else
        {
            currentRarity = TalentDatabase.RollRarity();
        }
        
        // ... (logging) ...

        // Step 2: Assign talents (only if not already prepared)
        if (!talentsPrepared)
        {
            AssignTalentsToWheels();
        }
        else
        {
            if (debugLog) Debug.Log("[PrayerWheelController] Using pre-assigned talents.");
        }
        talentsPrepared = false; // Reset for next spin cycle

        // Step 3: Pick random socket
        currentSocketIndex = Random.Range(0, 5);
        if (debugLog) Debug.Log($"[PrayerWheelController] Socket chosen: {currentSocketIndex + 1}");

        // Step 4: Calculate target rotation intelligently
        float anglePerSocket = 72f; 
        
        // Apply direction multiplier
        float direction = reverseSpin ? -1f : 1f;
        
        // Target angle calculation with Calibration Offset
        float targetSocketAngle = (currentSocketIndex * anglePerSocket * direction) + calibrationOffset;
        
        // Get current rotation (normalized 0-360)
        float currentY = wheel1.wheelRoot.localEulerAngles.y;

        // Calculate minimum target (at least 2 full spins = 720)
        float minSpin = 720f * direction;
        float rawTarget = currentY + minSpin;

        // Adjust rawTarget to align with targetSocketAngle
        float remainder = rawTarget % 360f;
        float discrepancy = targetSocketAngle - remainder;
        
        // Normalize discrepancy to shortest path
        if (discrepancy > 180) discrepancy -= 360;
        if (discrepancy < -180) discrepancy += 360;
        
        // Force positive spin if not reversing (or negative if reversing) to ensure momentum
        if (!reverseSpin && discrepancy < 0) discrepancy += 360;
        if (reverseSpin && discrepancy > 0) discrepancy -= 360;

        float finalTargetRotation = rawTarget + discrepancy;
        
        float rotationAmountToAdd = finalTargetRotation - currentY;
        
        if (debugLog) Debug.Log($"[PrayerWheelController] Target: {targetSocketAngle}°, ToAdd: {rotationAmountToAdd:F1}° (Offset: {calibrationOffset})");

        // Step 5: Store original material colors for reset
        // Use class member so we can restore if disabled mid-spin in OnDisable
        originalColors.Clear();
        StoreMaterialColors(wheel1, originalColors);
        StoreMaterialColors(wheel2, originalColors);

        // EXTRA: Force-disable emission at start
        DisableEmissionForWheel(wheel1);
        DisableEmissionForWheel(wheel2);

        // Play spin noise once at start
        if (spinNoiseSound != null && AudioManager.Instance != null)
            AudioManager.Instance.Play(spinNoiseSound);

        // Step 6: Play layered spin sounds based on rarity
        PlayLayeredSpinSounds();

        // Step 7: Animate spin
        yield return StartCoroutine(AnimateSpin(rotationAmountToAdd, originalColors));

        // Step 8: Finalize - Stop spin sounds and play stop sounds
        StopLayeredSpinSounds();
        PlayLayeredStopSounds();
        
        // Play unified spin end sound
        if (spinEndSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(spinEndSound);
            if (debugLog) Debug.Log("[PrayerWheelController] Spin end sound played");
        }

        // Determine winners
        int rarityIndex = 0;
        switch (currentRarity)
        {
            case TalentData.TalentRarity.Common: rarityIndex = 0; break;
            case TalentData.TalentRarity.Rare: rarityIndex = 1; break;
            case TalentData.TalentRarity.Legendary: rarityIndex = 2; break;
        }

        chosenTalent1 = wheel1Talents[rarityIndex, currentSocketIndex];
        chosenTalent2 = wheel2Talents[rarityIndex, currentSocketIndex];

        isSpinning = false;
        
        // Unmute camera shake
        if (CameraShakeManager.Instance != null)
            CameraShakeManager.MuteShakes(false);

        if (debugLog)
        {
            Debug.Log("======================================");
            Debug.Log($"[PrayerWheelController] SPIN COMPLETE!");
            Debug.Log($"[PrayerWheelController] Rarity: {currentRarity}");
            Debug.Log($"[PrayerWheelController] Selected Socket Index: {currentSocketIndex}");
            Debug.Log($"[PrayerWheelController] <b>Wheel 1 Reward:</b> {(chosenTalent1 != null ? chosenTalent1.talentName : "NULL")}");
            Debug.Log($"[PrayerWheelController] <b>Wheel 2 Reward:</b> {(chosenTalent2 != null ? chosenTalent2.talentName : "NULL")}");
            Debug.Log("======================================");
        }

        OnSpinComplete?.Invoke(chosenTalent1, chosenTalent2);
        
        // Reset guaranteed rarity so next spin is random
        ClearGuaranteedRarity();

        // Optional: Reset tint after a delay? For now we keep it glowing.
        // ResetMaterialColors(originalColors);
    }

    private void DisableEmissionForWheel(PrayerWheel wheel)
    {
        DisableEmission(wheel.commonFloor?.floorMaterial);
        DisableEmission(wheel.rareFloor?.floorMaterial);
        DisableEmission(wheel.legendaryFloor?.floorMaterial);
    }

    private void DisableEmission(Material mat)
    {
        if (mat != null)
        {
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.SetColor(tintPropertyName, Color.black);
        }
    }

    // ... (existing code)

    /// <summary>
    /// Prepares talents on the wheel slots immediately (call before spin to show icons early).
    /// </summary>
    public void PrepareTalents()
    {
        if (talentDatabase == null)
        {
            if (debugLog) Debug.LogError("[PrayerWheelController] TalentDatabase not assigned!");
            return;
        }
        
        if (debugLog) Debug.Log("[PrayerWheelController] PrepareTalents() called - assigning icons to slots...");
        AssignTalentsToWheels();
        talentsPrepared = true; // Mark as prepared so spin won't re-assign
        
        // Debug: Check if icons were assigned
        int iconsAssigned = 0;
        for (int r = 0; r < 3; r++)
        {
            for (int s = 0; s < 5; s++)
            {
                if (wheel1Talents[r, s]?.icon != null) iconsAssigned++;
                if (wheel2Talents[r, s]?.icon != null) iconsAssigned++;
            }
        }
        if (debugLog) Debug.Log($"[PrayerWheelController] Talents prepared. Total icons assigned: {iconsAssigned}");
    }

    private void AssignTalentsToWheels()
    {
        if (debugLog) Debug.Log($"[PrayerWheelController] AssignTalentsToWheels() CALLED! Stack: {System.Environment.StackTrace}");
        
        // For each rarity, get talents and distribute without repetition
        AssignRarityTalents(TalentData.TalentRarity.Common, 0);
        AssignRarityTalents(TalentData.TalentRarity.Rare, 1);
        AssignRarityTalents(TalentData.TalentRarity.Legendary, 2);
    }

    private void AssignRarityTalents(TalentData.TalentRarity rarity, int rarityIndex)
    {
        TalentData[] pool = talentDatabase.GetTalentsByRarity(rarity);
        
        if (pool == null || pool.Length == 0)
        {
            if (debugLog) Debug.LogWarning($"[PrayerWheelController] No {rarity} talents in database!");
            return;
        }

        // Shuffle the pool
        List<TalentData> shuffled = new List<TalentData>(pool);
        ShuffleList(shuffled);

        // If we have 10+ talents, use the ideal split (first 5 for wheel1, next 5 for wheel2)
        if (shuffled.Count >= 10)
        {
            for (int i = 0; i < 5; i++)
            {
                wheel1Talents[rarityIndex, i] = shuffled[i];
                wheel2Talents[rarityIndex, i] = shuffled[i + 5];
            }
        }
        else
        {
            // Limited talents - ensure same socket position doesn't get same talent
            if (debugLog) Debug.Log($"[PrayerWheelController] Limited {rarity} talents ({shuffled.Count}). Using smart assignment.");
            
            for (int i = 0; i < 5; i++)
            {
                // Assign wheel1 from shuffled pool (cycling if needed)
                wheel1Talents[rarityIndex, i] = shuffled[i % shuffled.Count];
                
                // For wheel2, pick a DIFFERENT talent for the same socket
                if (shuffled.Count >= 2)
                {
                    // Pick next talent in the shuffled list (wrap around)
                    int wheel2Index = (i + 1) % shuffled.Count;
                    
                    // Make sure it's different from wheel1's talent at this socket
                    if (shuffled[wheel2Index] == wheel1Talents[rarityIndex, i])
                    {
                        // Try the next one
                        wheel2Index = (wheel2Index + 1) % shuffled.Count;
                    }
                    
                    wheel2Talents[rarityIndex, i] = shuffled[wheel2Index];
                }
                else
                {
                    // Only 1 talent - can't avoid duplicate (edge case)
                    wheel2Talents[rarityIndex, i] = shuffled[0];
                    if (debugLog) Debug.LogWarning($"[PrayerWheelController] Only 1 {rarity} talent - duplicate unavoidable at socket {i+1}");
                }
            }
        }

        // Update socket icons for both wheels
        WheelFloor floor1 = wheel1.GetFloor(rarity);
        WheelFloor floor2 = wheel2.GetFloor(rarity);

        for (int i = 0; i < 5; i++)
        {
            if (floor1?.sockets != null && i < floor1.sockets.Length && floor1.sockets[i] != null)
            {
                floor1.sockets[i].sprite = wheel1Talents[rarityIndex, i]?.icon;
                floor1.sockets[i].enabled = wheel1Talents[rarityIndex, i]?.icon != null;
            }

            if (floor2?.sockets != null && i < floor2.sockets.Length && floor2.sockets[i] != null)
            {
                floor2.sockets[i].sprite = wheel2Talents[rarityIndex, i]?.icon;
                floor2.sockets[i].enabled = wheel2Talents[rarityIndex, i]?.icon != null;
            }
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private IEnumerator AnimateSpin(float targetRotation, Dictionary<Material, Color> originalColors)
    {
        float elapsed = 0f;
        float currentRotation = 0f;

        // Get wheel root transforms (all floors rotate together)
        Transform transform1 = wheel1?.wheelRoot;
        Transform transform2 = wheel2?.wheelRoot;

        // Store starting rotations
        Vector3 startRot1 = transform1 != null ? transform1.localEulerAngles : Vector3.zero;
        Vector3 startRot2 = transform2 != null ? transform2.localEulerAngles : Vector3.zero;

        // Track tint states
        bool commonTinted = false;
        bool rareTinted = false;
        bool legendaryTinted = false;

        bool audioPaused = false;

        while (elapsed < spinDuration)
        {
            // Hybrid Approach:
            // 1. If Time.timeScale is 0 (Pause Menu), the wheel should STOP.
            // 2. If Time.timeScale is > 0 (e.g. 0.15 Slow Mo), the wheel should spin at NORMAL speed (Unscaled).
            float dt = (Time.timeScale == 0f) ? 0f : Time.unscaledDeltaTime;
            
            // Audio Pause Logic
            if (Time.timeScale == 0f)
            {
                if (!audioPaused)
                {
                    if (commonSpinSource != null) commonSpinSource.Pause();
                    if (rareSpinSource != null) rareSpinSource.Pause();
                    if (legendarySpinSource != null) legendarySpinSource.Pause();
                    audioPaused = true;
                    if (debugLog) Debug.Log("[PrayerWheelController] Audio Paused due to Game Pause");
                }
            }
            else
            {
                if (audioPaused)
                {
                    if (commonSpinSource != null) commonSpinSource.UnPause();
                    if (rareSpinSource != null) rareSpinSource.UnPause();
                    if (legendarySpinSource != null) legendarySpinSource.UnPause();
                    audioPaused = false;
                    if (debugLog) Debug.Log("[PrayerWheelController] Audio Resumed");
                }
            }

            elapsed += dt; 
            float normalizedTime = elapsed / spinDuration;
            
            float speed = speedRamp.Evaluate(normalizedTime);
            float targetAtTime = targetRotation * GetIntegratedProgress(normalizedTime);
            currentRotation = targetAtTime;

            if (transform1 != null) transform1.localEulerAngles = new Vector3(startRot1.x, startRot1.y + currentRotation, startRot1.z);
            if (transform2 != null) transform2.localEulerAngles = new Vector3(startRot2.x, startRot2.y + currentRotation, startRot2.z);

            // Material tinting based on progress
            float progress = normalizedTime;

            // 25% - Common gets red tint
            if (progress >= 0.25f && !commonTinted)
            {
                ApplyTint(wheel1.commonFloor?.floorMaterial, commonTintColor);
                ApplyTint(wheel2.commonFloor?.floorMaterial, commonTintColor);
                commonTinted = true;
            }

            // 50% - Rare gets blue tint, turn off common
            if (progress >= 0.50f && !rareTinted && currentRarity >= TalentData.TalentRarity.Rare)
            {
                // Turn off common tint
                DisableEmission(wheel1.commonFloor?.floorMaterial);
                DisableEmission(wheel2.commonFloor?.floorMaterial);
                
                // Apply rare tint
                ApplyTint(wheel1.rareFloor?.floorMaterial, rareTintColor);
                ApplyTint(wheel2.rareFloor?.floorMaterial, rareTintColor);
                rareTinted = true;
                
                // Unmute rare sound
                if (rareSpinSource != null && spinSoundRare != null)
                {
                    rareSpinSource.volume = spinSoundRare.GetVolume();
                    if (debugLog) Debug.Log("[PrayerWheelController] Rare sound unmuted");
                }
            }

            // 75% - Legendary gets yellow tint, turn off rare
            if (progress >= 0.75f && !legendaryTinted && currentRarity == TalentData.TalentRarity.Legendary)
            {
                // Turn off rare tint
                DisableEmission(wheel1.rareFloor?.floorMaterial);
                DisableEmission(wheel2.rareFloor?.floorMaterial);
                
                // Apply legendary tint
                ApplyTint(wheel1.legendaryFloor?.floorMaterial, legendaryTintColor);
                ApplyTint(wheel2.legendaryFloor?.floorMaterial, legendaryTintColor);
                legendaryTinted = true;
                
                // Unmute legendary sound
                if (legendarySpinSource != null && spinSoundLegendary != null)
                {
                    legendarySpinSource.volume = spinSoundLegendary.GetVolume();
                    if (debugLog) Debug.Log("[PrayerWheelController] Legendary sound unmuted");
                }
            }

            yield return null;
        }

        // Final snap
        if (transform1 != null) transform1.localEulerAngles = new Vector3(startRot1.x, startRot1.y + targetRotation, startRot1.z);
        if (transform2 != null) transform2.localEulerAngles = new Vector3(startRot2.x, startRot2.y + targetRotation, startRot2.z);
    }

    /// <summary>
    /// Calculates integrated progress using pre-calculated Lookup Table (LUT).
    /// This is O(1) instead of O(N) where N was 40 iterations.
    /// </summary>
    private float GetIntegratedProgress(float normalizedTime)
    {
        if (_progressLUT == null || _progressLUT.Length == 0)
            return normalizedTime; // Fallback

        normalizedTime = Mathf.Clamp01(normalizedTime);
        float indexFloat = normalizedTime * (LUT_RESOLUTION - 1);
        int indexLower = Mathf.FloorToInt(indexFloat);
        int indexUpper = Mathf.Min(indexLower + 1, LUT_RESOLUTION - 1);
        float t = indexFloat - indexLower;

        // Linear interpolate between LUT values
        return Mathf.Lerp(_progressLUT[indexLower], _progressLUT[indexUpper], t);
    }
    
    private void PreCalculateProgressLUT()
    {
        _progressLUT = new float[LUT_RESOLUTION];
        
        // Use the expensive integration logic ONCE at startup to fill the table
        int integrationSteps = 20; // Internal steps for the "truth" calculation
        
        for (int i = 0; i < LUT_RESOLUTION; i++)
        {
            float checkTime = (float)i / (LUT_RESOLUTION - 1);
            
            // Perform the expensive calculation here
            float sum = 0f;
            float stepSize = checkTime / integrationSteps;
            
            // Handle t=0 case
            if (checkTime <= 0.0001f)
            {
                _progressLUT[i] = 0f;
                continue;
            }
            
            for (int k = 0; k < integrationSteps; k++)
            {
                float t0 = k * stepSize;
                float t1 = (k + 1) * stepSize;
                float v0 = speedRamp.Evaluate(t0 / checkTime * checkTime); // Simplified: evaluating at effective time
                float v1 = speedRamp.Evaluate(t1 / checkTime * checkTime);
                sum += (v0 + v1) / 2f * stepSize;
            }
            
            // We also need the "Total Area" normalization constant, which is constant for the curve
            // But since GetIntegratedProgress logic included it dynamically, let's just stick to the original LOGIC
            // Wait, looking at original code:
            // "Evaluate(t0 / normalizedTime * normalizedTime)" -> This simplifies to "Evaluate(t0)"
            // The original code was: t0 / normalizedTime * normalizedTime == t0.
            // So it was just integrating from 0 to current time.
            
            // Let's optimize the logic cleanly:
            // We want "Area from 0 to t" / "Total Area from 0 to 1".
            
            _progressLUT[i] = CalculateAreaUnderCurve(checkTime);
        }
        
        // Normalize the whole table by the max value (Area at 1.0)
        float totalArea = _progressLUT[LUT_RESOLUTION - 1];
        if (totalArea > 0)
        {
            for (int i = 0; i < LUT_RESOLUTION; i++)
            {
                _progressLUT[i] /= totalArea;
            }
        }
    }
    
    private float CalculateAreaUnderCurve(float endTime)
    {
        int steps = 20;
        float sum = 0f;
        float stepSize = endTime / steps;
        
        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i * stepSize;
            float t1 = (float)(i + 1) * stepSize;
            float v0 = speedRamp.Evaluate(t0);
            float v1 = speedRamp.Evaluate(t1);
            sum += (v0 + v1) / 2f * stepSize;
        }
        return sum;
    }

    private void StoreMaterialColors(PrayerWheel wheel, Dictionary<Material, Color> storage)
    {
        StoreMaterialColor(wheel.commonFloor?.floorMaterial, storage);
        StoreMaterialColor(wheel.rareFloor?.floorMaterial, storage);
        StoreMaterialColor(wheel.legendaryFloor?.floorMaterial, storage);
    }

    private void StoreMaterialColor(Material mat, Dictionary<Material, Color> storage)
    {
        if (mat != null && !storage.ContainsKey(mat))
        {
            if (mat.HasProperty(tintPropertyName))
            {
                storage[mat] = mat.GetColor(tintPropertyName);
            }
            else
            {
                storage[mat] = Color.black;
            }
        }
    }

    private void ApplyTint(Material mat, Color color)
    {
        if (mat != null)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty(tintPropertyName))
            {
                mat.SetColor(tintPropertyName, color);
            }
        }
    }

    private void ResetMaterialColors(Dictionary<Material, Color> originalColors)
    {
        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null && kvp.Key.HasProperty(tintPropertyName))
            {
                kvp.Key.SetColor(tintPropertyName, kvp.Value);
                
                // If original color was basically black, disable emission to be clean
                if (kvp.Value.maxColorComponent < 0.01f)
                {
                    kvp.Key.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    /// <summary>
    /// Returns the world position of the winning socket for the specified wheel (1 or 2).
    /// Used by UI to position buttons over the 3D model.
    /// </summary>
    public Vector3 GetWinningSocketPosition(int wheelNum)
    {
        PrayerWheel targetWheel = (wheelNum == 1) ? wheel1 : wheel2;
        if (targetWheel == null) return Vector3.zero;

        WheelFloor floor = targetWheel.GetFloor(currentRarity);
        if (floor == null || floor.sockets == null) return Vector3.zero;

        if (currentSocketIndex >= 0 && currentSocketIndex < floor.sockets.Length)
        {
            Image socket = floor.sockets[currentSocketIndex];
            if (socket != null)
            {
                return socket.transform.position;
            }
        }
        
        return Vector3.zero;
    }

    /// <summary>
    /// Returns the world position of the wheel center point.
    /// Used by UI to position talent name text over the 3D wheels.
    /// Wheel number parameter is kept for API compatibility but returns same center point.
    /// </summary>
    public Vector3 GetTextPanelPosition(int wheelNum)
    {
        return wheelCenterPoint != null ? wheelCenterPoint.position : Vector3.zero;
    }
    
    /// <summary>
    /// Returns the wheel center point transform (for UI to calculate offsets).
    /// </summary>
    public Transform GetWheelCenterPoint() => wheelCenterPoint;

    #region Layered Audio
    /// <summary>
    /// Plays all tier spin sounds from the start.
    /// Common plays at normal volume.
    /// Rare and Legendary start muted (volume 0) and unmute when their tints are applied.
    /// </summary>
    private void PlayLayeredSpinSounds()
    {
        if (AudioManager.Instance == null) return;
        
        // Clear previous references
        commonSpinSource = null;
        rareSpinSource = null;
        legendarySpinSource = null;
        
        // Common always plays at full volume
        if (spinSoundCommon != null)
        {
            commonSpinSource = AudioManager.Instance.PlayAndGetSource(spinSoundCommon, 1f);
            if (debugLog) Debug.Log("[PrayerWheelController] Common spin sound started at full volume");
        }
        
        // Rare starts MUTED (volume 0) - will unmute when rare tint is applied
        if (spinSoundRare != null && currentRarity >= TalentData.TalentRarity.Rare)
        {
            rareSpinSource = AudioManager.Instance.PlayAndGetSource(spinSoundRare, 0f);
            if (debugLog) Debug.Log("[PrayerWheelController] Rare spin sound started MUTED (will unmute at 50%)");
        }
        
        // Legendary starts MUTED (volume 0) - will unmute when legendary tint is applied
        if (spinSoundLegendary != null && currentRarity == TalentData.TalentRarity.Legendary)
        {
            legendarySpinSource = AudioManager.Instance.PlayAndGetSource(spinSoundLegendary, 0f);
            if (debugLog) Debug.Log("[PrayerWheelController] Legendary spin sound started MUTED (will unmute at 75%)");
        }
    }
    
    /// <summary>
    /// Stops all layered spin sounds by stopping the cached sources.
    /// </summary>
    private void StopLayeredSpinSounds()
    {
        if (commonSpinSource != null)
        {
            commonSpinSource.Stop();
            commonSpinSource = null;
        }
        
        if (rareSpinSource != null)
        {
            rareSpinSource.Stop();
            rareSpinSource = null;
        }
        
        if (legendarySpinSource != null)
        {
            legendarySpinSource.Stop();
            legendarySpinSource = null;
        }
    }
    
    /// <summary>
    /// Plays layered stop sounds based on current rarity.
    /// Common = Common only. Rare = Common + Rare. Legendary = Common + Rare + Legendary.
    /// </summary>
    private void PlayLayeredStopSounds()
    {
        if (AudioManager.Instance == null) return;
        
        // Common always plays
        if (stopSoundCommon != null)
            AudioManager.Instance.Play(stopSoundCommon);
        
        // Rare layer plays for Rare and Legendary
        if (currentRarity >= TalentData.TalentRarity.Rare && stopSoundRare != null)
            AudioManager.Instance.Play(stopSoundRare);
        
        // Legendary layer only for Legendary
        if (currentRarity == TalentData.TalentRarity.Legendary && stopSoundLegendary != null)
            AudioManager.Instance.Play(stopSoundLegendary);
    }
    #endregion

    #region Debug
    [ContextMenu("Debug: Snap to Socket 0 (Check Alignment)")]
    public void DebugSnapToSocket0()
    {
        if (wheel1.wheelRoot != null) wheel1.wheelRoot.localEulerAngles = new Vector3(0, calibrationOffset, 0);
        if (wheel2.wheelRoot != null) wheel2.wheelRoot.localEulerAngles = new Vector3(0, calibrationOffset, 0);
        if (debugLog) Debug.Log($"[PrayerWheelController] Snapped to 0° + Offset {calibrationOffset}");
    }
    
    [ContextMenu("Debug: Start Spin")]
    public void DebugStartSpin() => StartSpin();
    #endregion
    
    private void Start()
    {
        if (wheelCenterPoint == null)
        {
            Debug.LogWarning($"[PrayerWheelController] <b>Wheel Center Point is NOT ASSIGNED on {gameObject.name}!</b> Text positioning will be incorrect.");
        }
    }

    private void OnDrawGizmos()
    {
        if (wheelCenterPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(wheelCenterPoint.position, 0.2f);
            Gizmos.DrawWireSphere(wheelCenterPoint.position, 0.5f);
        }
        else
        {
             // Draw warning gizmo at controller position
             Gizmos.color = Color.red;
             Gizmos.DrawWireSphere(transform.position, 0.5f);
             // Unity Gizmo text is editor only, so we skip it to keep code clean
        }
    }
}
