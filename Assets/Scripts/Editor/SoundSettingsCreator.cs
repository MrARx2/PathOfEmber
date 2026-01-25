using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
public class SoundSettingsCreator
{
    [MenuItem("Tools/Path of Ember/UI/Setup Sound Settings Panel")]
    public static void CreateSoundSettingsPanel()
    {
        // Find Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in the scene! Please create a UI Canvas first.", "OK");
            return;
        }

        // 1. Create Main Panel
        GameObject panelObj = new GameObject("SoundSettingsPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f); // Dark background
        
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add Logic Script
        SoundSettingsUI settingsScript = panelObj.AddComponent<SoundSettingsUI>();

        // 2. Create Layout Container (Vertical)
        GameObject layoutObj = new GameObject("ContentLayout");
        layoutObj.transform.SetParent(panelObj.transform, false);
        
        RectTransform layoutRect = layoutObj.AddComponent<RectTransform>();
        layoutRect.anchorMin = new Vector2(0.3f, 0.2f);
        layoutRect.anchorMax = new Vector2(0.7f, 0.8f);
        layoutRect.offsetMin = Vector2.zero;
        layoutRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = layoutObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 30;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.padding = new RectOffset(20, 20, 20, 20);

        // 3. Create Title
        CreateText(layoutObj.transform, "Sound Settings", 42, FontStyles.Bold);

        // 4. Create Sliders
        (Slider master, TextMeshProUGUI masterLbl) = CreateSliderRow(layoutObj.transform, "Master Volume");
        (Slider bgm, TextMeshProUGUI bgmLbl) = CreateSliderRow(layoutObj.transform, "Music Volume");
        (Slider sfx, TextMeshProUGUI sfxLbl) = CreateSliderRow(layoutObj.transform, "SFX Volume");
        (Slider ambient, TextMeshProUGUI ambientLbl) = CreateSliderRow(layoutObj.transform, "Ambient Volume");

        // 5. Create Back Button
        Button backBtn = CreateBackButton(layoutObj.transform);

        // 6. Assign References
        Undo.RecordObject(settingsScript, "Setup Sound Settings");
        SerializedObject so = new SerializedObject(settingsScript);
        
        so.FindProperty("masterSlider").objectReferenceValue = master;
        so.FindProperty("bgmSlider").objectReferenceValue = bgm;
        so.FindProperty("sfxSlider").objectReferenceValue = sfx;
        so.FindProperty("ambientSlider").objectReferenceValue = ambient;
        
        so.FindProperty("masterLabel").objectReferenceValue = masterLbl;
        so.FindProperty("bgmLabel").objectReferenceValue = bgmLbl;
        so.FindProperty("sfxLabel").objectReferenceValue = sfxLbl;
        so.FindProperty("ambientLabel").objectReferenceValue = ambientLbl;
        
        so.FindProperty("backButton").objectReferenceValue = backBtn;
        
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panelObj;
    }

    private static (Slider, TextMeshProUGUI) CreateSliderRow(Transform parent, string labelText)
    {
        GameObject row = new GameObject($"Row_{labelText}");
        row.transform.SetParent(parent, false);
        
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        le.minHeight = 60;

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = labelText;
        labelTmp.fontSize = 24;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.4f, 1);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        // Slider
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        Slider slider = sliderObj.AddComponent<Slider>();
        
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.45f, 0.2f);
        sliderRect.anchorMax = new Vector2(0.85f, 0.8f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        // Slider Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.25f);
        bgRect.anchorMax = new Vector2(1, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.2f); // Green fill
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        
        slider.targetGraphic = bgImg;
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;

        // Handle
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(0, 0);
        handleAreaRect.offsetMax = new Vector2(0, 0);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 0);
        handleRect.anchorMin = new Vector2(0, 0);
        handleRect.anchorMax = new Vector2(0, 1);
        
        slider.handleRect = handleRect;

        // Value Label
        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI valTmp = valObj.AddComponent<TextMeshProUGUI>();
        valTmp.text = "100%";
        valTmp.fontSize = 20;
        valTmp.alignment = TextAlignmentOptions.MidlineRight;
        
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.85f, 0);
        valRect.anchorMax = new Vector2(1, 1);
        valRect.offsetMin = Vector2.zero;
        valRect.offsetMax = Vector2.zero;
        
        return (slider, valTmp);
    }

    private static void CreateText(Transform parent, string content, float fontSize, FontStyles style)
    {
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        
        LayoutElement le = txtObj.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize * 1.5f;
        le.minHeight = fontSize;
    }

    private static Button CreateBackButton(Transform parent)
    {
        GameObject btnObj = new GameObject("BackButton");
        btnObj.transform.SetParent(parent, false);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.8f, 0.2f, 0.2f); // Reddish
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Back";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        le.minHeight = 50;
        le.preferredWidth = 200;

        return btn;
    }
}
#endif
