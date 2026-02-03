using UnityEngine;
using UnityEditor;
#if UNITY_2022_2_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

public class CameraShakeSetup : EditorWindow
{
    [MenuItem("Tools/Path of Ember/Visuals/Setup Camera Shake")]
    public static void Setup()
    {
        SetupManager();
        SetupCamera();
    }

    private static void SetupManager()
    {
        // 1. Find or Create Manager
        var manager = Object.FindFirstObjectByType<CameraShakeManager>();
        if (manager == null)
        {
            GameObject obj = new GameObject("CameraShakeManager");
            manager = obj.AddComponent<CameraShakeManager>();
            Undo.RegisterCreatedObjectUndo(obj, "Create CameraShakeManager");
            Debug.Log("[Setup] Created CameraShakeManager");
        }
        else
        {
            Debug.Log("[Setup] Found existing CameraShakeManager");
        }

        // 2. Setup Impulse Source
        var source = manager.GetComponent<CinemachineImpulseSource>();
        if (source == null)
        {
            source = manager.gameObject.AddComponent<CinemachineImpulseSource>();
        }

        // 3. Assign 6D Shake Profile
        // Try to find the default profile
        string[] guids = AssetDatabase.FindAssets("6D Shake t:NoiseSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var noiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(path);
            
            if (noiseProfile != null)
            {
#if UNITY_2022_2_OR_NEWER
                source.ImpulseDefinition.RawSignal = noiseProfile;
#else
                source.m_ImpulseDefinition.m_RawSignal = noiseProfile;
#endif
                EditorUtility.SetDirty(source);
                Debug.Log($"[Setup] Assigned '6D Shake' profile to Impulse Source");
            }
        }
        else
        {
            Debug.LogWarning("[Setup] Could not find '6D Shake' profile! Please assign a Signal Source manually to the CameraShakeManager.");
        }
    }

    private static void SetupCamera()
    {
        // 1. Find Virtual Camera
#if UNITY_2022_2_OR_NEWER
        var vcam = Object.FindFirstObjectByType<CinemachineCamera>();
        if (vcam == null)
        {
            Debug.LogError("[Setup] No Cinemachine Camera found in scene!");
            return;
        }
#else
        var vcam = Object.FindFirstObjectByType<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            Debug.LogError("[Setup] No Cinemachine Virtual Camera found in scene!");
            return;
        }
#endif

        // 2. Add Impulse Listener
        var listener = vcam.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
        {
            listener = vcam.gameObject.AddComponent<CinemachineImpulseListener>();
            Undo.RegisterCreatedObjectUndo(listener, "Add Impulse Listener");
            Debug.Log("[Setup] Added CinemachineImpulseListener to Virtual Camera");
        }
        else
        {
            Debug.Log("[Setup] Camera already has Impulse Listener");
        }
    }
}
