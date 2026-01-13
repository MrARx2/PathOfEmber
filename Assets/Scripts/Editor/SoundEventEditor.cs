using UnityEngine;
using UnityEditor;
using Audio;
using System.Reflection;

/// <summary>
/// Custom inspector for SoundEvent ScriptableObjects.
/// Provides preview buttons and visual feedback.
/// </summary>
[CustomEditor(typeof(SoundEvent))]
public class SoundEventEditor : Editor
{
    private AudioSource _previewSource;
    private SoundEvent _soundEvent;
    
    private void OnEnable()
    {
        _soundEvent = (SoundEvent)target;
    }
    
    private void OnDisable()
    {
        StopPreview();
    }
    
    public override void OnInspectorGUI()
    {
        // Header with preview buttons
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("▶ Play Random", GUILayout.Width(100), GUILayout.Height(25)))
        {
            PlayRandomClip();
        }
        
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.4f);
        if (GUILayout.Button("▶ Play All", GUILayout.Width(80), GUILayout.Height(25)))
        {
            PlayAllClips();
        }
        
        GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
        if (GUILayout.Button("■ Stop", GUILayout.Width(60), GUILayout.Height(25)))
        {
            StopPreview();
        }
        
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // Draw clips array with individual play buttons
        DrawClipsArray();
        
        EditorGUILayout.Space(5);
        
        // Draw remaining properties
        DrawPropertiesExcluding(serializedObject, "m_Script", "clips");
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawClipsArray()
    {
        EditorGUILayout.LabelField("Audio Clips", EditorStyles.boldLabel);
        
        SerializedProperty clipsProp = serializedObject.FindProperty("clips");
        
        EditorGUI.indentLevel++;
        
        // Array size
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(clipsProp.FindPropertyRelative("Array.size"), new GUIContent("Size"));
        EditorGUILayout.EndHorizontal();
        
        // Individual clips with play buttons
        for (int i = 0; i < clipsProp.arraySize; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
            AudioClip clip = clipProp.objectReferenceValue as AudioClip;
            
            // Play button for this clip
            GUI.backgroundColor = new Color(0.6f, 0.8f, 0.6f);
            if (GUILayout.Button("▶", GUILayout.Width(25), GUILayout.Height(18)))
            {
                if (clip != null)
                {
                    PlayClip(clip);
                }
            }
            GUI.backgroundColor = Color.white;
            
            // Clip field
            EditorGUILayout.PropertyField(clipProp, new GUIContent($"[{i}]"));
            
            // Show duration
            if (clip != null)
            {
                GUILayout.Label($"{clip.length:F2}s", GUILayout.Width(45));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void PlayRandomClip()
    {
        if (_soundEvent == null || !_soundEvent.IsValid) return;
        
        AudioClip clip = _soundEvent.GetClip();
        PlayClip(clip, _soundEvent.volume, _soundEvent.GetPitch());
    }
    
    private void PlayAllClips()
    {
        if (_soundEvent == null || _soundEvent.clips == null) return;
        
        StopPreview();
        
        // Play each clip with a small delay
        float delay = 0f;
        foreach (var clip in _soundEvent.clips)
        {
            if (clip != null)
            {
                EditorApplication.delayCall += () =>
                {
                    PlayClip(clip, _soundEvent.volume, _soundEvent.pitch);
                };
                delay += clip.length + 0.2f;
            }
        }
    }
    
    private void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        
        StopPreview();
        
        // Create temporary audio source for preview
        GameObject go = new GameObject("SoundEventPreview");
        go.hideFlags = HideFlags.HideAndDontSave;
        _previewSource = go.AddComponent<AudioSource>();
        _previewSource.clip = clip;
        _previewSource.volume = volume;
        _previewSource.pitch = pitch;
        _previewSource.Play();
        
        // Auto-cleanup when done
        EditorApplication.delayCall += () =>
        {
            if (_previewSource != null && !_previewSource.isPlaying)
            {
                StopPreview();
            }
        };
    }
    
    private void StopPreview()
    {
        if (_previewSource != null)
        {
            _previewSource.Stop();
            DestroyImmediate(_previewSource.gameObject);
            _previewSource = null;
        }
        
        // Also stop any Unity internal preview
        StopUnityPreview();
    }
    
    private void StopUnityPreview()
    {
        // Use reflection to stop Unity's internal audio preview
        var audioUtilClass = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        if (audioUtilClass != null)
        {
            var method = audioUtilClass.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public);
            method?.Invoke(null, null);
        }
    }
}
