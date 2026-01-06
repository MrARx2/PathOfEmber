using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spinSound;
    [SerializeField] private AudioClip stopSound;

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

        // Auto-wire from PrayerWheelSetup if assigned
        AutoWireFromSetup();
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
            Debug.Log("[PrayerWheelController] Auto-wired wheel1 from setup");
        }

        if (wheel2Setup != null)
        {
            wheel2.wheelRoot = wheel2Setup.transform;
            wheel2.commonFloor = new WheelFloor { sockets = wheel2Setup.CommonImages, floorMaterial = wheel2Setup.commonMaterial };
            wheel2.rareFloor = new WheelFloor { sockets = wheel2Setup.RareImages, floorMaterial = wheel2Setup.rareMaterial };
            wheel2.legendaryFloor = new WheelFloor { sockets = wheel2Setup.LegendaryImages, floorMaterial = wheel2Setup.legendaryMaterial };
            Debug.Log("[PrayerWheelController] Auto-wired wheel2 from setup");
        }
    }

    /// <summary>
    /// Starts the prayer wheel spin sequence.
    /// </summary>
    public void StartSpin()
    {
        if (isSpinning)
        {
            Debug.LogWarning("[PrayerWheelController] Already spinning!");
            return;
        }

        if (talentDatabase == null)
        {
            Debug.LogError("[PrayerWheelController] TalentDatabase not assigned!");
            return;
        }

        StartCoroutine(SpinSequence());
    }

    [Header("Debug")]
    [SerializeField] private bool useDebugRarity = false;
    [SerializeField] private TalentData.TalentRarity debugRarityOverride = TalentData.TalentRarity.Legendary;

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
            Debug.Log("[PrayerWheelController] Using pre-assigned talents.");
        }
        talentsPrepared = false; // Reset for next spin cycle

        // Step 3: Pick random socket
        currentSocketIndex = Random.Range(0, 5);
        Debug.Log($"[PrayerWheelController] Socket chosen: {currentSocketIndex + 1}");

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
        
        Debug.Log($"[PrayerWheelController] Target: {targetSocketAngle}°, ToAdd: {rotationAmountToAdd:F1}° (Offset: {calibrationOffset})");

        // Step 5: Store original material colors for reset
        Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
        StoreMaterialColors(wheel1, originalColors);
        StoreMaterialColors(wheel2, originalColors);

        // EXTRA: Force-disable emission at start
        DisableEmissionForWheel(wheel1);
        DisableEmissionForWheel(wheel2);

        // Step 6: Play spin sound
        if (audioSource != null && spinSound != null)
        {
            audioSource.clip = spinSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Step 7: Animate spin
        yield return StartCoroutine(AnimateSpin(rotationAmountToAdd, originalColors));

        // Step 8: Finalize
        if (audioSource != null)
        {
            if (stopSound != null) 
            {
                audioSource.Stop();
                audioSource.PlayOneShot(stopSound);
            }
            else
            {
                audioSource.Stop();
            }
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

        Debug.Log("======================================");
        Debug.Log($"[PrayerWheelController] SPIN COMPLETE!");
        Debug.Log($"[PrayerWheelController] Rarity: {currentRarity}");
        Debug.Log($"[PrayerWheelController] Selected Socket Index: {currentSocketIndex}");
        Debug.Log($"[PrayerWheelController] <b>Wheel 1 Reward:</b> {(chosenTalent1 != null ? chosenTalent1.talentName : "NULL")}");
        Debug.Log($"[PrayerWheelController] <b>Wheel 2 Reward:</b> {(chosenTalent2 != null ? chosenTalent2.talentName : "NULL")}");
        Debug.Log("======================================");

        OnSpinComplete?.Invoke(chosenTalent1, chosenTalent2);
        
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
            Debug.LogError("[PrayerWheelController] TalentDatabase not assigned!");
            return;
        }
        
        Debug.Log("[PrayerWheelController] PrepareTalents() called - assigning icons to slots...");
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
        Debug.Log($"[PrayerWheelController] Talents prepared. Total icons assigned: {iconsAssigned}");
    }

    private void AssignTalentsToWheels()
    {
        Debug.Log($"[PrayerWheelController] AssignTalentsToWheels() CALLED! Stack: {System.Environment.StackTrace}");
        
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
            Debug.LogWarning($"[PrayerWheelController] No {rarity} talents in database!");
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
            Debug.Log($"[PrayerWheelController] Limited {rarity} talents ({shuffled.Count}). Using smart assignment.");
            
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
                    Debug.LogWarning($"[PrayerWheelController] Only 1 {rarity} talent - duplicate unavoidable at socket {i+1}");
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

        while (elapsed < spinDuration)
        {
            elapsed += Time.unscaledDeltaTime; 
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
            }

            yield return null;
        }

        // Final snap
        if (transform1 != null) transform1.localEulerAngles = new Vector3(startRot1.x, startRot1.y + targetRotation, startRot1.z);
        if (transform2 != null) transform2.localEulerAngles = new Vector3(startRot2.x, startRot2.y + targetRotation, startRot2.z);
    }

    /// <summary>
    /// Calculates integrated progress based on speed curve.
    /// This ensures smooth acceleration/deceleration.
    /// </summary>
    private float GetIntegratedProgress(float normalizedTime)
    {
        // Simple trapezoidal integration of the speed curve
        int steps = 20;
        float sum = 0f;
        float stepSize = normalizedTime / steps;
        
        for (int i = 0; i < steps; i++)
        {
            float t0 = i * stepSize;
            float t1 = (i + 1) * stepSize;
            float v0 = speedRamp.Evaluate(t0 / normalizedTime * normalizedTime);
            float v1 = speedRamp.Evaluate(t1 / normalizedTime * normalizedTime);
            sum += (v0 + v1) / 2f * stepSize;
        }

        // Normalize by total area under curve
        float totalArea = 0f;
        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            float v0 = speedRamp.Evaluate(t0);
            float v1 = speedRamp.Evaluate(t1);
            totalArea += (v0 + v1) / 2f * (1f / steps);
        }

        return totalArea > 0 ? sum / totalArea : normalizedTime;
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

    #region Debug
    [ContextMenu("Debug: Snap to Socket 0 (Check Alignment)")]
    public void DebugSnapToSocket0()
    {
        if (wheel1.wheelRoot != null) wheel1.wheelRoot.localEulerAngles = new Vector3(0, calibrationOffset, 0);
        if (wheel2.wheelRoot != null) wheel2.wheelRoot.localEulerAngles = new Vector3(0, calibrationOffset, 0);
        Debug.Log($"[PrayerWheelController] Snapped to 0° + Offset {calibrationOffset}");
    }
    
    [ContextMenu("Debug: Start Spin")]
    public void DebugStartSpin() => StartSpin();
    #endregion
}
