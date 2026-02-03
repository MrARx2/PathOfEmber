using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor wizard to create a Death Popup UI panel on an existing Canvas.
/// Access via: Tools > Path of Ember > Setup Death Popup
/// </summary>
public class DeathPopupSetupWizard : EditorWindow
{
    private Canvas targetCanvas;
    
    // Styling
    private Color panelColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
    private Color buttonColor = new Color(0.2f, 0.25f, 0.35f, 1f);
    private string titleText = "YOU DIED";
    private Color titleColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    
    [MenuItem("Tools/Path of Ember/UI/Setup Death Popup")]
    public static void ShowWindow()
    {
        var window = GetWindow<DeathPopupSetupWizard>("Death Popup Setup");
        window.minSize = new Vector2(400, 350);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Death Popup Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        // Canvas selection
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Target Canvas", EditorStyles.boldLabel);
        targetCanvas = (Canvas)EditorGUILayout.ObjectField("Canvas", targetCanvas, typeof(Canvas), true);
        
        if (targetCanvas == null)
        {
            EditorGUILayout.HelpBox("Select the Canvas to add the Death Popup to (usually GameCanvas).", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Styling
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Styling", EditorStyles.boldLabel);
        panelColor = EditorGUILayout.ColorField("Panel Background", panelColor);
        buttonColor = EditorGUILayout.ColorField("Button Color", buttonColor);
        titleText = EditorGUILayout.TextField("Title Text", titleText);
        titleColor = EditorGUILayout.ColorField("Title Color", titleColor);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(15);
        
        // Create button
        GUI.enabled = targetCanvas != null;
        if (GUILayout.Button("Create Death Popup", GUILayout.Height(40)))
        {
            CreateDeathPopup();
        }
        GUI.enabled = true;
        
        EditorGUILayout.Space(10);
        
        // Preview
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Hierarchy Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "[Canvas]\n" +
            "└── DeathPopupPanel (hidden by default)\n" +
            "    ├── Overlay (dark background)\n" +
            "    ├── ContentPanel\n" +
            "    │   ├── TitleText (\"YOU DIED\")\n" +
            "    │   ├── RestartButton\n" +
            "    │   └── MainMenuButton",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }
    
    private void CreateDeathPopup()
    {
        if (targetCanvas == null) return;
        
        Undo.SetCurrentGroupName("Create Death Popup");
        int undoGroup = Undo.GetCurrentGroup();
        
        // Root panel (full screen)
        var popupGO = new GameObject("DeathPopupPanel");
        Undo.RegisterCreatedObjectUndo(popupGO, "Create Death Popup");
        popupGO.transform.SetParent(targetCanvas.transform, false);
        
        var popupRect = popupGO.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;
        
        // Dark overlay
        var overlayGO = new GameObject("Overlay");
        overlayGO.transform.SetParent(popupGO.transform, false);
        
        var overlayRect = overlayGO.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        
        var overlayImage = overlayGO.AddComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.7f);
        overlayImage.raycastTarget = true;
        
        // Content panel (centered)
        var contentGO = new GameObject("ContentPanel");
        contentGO.transform.SetParent(popupGO.transform, false);
        
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(400, 350);
        
        var contentImage = contentGO.AddComponent<Image>();
        contentImage.color = panelColor;
        
        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(40, 40, 50, 50);
        contentLayout.spacing = 30;
        contentLayout.childAlignment = TextAnchor.MiddleCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        // Title
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(contentGO.transform, false);
        
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 80);
        
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = titleText;
        titleTMP.fontSize = 56;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = titleColor;
        
        var titleLayout = titleGO.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 80;
        
        // Restart button
        CreateButton(contentGO.transform, "RestartButton", "Restart");
        
        // Main Menu button
        CreateButton(contentGO.transform, "MainMenuButton", "Back to Menu");
        
        // Hide by default
        popupGO.SetActive(false);
        
        Selection.activeGameObject = popupGO;
        Undo.CollapseUndoOperations(undoGroup);
        
        EditorUtility.DisplayDialog("Success",
            "Death Popup created!\n\n" +
            "Next steps:\n" +
            "1. Assign 'DeathPopupPanel' to PlayerHealth\n" +
            "2. (Optional) Add DeathPopupController script\n" +
            "3. Style the panel as desired",
            "OK");
    }
    
    private void CreateButton(Transform parent, string name, string label)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);
        
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0, 60);
        
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = buttonColor;
        
        buttonGO.AddComponent<Button>();
        
        var buttonLayout = buttonGO.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 60;
        
        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.text = label;
        textTMP.fontSize = 28;
        textTMP.alignment = TextAlignmentOptions.Center;
        textTMP.color = Color.white;
    }
}
