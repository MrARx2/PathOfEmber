using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Audio; // Namespace for AudioManager

/// <summary>
/// Controls the Sound Settings panel.
/// connects 4 sliders (Master, BGM, SFX, Ambient) to the AudioManager.
/// </summary>
public class SoundSettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider ambientSlider;

    [Header("Labels (Optional)")]
    [SerializeField] private TextMeshProUGUI masterLabel;
    [SerializeField] private TextMeshProUGUI bgmLabel;
    [SerializeField] private TextMeshProUGUI sfxLabel;
    [SerializeField] private TextMeshProUGUI ambientLabel;

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    private void Start()
    {
        // Setup listeners
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        if (ambientSlider != null) ambientSlider.onValueChanged.AddListener(OnAmbientChanged);

        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        // Initialize values from AudioManager
        RefreshValues();
    }

    private void OnEnable()
    {
        // Refresh values whenever the panel opens
        RefreshValues();
    }

    private void RefreshValues()
    {
        if (AudioManager.Instance == null) return;

        UpdateSlider(masterSlider, masterLabel, AudioManager.Instance.GetMasterVolume());
        UpdateSlider(bgmSlider, bgmLabel, AudioManager.Instance.GetBGMVolume());
        UpdateSlider(sfxSlider, sfxLabel, AudioManager.Instance.GetSFXVolume());
        UpdateSlider(ambientSlider, ambientLabel, AudioManager.Instance.GetAmbientVolume());
    }

    private void UpdateSlider(Slider slider, TextMeshProUGUI label, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
        UpdateLabel(label, value);
    }

    private void UpdateLabel(TextMeshProUGUI label, float value)
    {
        if (label != null)
        {
            label.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }

    public void OnMasterChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(value);
        UpdateLabel(masterLabel, value);
    }

    public void OnBGMChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetBGMVolume(value);
        UpdateLabel(bgmLabel, value);
    }

    public void OnSFXChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(value);
        UpdateLabel(sfxLabel, value);
    }

    public void OnAmbientChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetAmbientVolume(value);
        UpdateLabel(ambientLabel, value);
    }

    private void OnBackClicked()
    {
        gameObject.SetActive(false);
    }
}
