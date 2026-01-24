using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor wizard to create the In-Game Pause Menu UI hierarchy on an existing Canvas.
/// Access via: Tools > Path of Ember > Setup In-Game Menu
/// </summary>
public class MenuUISetupWizard : EditorWindow
{
    // Target canvas
    private Canvas targetCanvas;
    
    // Grid settings
    private int gridColumns = 4;
    private Vector2 iconSize = new Vector2(100, 100);
    private Vector2 spacing = new Vector2(15, 15);
    private int previewIconCount = 6;
    
    // Colors
    private Color panelColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
    private Color buttonColor = new Color(0.2f, 0.25f, 0.35f, 1f);
    
    [MenuItem("Tools/Path of Ember/Setup In-Game Menu")]
    public static void ShowWindow()
    {
        var window = GetWindow<MenuUISetupWizard>("In-Game Menu Setup");
        window.minSize = new Vector2(450, 500);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("In-Game Pause Menu Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        // Canvas selection
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Target Canvas", EditorStyles.boldLabel);
        targetCanvas = (Canvas)EditorGUILayout.ObjectField("Canvas", targetCanvas, typeof(Canvas), true);
        
        if (targetCanvas == null)
        {
            EditorGUILayout.HelpBox("Select the Canvas to add the menu UI to.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Grid configuration
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Abilities Grid Settings", EditorStyles.boldLabel);
        
        gridColumns = EditorGUILayout.IntSlider("Columns", gridColumns, 2, 6);
        iconSize = EditorGUILayout.Vector2Field("Icon Size", iconSize);
        spacing = EditorGUILayout.Vector2Field("Spacing", spacing);
        previewIconCount = EditorGUILayout.IntSlider("Preview Icons", previewIconCount, 0, 12);
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Color configuration
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Colors", EditorStyles.boldLabel);
        
        panelColor = EditorGUILayout.ColorField("Panel Background", panelColor);
        buttonColor = EditorGUILayout.ColorField("Button Color", buttonColor);
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(15);
        
        // Create button
        GUI.enabled = targetCanvas != null;
        if (GUILayout.Button("Create Menu UI Hierarchy", GUILayout.Height(40)))
        {
            CreateMenuUI();
        }
        GUI.enabled = true;
        
        EditorGUILayout.Space(10);
        
        // Preview of what will be created
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Hierarchy Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "[Canvas]\n" +
            "├── HamburgerButton\n" +
            "└── MenuPanel\n" +
            "    ├── TitleText (\"PAUSED\")\n" +
            "    ├── AbilitiesSection\n" +
            "    │   ├── SectionLabel\n" +
            "    │   └── AbilitiesGridPanel (ScrollRect)\n" +
            "    │       └── Viewport → Content → TalentIcons\n" +
            "    └── ButtonsPanel\n" +
            "        ├── SoundSettingsButton\n" +
            "        ├── BackToMainMenuButton\n" +
            "        └── ContinueButton",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }
    
    private void CreateMenuUI()
    {
        if (targetCanvas == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a Canvas first.", "OK");
            return;
        }
        
        Undo.SetCurrentGroupName("Create In-Game Menu UI");
        int undoGroup = Undo.GetCurrentGroup();
        
        // Create hamburger button
        var hamburgerButton = CreateHamburgerButton(targetCanvas.transform);
        
        // Create menu panel (hidden by default)
        var menuPanel = CreateMenuPanel(targetCanvas.transform);
        
        // Focus on the created panel
        Selection.activeGameObject = menuPanel;
        
        Undo.CollapseUndoOperations(undoGroup);
        
        EditorUtility.DisplayDialog("Success", 
            "In-Game Menu UI created!\n\n" +
            "Next steps:\n" +
            "1. Position the HamburgerButton (top-right)\n" +
            "2. Resize MenuPanel as needed\n" +
            "3. Attach InGameMenuController script\n" +
            "4. Assign references in Inspector", "OK");
    }
    
    private GameObject CreateHamburgerButton(Transform parent)
    {
        // Create button
        var buttonGO = new GameObject("HamburgerButton");
        Undo.RegisterCreatedObjectUndo(buttonGO, "Create Hamburger Button");
        buttonGO.transform.SetParent(parent, false);
        
        // RectTransform - top right
        var rect = buttonGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -20);
        rect.sizeDelta = new Vector2(60, 60);
        
        // Image
        var image = buttonGO.AddComponent<Image>();
        image.color = buttonColor;
        
        // Button
        buttonGO.AddComponent<Button>();
        
        // Add placeholder icon (3 horizontal lines)
        CreateHamburgerIcon(buttonGO.transform);
        
        return buttonGO;
    }
    
    private void CreateHamburgerIcon(Transform parent)
    {
        for (int i = 0; i < 3; i++)
        {
            var line = new GameObject($"Line_{i}");
            line.transform.SetParent(parent, false);
            
            var rect = line.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.5f);
            rect.anchorMax = new Vector2(0.8f, 0.5f);
            rect.anchoredPosition = new Vector2(0, (1 - i) * 12 - 12);
            rect.sizeDelta = new Vector2(0, 6);
            
            var image = line.AddComponent<Image>();
            image.color = Color.white;
        }
    }
    
    private GameObject CreateMenuPanel(Transform parent)
    {
        // MenuPanel container
        var panelGO = new GameObject("MenuPanel");
        Undo.RegisterCreatedObjectUndo(panelGO, "Create Menu Panel");
        panelGO.transform.SetParent(parent, false);
        
        var rect = panelGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(40, 100);
        rect.offsetMax = new Vector2(-40, -100);
        
        var image = panelGO.AddComponent<Image>();
        image.color = panelColor;
        
        // Add VerticalLayoutGroup for easy arrangement
        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Title
        CreateTitleText(panelGO.transform);
        
        // Abilities section
        CreateAbilitiesSection(panelGO.transform);
        
        // Buttons panel
        CreateButtonsPanel(panelGO.transform);
        
        // Hide by default
        panelGO.SetActive(false);
        
        return panelGO;
    }
    
    private void CreateTitleText(Transform parent)
    {
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(parent, false);
        
        var rect = titleGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 60);
        
        var text = titleGO.AddComponent<TextMeshProUGUI>();
        text.text = "PAUSED";
        text.fontSize = 48;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        
        var layoutElem = titleGO.AddComponent<LayoutElement>();
        layoutElem.preferredHeight = 60;
    }
    
