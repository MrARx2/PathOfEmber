using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that tracks all talents acquired during a run.
/// Persists across scenes, reset on game restart.
/// </summary>
[CreateAssetMenu(fileName = "RunTalentRegistry", menuName = "Game/Run Talent Registry")]
public class RunTalentRegistry : ScriptableObject
{
    [Serializable]
    public struct TalentEntry
    {
        public TalentData talent;
        public int stacks;
    }

    // Internal dictionary for O(1) operations
    private Dictionary<TalentData, int> talentStacks = new Dictionary<TalentData, int>();

    // Debug: Serialized list for Inspector visibility
    [Header("Debug View (Read-Only)")]
    [SerializeField] private List<TalentEntry> debugTalentList = new List<TalentEntry>();

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool debugLog = false;

    // Cached array for fast menu access
    private TalentEntry[] cachedEntries;
    private bool cacheValid = false;

    /// <summary>
    /// Pre-built array of all acquired talents and their stacks.
    /// Use this for menu UI iteration (zero allocation).
    /// </summary>
    public TalentEntry[] Entries
    {
        get
        {
            if (!cacheValid || cachedEntries == null)
            {
                RebuildCache();
            }
            return cachedEntries;
        }
    }

    /// <summary>
    /// Number of unique talents acquired.
    /// </summary>
    public int UniqueTalentCount => talentStacks.Count;

    /// <summary>
    /// Adds one stack of a talent. Creates entry if first acquisition.
    /// </summary>
    public void AddTalent(TalentData talent)
    {
        if (talent == null) return;

        if (talentStacks.TryGetValue(talent, out int currentStacks))
        {
            talentStacks[talent] = currentStacks + 1;
        }
        else
        {
            talentStacks[talent] = 1;
        }

        cacheValid = false;
        SyncDebugList();
        
        if (debugLog) Debug.Log($"[RunTalentRegistry] Added {talent.talentName} (now {talentStacks[talent]} stacks)");
    }

    /// <summary>
    /// Returns the stack count for a talent (0 if not acquired).
    /// </summary>
    public int GetStacks(TalentData talent)
    {
        if (talent == null) return 0;
        return talentStacks.TryGetValue(talent, out int stacks) ? stacks : 0;
    }

    /// <summary>
    /// Returns true if the player has acquired this talent.
    /// </summary>
    public bool HasTalent(TalentData talent)
    {
        return talent != null && talentStacks.ContainsKey(talent);
    }

    /// <summary>
    /// Clears all acquired talents. Call on game restart/new run.
    /// </summary>
    public void Clear()
    {
        talentStacks.Clear();
        debugTalentList.Clear();
        cachedEntries = Array.Empty<TalentEntry>();
        cacheValid = true;
        
        if (debugLog) Debug.Log("[RunTalentRegistry] Cleared all talents");
    }

    private void RebuildCache()
    {
        cachedEntries = new TalentEntry[talentStacks.Count];
        int i = 0;
        foreach (var kvp in talentStacks)
        {
            cachedEntries[i++] = new TalentEntry { talent = kvp.Key, stacks = kvp.Value };
        }
        cacheValid = true;
    }

    private void SyncDebugList()
    {
        debugTalentList.Clear();
        foreach (var kvp in talentStacks)
        {
            debugTalentList.Add(new TalentEntry { talent = kvp.Key, stacks = kvp.Value });
        }
    }

    // Clear on play mode exit (editor only) to prevent stale data
    private void OnEnable()
    {
        // Reinitialize dictionary (ScriptableObjects don't serialize dictionaries)
        if (talentStacks == null)
        {
            talentStacks = new Dictionary<TalentData, int>();
        }
        
        // Rebuild from debug list if it has data (e.g., after domain reload)
        if (debugTalentList != null && debugTalentList.Count > 0 && talentStacks.Count == 0)
        {
            foreach (var entry in debugTalentList)
            {
                if (entry.talent != null)
                {
                    talentStacks[entry.talent] = entry.stacks;
                }
            }
        }
        
        cacheValid = false;
    }

    [ContextMenu("Debug: Clear All")]
    public void DebugClear() => Clear();
}
