using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using Audio;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

/// <summary>
/// Batch generator for SoundEvent assets.
/// Scans sound folders and auto-creates configured SoundEvent assets.
/// Access via: Tools > Path of Ember > Generate Sound Events
/// </summary>
public class SoundEventBatchGenerator : EditorWindow
{
    private AudioMixerGroup sfxGroup;
    private AudioMixerGroup bgmGroup;
    private AudioMixerGroup ambientGroup;
    
    private bool includePlayer = true;
    private bool includeEnemies = true;
    private bool includeHazards = true;
    private bool includeUI = true;
    private bool includeAmbient = true;
    private bool includeMusic = true;
    
    private Vector2 scrollPos;
    private List<string> generationLog = new List<string>();
    
    [MenuItem("Tools/Path of Ember/Audio/Generate Sound Events")]
    public static void ShowWindow()
    {
        var window = GetWindow<SoundEventBatchGenerator>("Sound Event Generator");
        window.minSize = new Vector2(450, 500);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Sound Event Batch Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool scans your Sounds folders and automatically creates SoundEvent assets.\n" +
            "Variant files (Hurt1, Hurt2, Hurt3) are grouped into single SoundEvents.",
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // Mixer groups
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Mixer Groups (Optional)", EditorStyles.boldLabel);
        sfxGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("SFX Group", sfxGroup, typeof(AudioMixerGroup), false);
        bgmGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("BGM Group", bgmGroup, typeof(AudioMixerGroup), false);
        ambientGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("Ambient Group", ambientGroup, typeof(AudioMixerGroup), false);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Categories
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Categories to Generate", EditorStyles.boldLabel);
        includePlayer = EditorGUILayout.Toggle("Player Sounds", includePlayer);
        includeEnemies = EditorGUILayout.Toggle("Enemy Sounds", includeEnemies);
        includeHazards = EditorGUILayout.Toggle("Hazard Sounds", includeHazards);
        includeUI = EditorGUILayout.Toggle("UI Sounds", includeUI);
        includeAmbient = EditorGUILayout.Toggle("Ambient Sounds", includeAmbient);
        includeMusic = EditorGUILayout.Toggle("Music", includeMusic);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Generate button
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Generate All Sound Events", GUILayout.Height(35)))
        {
            GenerateAllSoundEvents();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(10);
        
        // Log
        if (generationLog.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Generation Log", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            foreach (var log in generationLog)
            {
                GUILayout.Label(log);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }
    
    private void GenerateAllSoundEvents()
    {
        generationLog.Clear();
        int totalCreated = 0;
        
        // Ensure output folders exist
        EnsureFolderExists("Assets/Sounds/SoundEvents");
        EnsureFolderExists("Assets/Sounds/SoundEvents/Player");
        EnsureFolderExists("Assets/Sounds/SoundEvents/Enemies");
        EnsureFolderExists("Assets/Sounds/SoundEvents/Hazards");
        EnsureFolderExists("Assets/Sounds/SoundEvents/UI");
        EnsureFolderExists("Assets/Sounds/SoundEvents/Ambient");
        EnsureFolderExists("Assets/Sounds/SoundEvents/Music");
        
        // Generate each category
        if (includePlayer)
            totalCreated += GeneratePlayerSounds();
        
        if (includeEnemies)
            totalCreated += GenerateEnemySounds();
        
        if (includeHazards)
            totalCreated += GenerateHazardSounds();
        
        if (includeUI)
            totalCreated += GenerateUISounds();
        
        if (includeAmbient)
            totalCreated += GenerateAmbientSounds();
        
        if (includeMusic)
            totalCreated += GenerateMusicSounds();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        generationLog.Add($"--- COMPLETE: {totalCreated} SoundEvents created ---");
        Debug.Log($"[SoundEvent Generator] Created {totalCreated} SoundEvent assets");
    }
    
    private int GeneratePlayerSounds()
    {
        int count = 0;
        string sourcePath = "Assets/Sounds/Player";
        string outputPath = "Assets/Sounds/SoundEvents/Player";
        
        if (!AssetDatabase.IsValidFolder(sourcePath)) return 0;
        
        // Define variant groups
        var groups = new Dictionary<string, string[]>
        {
            { "PlayerHurt", new[] { "POE Hurt1", "POE Hurt2", "POE Hurt3" } },
            { "CoinPickup", new[] { "POE Coin1", "POE Coin2", "POE Coin3" } },
            { "EnemyHit", new[] { "POE EnemyHit1", "POE EnemyHit2", "POE EnemyHit3" } },
            { "FreezeApply", new[] { "POE Freeze1", "POE Freeze2", "POE Freeze3" } },
            { "VenomApply", new[] { "POE Venom1", "POE Venom2", "POE Venom3" } },
            { "ArrowWallHit", new[] { "POE Wall Hit1", "POE Wall Hit2", "POE Wall Hit 3" } },
        };
        
        // Create grouped SoundEvents - Player sounds are 2D (always heard)
        foreach (var group in groups)
        {
            var clips = FindClips(sourcePath, group.Value);
            if (clips.Count > 0)
            {
                CreateSoundEvent(outputPath, group.Key, clips.ToArray(), sfxGroup, 
                    randomizePitch: true, maxInstances: 3, cooldown: 0.05f, 
                    spatialBlend: 0f); // 2D - player sounds always audible
                count++;
            }
        }
        
        // Single sounds
        var singles = new Dictionary<string, string>
        {
            { "BowShot", "BowShot" },
            { "Footstep", "Step" },
            { "Heal", "Heal" },
            { "FireHurt", "FireHurt" },
            { "Invulnerability", "InvulnerabiilityState" },
            { "PotionPickup", "PotionPickUp" },
            { "PotionFall", "PotionFall" },
            { "XPUp", "XPUp" },
            { "PlayerDie", "POE PlayerDie" },
        };
        
        foreach (var single in singles)
        {
            var clip = FindClip(sourcePath, single.Value);
            if (clip != null)
            {
                CreateSoundEvent(outputPath, single.Key, new[] { clip }, sfxGroup, 
                    spatialBlend: 0f); // 2D - player sounds always audible
                count++;
            }
        }
        
        generationLog.Add($"Player: {count} events created");
        return count;
    }
    
    private int GenerateEnemySounds()
    {
        int count = 0;
        string outputPath = "Assets/Sounds/SoundEvents/Enemies";
        
        // Chaser
        count += CreateFromFolder("Assets/Sounds/Enemies/Chaser", outputPath, "Chaser", new Dictionary<string, string>
        {
            { "ChaserDie", "POE Chaser Die" },
            { "ChaserDistinct", "POE Chaser Distinct" },
        });
        
        // Bomber
        count += CreateFromFolder("Assets/Sounds/Enemies/Bomber", outputPath, "Bomber", new Dictionary<string, string>
        {
            { "BomberBip", "BomberBip" },
            { "BomberBoom", "BomberBoom" },
            { "BomberDie", "BomberDie" },
            { "BomberDistinct", "BomberDistinct" },
        });
        
        // Sniper
        count += CreateFromFolder("Assets/Sounds/Enemies/Sniper", outputPath, "Sniper", new Dictionary<string, string>
        {
            { "SniperAim", "POE Sniper Aim" },
            { "SniperShoot", "POE Sniper Shoot" },
            { "SniperDie", "POE Sniper Die" },
            { "SniperDistinct", "POE Sniper Distinct" },
        });
        
        // MiniBoss
        count += CreateFromFolder("Assets/Sounds/Enemies/MiniBoss", outputPath, "MiniBoss", new Dictionary<string, string>
        {
            { "MiniBossDie", "POE MiniBossDie" },
            { "MiniBossFireBall", "POE MiniBossFireBallCall" },
            { "MiniBossMeteor", "POE MiniBossMeteorCall" },
            { "MiniBossRage", "POE MiniBossRageBlast" },
        });
        
        // Titan
        count += CreateFromFolder("Assets/Sounds/Enemies/Titan", outputPath, "Titan", new Dictionary<string, string>
        {
            { "TitanDie", "TitanDie" },
            { "TitanFist", "TitanFist" },
            { "TitanMeteorBlast", "TitanMeteorBlast" },
            { "TitanRage", "TitanRage" },
            { "TitanSummon", "TitanSummon" },
            { "TitanSummonCrunch", "TitanSummonCrunch" },
        });
        
        generationLog.Add($"Enemies: {count} events created");
        return count;
    }
    
    private int GenerateHazardSounds()
    {
        int count = 0;
        string outputPath = "Assets/Sounds/SoundEvents/Hazards";
        
        // Meteor - larger range (10m) as they're impactful events
        var meteorFall = FindClip("Assets/Sounds/Meteor", "MeteorFall");
        var meteorBoom = FindClip("Assets/Sounds/Meteor", "MeteorBoom");
        
        if (meteorFall != null)
        {
            CreateSoundEvent(outputPath, "MeteorFall", new[] { meteorFall }, sfxGroup,
                spatialBlend: 1f, minDistance: 2f, maxDistance: 10f);
            count++;
        }
        if (meteorBoom != null)
        {
            CreateSoundEvent(outputPath, "MeteorBoom", new[] { meteorBoom }, sfxGroup, 
                maxInstances: 5, spatialBlend: 1f, minDistance: 2f, maxDistance: 10f);
            count++;
        }
        
        // Spawn - medium range (5m)
        var spawnPuff = FindClip("Assets/Sounds/Spawn", "SpawnPuff");
        if (spawnPuff != null)
        {
            CreateSoundEvent(outputPath, "SpawnPuff", new[] { spawnPuff }, sfxGroup, 
                maxInstances: 5, spatialBlend: 1f, minDistance: 1f, maxDistance: 5f);
            count++;
        }
        
        generationLog.Add($"Hazards: {count} events created");
        return count;
    }
    
    private int GenerateUISounds()
    {
        int count = 0;
        string sourcePath = "Assets/Sounds/PrayerWheel";
        string outputPath = "Assets/Sounds/SoundEvents/UI";
        
        var uiSounds = new Dictionary<string, string>
        {
            { "UIClick", "Click" },
            { "PrayerWheelSpin", "POE PrayerWheelSpin" },
            { "PrayerWheelEnd", "POE PrayerWheelEnd" },
            { "PrayerWheelCommon", "POE PrayerWheelCommon" },
            { "PrayerWheelRare", "POE PrayerWheelRare" },
            { "PrayerWheelLegendary", "POE PrayerWheelLegandary" },
        };
        
        foreach (var sound in uiSounds)
        {
            var clip = FindClip(sourcePath, sound.Value);
            if (clip != null)
            {
                // UI sounds are 2D - always heard at full volume
                CreateSoundEvent(outputPath, sound.Key, new[] { clip }, sfxGroup,
                    spatialBlend: 0f);
                count++;
            }
        }
        
        generationLog.Add($"UI: {count} events created");
        return count;
    }
    
    private int GenerateAmbientSounds()
    {
        int count = 0;
        string sourcePath = "Assets/Sounds/Ambient";
        string outputPath = "Assets/Sounds/SoundEvents/Ambient";
        
        var ambientSounds = new Dictionary<string, string>
        {
            { "AmbientFire", "Fire" },
            { "AmbientLava", "LavaFlow" },
            { "AmbientRiver", "RiverFlow" },
            { "AmbientWind", "Wind" },
        };
        
        foreach (var sound in ambientSounds)
        {
            var clip = FindClip(sourcePath, sound.Value);
            if (clip != null)
            {
                // Ambient: 3m range, fully 3D
                CreateSoundEvent(outputPath, sound.Key, new[] { clip }, ambientGroup, 
                    maxInstances: 1, spatialBlend: 1f, minDistance: 0.5f, maxDistance: 3f);
                count++;
            }
        }
        
        generationLog.Add($"Ambient: {count} events created");
        return count;
    }
    
    private int GenerateMusicSounds()
    {
        int count = 0;
        string sourcePath = "Assets/Sounds/Music";
        string outputPath = "Assets/Sounds/SoundEvents/Music";
        
        var clip = FindClip(sourcePath, "MenuTrack");
        if (clip != null)
        {
            CreateSoundEvent(outputPath, "MenuTrack", new[] { clip }, bgmGroup, maxInstances: 1);
            count++;
        }
        
        generationLog.Add($"Music: {count} events created");
        return count;
    }
    
    #region Helpers
    
    private int CreateFromFolder(string sourcePath, string outputPath, string prefix, Dictionary<string, string> sounds)
    {
        int count = 0;
        foreach (var sound in sounds)
        {
            var clip = FindClip(sourcePath, sound.Value);
            if (clip != null)
            {
                // Enemy sounds: 5m range, fully 3D
                CreateSoundEvent(outputPath, sound.Key, new[] { clip }, sfxGroup,
                    spatialBlend: 1f, minDistance: 1f, maxDistance: 5f);
                count++;
            }
        }
        return count;
    }
    
    private AudioClip FindClip(string folderPath, string nameContains)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            generationLog.Add($"    [!] Folder not found: {folderPath}");
            return null;
        }
        
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
        
        // First try exact match (ignoring spaces)
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string normalizedFile = fileName.Replace(" ", "").ToLower();
            string normalizedSearch = nameContains.Replace(" ", "").ToLower();
            
            if (normalizedFile == normalizedSearch)
            {
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
        
        // Then try contains match
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string normalizedFile = fileName.Replace(" ", "").ToLower();
            string normalizedSearch = nameContains.Replace(" ", "").ToLower();
            
            if (normalizedFile.Contains(normalizedSearch) || normalizedSearch.Contains(normalizedFile))
            {
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
        
        generationLog.Add($"    [!] Clip not found: {nameContains} in {folderPath}");
        return null;
    }
    
    private List<AudioClip> FindClips(string folderPath, string[] names)
    {
        var clips = new List<AudioClip>();
        foreach (var name in names)
        {
            var clip = FindClip(folderPath, name);
            if (clip != null) clips.Add(clip);
        }
        return clips;
    }
    
    private void CreateSoundEvent(string outputPath, string eventName, AudioClip[] clips, 
        AudioMixerGroup mixerGroup, bool randomizePitch = false, int maxInstances = 3, 
        float cooldown = 0f, float spatialBlend = 1f, float minDistance = 1f, float maxDistance = 5f)
    {
        string assetPath = $"{outputPath}/{eventName}.asset";
        
        // Check if already exists
        if (AssetDatabase.LoadAssetAtPath<SoundEvent>(assetPath) != null)
        {
            generationLog.Add($"  Skipped (exists): {eventName}");
            return;
        }
        
        var soundEvent = ScriptableObject.CreateInstance<SoundEvent>();
        soundEvent.clips = clips;
        soundEvent.volume = 1f;
        soundEvent.pitch = 1f;
        soundEvent.randomizePitch = randomizePitch;
        soundEvent.pitchVariation = 0.1f;
        soundEvent.mixerGroup = mixerGroup;
        soundEvent.maxInstances = maxInstances;
        soundEvent.stealMode = SoundEvent.StealMode.StealOldest;
        soundEvent.cooldown = cooldown;
        soundEvent.spatialBlend = spatialBlend;
        soundEvent.minDistance = minDistance;
        soundEvent.maxDistance = maxDistance;
        
        AssetDatabase.CreateAsset(soundEvent, assetPath);
        generationLog.Add($"  Created: {eventName} ({clips.Length} clips, 3D: {spatialBlend:F1}, range: {minDistance}-{maxDistance}m)");
    }
    
    private void EnsureFolderExists(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
    
    #endregion
}
