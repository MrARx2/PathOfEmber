using UnityEngine;

/// <summary>
/// ScriptableObject containing all talents organized by rarity.
/// </summary>
[CreateAssetMenu(fileName = "TalentDatabase", menuName = "Path of Ember/Talent Database")]
public class TalentDatabase : ScriptableObject
{
    [Header("Common Talents (60% chance)")]
    [Tooltip("Talents in the Common tier")]
    public TalentData[] commonTalents;

    [Header("Rare Talents (30% chance)")]
    [Tooltip("Talents in the Rare tier")]
    public TalentData[] rareTalents;

    [Header("Legendary Talents (10% chance)")]
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
    /// Rolls for a random rarity based on probability weights.
    /// Common: 60%, Rare: 30%, Legendary: 10%
    /// </summary>
    public static TalentData.TalentRarity RollRarity()
    {
        float roll = Random.value;
        if (roll < 0.60f) return TalentData.TalentRarity.Common;
        if (roll < 0.90f) return TalentData.TalentRarity.Rare;
        return TalentData.TalentRarity.Legendary;
    }
}
