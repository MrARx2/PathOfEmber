using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using Audio;

/// <summary>
/// Setup wizard for the Audio Manager system.
/// Access via: Tools > Path of Ember > Setup Audio Manager
/// </summary>
public class AudioManagerSetupWizard : EditorWindow
{
    private AudioMixer audioMixer;
    private AudioMixerGroup sfxGroup;
    private AudioMixerGroup bgmGroup;
    
    [MenuItem("Tools/Path of Ember/Setup Audio Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<AudioManagerSetupWizard>("Audio Setup");
        window.minSize = new Vector2(400, 350);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Audio Manager Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        // Check status
        bool hasManager = FindAnyObjectByType<AudioManager>() != null;
        
        // Status
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Status", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("AudioManager in Scene:");
        GUILayout.Label(hasManager ? "✓ Found" : "✗ Not Found", 
            hasManager ? new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.green } } 
                       : EditorStyles.label);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Mixer assignment
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Audio Mixer Configuration", EditorStyles.boldLabel);
        
        audioMixer = (AudioMixer)EditorGUILayout.ObjectField("Audio Mixer", audioMixer, typeof(AudioMixer), false);
        sfxGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("SFX Group", sfxGroup, typeof(AudioMixerGroup), false);
        bgmGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("BGM Group", bgmGroup, typeof(AudioMixerGroup), false);
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Setup buttons
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Setup Actions", EditorStyles.boldLabel);
        
        GUI.enabled = !hasManager;
        if (GUILayout.Button("Create AudioManager in Scene", GUILayout.Height(30)))
        {
            CreateAudioManager();
        }
        GUI.enabled = true;
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Create SoundEvent Asset Folders", GUILayout.Height(25)))
        {
            CreateSoundEventFolders();
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Instructions
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Quick Start Guide", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Create AudioManager in scene\n" +
            "2. Assign your Audio Mixer and groups\n" +
            "3. Create SoundEvent assets (Right-click > Create > Audio > Sound Event)\n" +
            "4. Drag clips into SoundEvent, adjust settings\n" +
            "5. In your scripts: [SerializeField] SoundEvent mySound;\n" +
            "6. Play with: AudioManager.Instance.Play(mySound);",
            MessageType.Info);
        EditorGUILayout.EndVertical();
    }
    
    private void CreateAudioManager()
    {
        if (FindAnyObjectByType<AudioManager>() != null)
        {
            EditorUtility.DisplayDialog("Exists", "AudioManager already exists in scene.", "OK");
            return;
        }
        
        GameObject go = new GameObject("AudioManager");
        AudioManager manager = go.AddComponent<AudioManager>();
        
        // Assign mixer if set
        if (audioMixer != null || sfxGroup != null || bgmGroup != null)
        {
            SerializedObject so = new SerializedObject(manager);
            
            if (audioMixer != null)
                so.FindProperty("audioMixer").objectReferenceValue = audioMixer;
            if (sfxGroup != null)
                so.FindProperty("defaultSFXGroup").objectReferenceValue = sfxGroup;
            if (bgmGroup != null)
                so.FindProperty("defaultBGMGroup").objectReferenceValue = bgmGroup;
            
            so.ApplyModifiedProperties();
        }
        
        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");
        
        Debug.Log("[Audio Setup] Created AudioManager");
    }
    
    private void CreateSoundEventFolders()
    {
        string basePath = "Assets/Audio/SoundEvents";
        
        CreateFolderIfNeeded("Assets", "Audio");
        CreateFolderIfNeeded("Assets/Audio", "SoundEvents");
        CreateFolderIfNeeded(basePath, "Player");
        CreateFolderIfNeeded(basePath, "Enemies");
        CreateFolderIfNeeded(basePath, "Hazards");
        CreateFolderIfNeeded(basePath, "UI");
        CreateFolderIfNeeded(basePath, "Ambient");
        CreateFolderIfNeeded(basePath, "Music");
        
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Folders Created", 
            $"Created folder structure at:\n{basePath}\n\n" +
            "Subfolders: Player, Enemies, Hazards, UI, Ambient, Music", "OK");
    }
    
    private void CreateFolderIfNeeded(string parent, string folder)
    {
        string fullPath = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
