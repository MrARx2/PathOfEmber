using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool for quickly setting up clean, professional trails on projectiles.
/// Access via Tools > VFX > Trail Setup
/// </summary>
public class TrailSetup : EditorWindow
{
    // Trail Style Presets
    public enum TrailStyle
    {
        Fire,       // Orange/yellow fire trail
        Ice,        // Blue/cyan ice trail  
        Venom,      // Purple/green poison trail
        Energy,     // White/blue energy trail
        Custom      // Use custom colors
    }

    private TrailStyle selectedStyle = TrailStyle.Fire;
    private Color customHeadColor = Color.white;
    private Color customTailColor = new Color(1f, 0.5f, 0f, 0f);
    
    private float trailTime = 0.15f;
    private float trailWidth = 0.25f;
    private float minVertexDistance = 0.05f;
    
    private bool createNewTrail = false;
    private GameObject targetObject;

    [MenuItem("Tools/VFX/Trail Setup")]
    public static void ShowWindow()
    {
        GetWindow<TrailSetup>("Trail Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("🔥 Trail Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Target object
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        targetObject = EditorGUILayout.ObjectField("Projectile/Object", targetObject, typeof(GameObject), true) as GameObject;
        
        if (targetObject == null && Selection.activeGameObject != null)
        {
            if (GUILayout.Button("Use Selected Object"))
            {
                targetObject = Selection.activeGameObject;
            }
        }
        
        EditorGUILayout.Space(10);

        // Style selection
        EditorGUILayout.LabelField("Trail Style", EditorStyles.boldLabel);
        selectedStyle = (TrailStyle)EditorGUILayout.EnumPopup("Preset", selectedStyle);
        
        if (selectedStyle == TrailStyle.Custom)
        {
            customHeadColor = EditorGUILayout.ColorField("Head Color", customHeadColor);
            customTailColor = EditorGUILayout.ColorField("Tail Color", customTailColor);
        }
        
        EditorGUILayout.Space(10);

        // Trail settings
        EditorGUILayout.LabelField("Trail Settings", EditorStyles.boldLabel);
        trailTime = EditorGUILayout.Slider("Duration (seconds)", trailTime, 0.05f, 0.5f);
        trailWidth = EditorGUILayout.Slider("Width", trailWidth, 0.1f, 1f);
        minVertexDistance = EditorGUILayout.Slider("Smoothness", minVertexDistance, 0.01f, 0.2f);
        
        EditorGUILayout.HelpBox("Lower smoothness = smoother trail (more vertices)", MessageType.Info);
        
        EditorGUILayout.Space(10);

        // Options
        createNewTrail = EditorGUILayout.Toggle("Create New Trail (if none exists)", createNewTrail);
        
        EditorGUILayout.Space(15);

        // Apply button
        GUI.enabled = targetObject != null;
        if (GUILayout.Button("✨ Apply Trail Settings", GUILayout.Height(35)))
        {
            ApplyTrailSettings();
        }
        GUI.enabled = true;
        
        EditorGUILayout.Space(10);
        
        // Quick presets
        EditorGUILayout.LabelField("Quick Apply (to selection)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔥 Fire")) { selectedStyle = TrailStyle.Fire; ApplyToSelection(); }
        if (GUILayout.Button("❄️ Ice")) { selectedStyle = TrailStyle.Ice; ApplyToSelection(); }
        if (GUILayout.Button("☠️ Venom")) { selectedStyle = TrailStyle.Venom; ApplyToSelection(); }
        if (GUILayout.Button("⚡ Energy")) { selectedStyle = TrailStyle.Energy; ApplyToSelection(); }
        EditorGUILayout.EndHorizontal();
    }

    private void ApplyToSelection()
    {
        if (Selection.activeGameObject != null)
        {
            targetObject = Selection.activeGameObject;
            ApplyTrailSettings();
        }
    }

    private void ApplyTrailSettings()
    {
        if (targetObject == null)
        {
            EditorUtility.DisplayDialog("No Target", "Please select a target object first.", "OK");
            return;
        }

        Undo.RecordObject(targetObject, "Trail Setup");

        // Find or create TrailRenderer
        TrailRenderer trail = targetObject.GetComponentInChildren<TrailRenderer>();
        
        if (trail == null && createNewTrail)
        {
            GameObject trailObj = new GameObject("Trail");
            trailObj.transform.SetParent(targetObject.transform);
            trailObj.transform.localPosition = Vector3.zero;
            trail = trailObj.AddComponent<TrailRenderer>();
            Undo.RegisterCreatedObjectUndo(trailObj, "Create Trail");
        }

        if (trail == null)
        {
            EditorUtility.DisplayDialog("No Trail Found", 
                "No TrailRenderer found on the object. Enable 'Create New Trail' to add one.", "OK");
            return;
        }

        Undo.RecordObject(trail, "Trail Setup");

        // Apply timing settings
        trail.time = trailTime;
        trail.minVertexDistance = minVertexDistance;
        
        // Apply width curve (tapered)
        trail.widthMultiplier = trailWidth;
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, 1f);
        widthCurve.AddKey(0.5f, 0.4f);
        widthCurve.AddKey(1f, 0f);
        trail.widthCurve = widthCurve;

        // Get colors for selected style
        Color headColor, tailColor;
        GetStyleColors(out headColor, out tailColor);

        // Apply color gradient
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(headColor, 0f),
                new GradientColorKey(Color.Lerp(headColor, tailColor, 0.5f), 0.5f),
                new GradientColorKey(tailColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.3f),
                new GradientAlphaKey(0.2f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;

        // Performance settings
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        
        // Try to assign additive material if none
        if (trail.sharedMaterial == null)
        {
            // Try to find a suitable material
            Material defaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            if (defaultMat != null)
                trail.sharedMaterial = defaultMat;
        }

        EditorUtility.SetDirty(trail);
        EditorUtility.SetDirty(targetObject);
        
        Debug.Log($"[TrailSetup] Applied {selectedStyle} trail to {targetObject.name}");
    }

    private void GetStyleColors(out Color head, out Color tail)
    {
        switch (selectedStyle)
        {
            case TrailStyle.Fire:
                head = new Color(1f, 0.95f, 0.6f, 1f);  // Bright yellow-white
                tail = new Color(1f, 0.3f, 0f, 0f);     // Orange to transparent
                break;
            case TrailStyle.Ice:
                head = new Color(0.8f, 0.95f, 1f, 1f);  // Bright cyan-white
                tail = new Color(0.2f, 0.5f, 1f, 0f);   // Blue to transparent
                break;
            case TrailStyle.Venom:
                head = new Color(0.8f, 1f, 0.6f, 1f);   // Bright green-white
                tail = new Color(0.5f, 0f, 0.8f, 0f);   // Purple to transparent
                break;
            case TrailStyle.Energy:
                head = new Color(1f, 1f, 1f, 1f);       // Pure white
                tail = new Color(0.4f, 0.7f, 1f, 0f);   // Light blue to transparent
                break;
            case TrailStyle.Custom:
            default:
                head = customHeadColor;
                tail = customTailColor;
                break;
        }
    }
}
