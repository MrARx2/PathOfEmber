using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TalentDatabase))]
public class TalentDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        TalentDatabase database = (TalentDatabase)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Sync Rarities on TalentData Assets"))
        {
            SyncRarities(database);
        }
    }

    private void SyncRarities(TalentDatabase database)
    {
        int updatedCount = 0;

        // Sync Common
        if (database.commonTalents != null)
        {
            foreach (var talent in database.commonTalents)
            {
                if (talent != null && talent.rarity != TalentData.TalentRarity.Common)
                {
                    talent.rarity = TalentData.TalentRarity.Common;
                    EditorUtility.SetDirty(talent);
                    updatedCount++;
                }
            }
        }

        // Sync Rare
        if (database.rareTalents != null)
        {
            foreach (var talent in database.rareTalents)
            {
                if (talent != null && talent.rarity != TalentData.TalentRarity.Rare)
                {
                    talent.rarity = TalentData.TalentRarity.Rare;
                    EditorUtility.SetDirty(talent);
                    updatedCount++;
                }
            }
        }

        // Sync Legendary
        if (database.legendaryTalents != null)
        {
            foreach (var talent in database.legendaryTalents)
            {
                if (talent != null && talent.rarity != TalentData.TalentRarity.Legendary)
                {
                    talent.rarity = TalentData.TalentRarity.Legendary;
                    EditorUtility.SetDirty(talent);
                    updatedCount++;
                }
            }
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[TalentDatabase] Successfully updated {updatedCount} talents with correct rarities!");
            EditorUtility.DisplayDialog("Sync Complete", $"Updated {updatedCount} talents to match their database rarity assignment.", "OK");
        }
        else
        {
            Debug.Log("[TalentDatabase] All talents already have correct rarities.");
            EditorUtility.DisplayDialog("Sync Complete", "All talents are already in sync!", "OK");
        }
    }
}