    private void CreateAbilitiesSection(Transform parent)
    {
        // Container
        var sectionGO = new GameObject("AbilitiesSection");
        sectionGO.transform.SetParent(parent, false);
        
        var sectionRect = sectionGO.AddComponent<RectTransform>();
        
        var sectionLayout = sectionGO.AddComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 10;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = false;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;
        
        var layoutElem = sectionGO.AddComponent<LayoutElement>();
        layoutElem.flexibleHeight = 1; // Take remaining space
        layoutElem.minHeight = 150;
        
        // Section label
        var labelGO = new GameObject("SectionLabel");
        labelGO.transform.SetParent(sectionGO.transform, false);
        
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(0, 30);
        
        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = "Talents You Obtained";
        labelText.fontSize = 24;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = new Color(0.8f, 0.8f, 0.8f);
        
        var labelLayout = labelGO.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 30;
        
        // Grid panel with scroll
        CreateAbilitiesGrid(sectionGO.transform);
    }
    
    private void CreateAbilitiesGrid(Transform parent)
    {
        // ScrollRect container
        var gridPanelGO = new GameObject("AbilitiesGridPanel");
        gridPanelGO.transform.SetParent(parent, false);
        
        var panelRect = gridPanelGO.AddComponent<RectTransform>();
        
        var panelImage = gridPanelGO.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.3f);
        
        var scrollRect = gridPanelGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20;
        
        var panelLayout = gridPanelGO.AddComponent<LayoutElement>();
        panelLayout.flexibleHeight = 1;
        panelLayout.minHeight = 100;
        
        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(gridPanelGO.transform, false);
        
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        var viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = Color.clear;
        
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        
        // GridLayoutGroup
        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = iconSize;
        grid.spacing = spacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = gridColumns;
        grid.padding = new RectOffset(10, 10, 10, 10);
        
        // ContentSizeFitter for auto height
        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Wire up ScrollRect
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        
        // Create preview icons
        for (int i = 0; i < previewIconCount; i++)
        {
            CreatePreviewTalentIcon(contentGO.transform, i);
        }
    }
    
    private void CreatePreviewTalentIcon(Transform parent, int index)
    {
        var iconGO = new GameObject($"TalentIcon_{index}");
        iconGO.transform.SetParent(parent, false);
        
        var rect = iconGO.AddComponent<RectTransform>();
        rect.sizeDelta = iconSize;
        
        var image = iconGO.AddComponent<Image>();
        // Create a colored placeholder based on index
        float hue = (index * 0.15f) % 1f;
        image.color = Color.HSVToRGB(hue, 0.5f, 0.8f);
        
        // Add button for interaction
        iconGO.AddComponent<Button>();
    }
    
    private void CreateButtonsPanel(Transform parent)
    {
        // Container
        var buttonsGO = new GameObject("ButtonsPanel");
        buttonsGO.transform.SetParent(parent, false);
        
        var rect = buttonsGO.AddComponent<RectTransform>();
        
        var layout = buttonsGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        
        var layoutElem = buttonsGO.AddComponent<LayoutElement>();
        layoutElem.preferredHeight = 200;
        
        // Create the three buttons
        CreateMenuButton(buttonsGO.transform, "SoundSettingsButton", "Sound Settings");
        CreateMenuButton(buttonsGO.transform, "BackToMainMenuButton", "Back To Main Menu");
        CreateMenuButton(buttonsGO.transform, "ContinueButton", "Continue");
    }
    
    private void CreateMenuButton(Transform parent, string name, string label)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);
        
        var rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 50);
        
        var image = buttonGO.AddComponent<Image>();
        image.color = buttonColor;
        
        buttonGO.AddComponent<Button>();
        
        var layoutElem = buttonGO.AddComponent<LayoutElement>();
        layoutElem.preferredWidth = 300;
        layoutElem.preferredHeight = 50;
        
        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }
}
