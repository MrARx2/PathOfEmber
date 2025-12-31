using UnityEngine;

/// <summary>
/// ScriptableObject defining a single talent/ability.
/// </summary>
[CreateAssetMenu(fileName = "New Talent", menuName = "Path of Ember/Talent Data")]
public class TalentData : ScriptableObject
{
    public enum TalentRarity
    {
        Common,     // 60% chance
        Rare,       // 30% chance
        Legendary   // 10% chance
    }

    [Header("Identification")]
    [Tooltip("Display name shown to player")]
    public string talentName;
    
    [Tooltip("Unique ID matching PlayerAbilities method (e.g., 'MovementSpeed', 'Piercing')")]
    public string talentId;

    [Header("Visuals")]
    [Tooltip("Icon displayed on the prayer wheel socket")]
    public Sprite icon;
    
    [Tooltip("Rarity tier of this talent")]
    public TalentRarity rarity;

    [Header("Description")]
    [TextArea(2, 4)]
    [Tooltip("Tooltip description for the player")]
    public string description;
}
