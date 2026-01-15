using UnityEngine;

/// <summary>
/// ScriptableObject containing all talents organized by rarity.
/// </summary>
[CreateAssetMenu(fileName = "TalentDatabase", menuName = "Path of Ember/Talent Database")]
public class TalentDatabase : ScriptableObject
{
    [Header("Common Talents (50% chance)")]
    [Tooltip("Talents in the Common tier")]
    public TalentData[] commonTalents;

    [Header("Rare Talents (30% chance)")]
    [Tooltip("Talents in the Rare tier")]
    public TalentData[] rareTalents;

    [Header("Legendary Talents (20% chance)")]
    [Tooltip("Talents in the Legendary tier")]
    public TalentData[] legendaryTalents;

    /// <summary>
    /// Gets the talent array for a given rarity.
    /// </summary>
    public TalentData[] GetTalentsByRarity(TalentData.TalentRarity rarity)
    {
        return rarity switch
        {
            TalentData.TalentRarity.Common => commonTalents,
            TalentData.TalentRarity.Rare => rareTalents,
            TalentData.TalentRarity.Legendary => legendaryTalents,
            _ => commonTalents
        };
    }

    /// <summary>
    /// Gets all talents from all rarities combined.
    /// </summary>
    public TalentData[] GetAllTalents()
    {
        int totalCount = 
            (commonTalents?.Length ?? 0) + 
            (rareTalents?.Length ?? 0) + 
            (legendaryTalents?.Length ?? 0);
        
        TalentData[] all = new TalentData[totalCount];
        int index = 0;
        
        if (commonTalents != null)
        {
            foreach (var t in commonTalents) all[index++] = t;
        }
        if (rareTalents != null)
        {
            foreach (var t in rareTalents) all[index++] = t;
        }
        if (legendaryTalents != null)
        {
            foreach (var t in legendaryTalents) all[index++] = t;
        }
        
        return all;
    }

    /// <summary>
    /// Rolls for a random rarity based on probability weights.
    /// Common: 50%, Rare: 30%, Legendary: 20%
    /// </summary>
    public static TalentData.TalentRarity RollRarity()
    {
        float roll = Random.value;
        if (roll < 0.50f) return TalentData.TalentRarity.Common;
        if (roll < 0.80f) return TalentData.TalentRarity.Rare;
        return TalentData.TalentRarity.Legendary;
    }
}

