using UnityEngine;
using TMPro;

/// <summary>
/// Floating damage number that pops up and fades away.
/// Attach this to a prefab with a TextMeshPro component.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float fadeStartTime = 0.5f;
    [SerializeField] private float scaleUpAmount = 1.2f;
    [SerializeField] private float scaleUpDuration = 0.1f;

    [Header("Movement")]
    [SerializeField] private float randomHorizontalOffset = 0.5f;
    [SerializeField] private AnimationCurve floatCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private TextMeshPro textMesh;
    private TextMeshProUGUI textMeshUI;
    private float timer;
    private Vector3 startPos;
    private Vector3 moveDirection;
    private Color originalColor;
    private Vector3 originalScale;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        textMeshUI = GetComponent<TextMeshProUGUI>();
        originalScale = transform.localScale;
    }

    private void Start()
    {
        startPos = transform.position;
        
        // Random horizontal offset for variety
        float randomX = Random.Range(-randomHorizontalOffset, randomHorizontalOffset);
        moveDirection = new Vector3(randomX, 1f, 0f).normalized;

        if (textMesh != null)
            originalColor = textMesh.color;
        else if (textMeshUI != null)
            originalColor = textMeshUI.color;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float normalizedTime = timer / lifetime;

        // Float upward with curve
        float curveValue = floatCurve.Evaluate(normalizedTime);
        transform.position = startPos + moveDirection * (curveValue * floatSpeed * lifetime);

        // Scale pop effect at start
        if (timer < scaleUpDuration)
        {
            float scaleT = timer / scaleUpDuration;
            float scale = Mathf.Lerp(1f, scaleUpAmount, scaleT);
            transform.localScale = originalScale * scale;
        }
        else if (timer < scaleUpDuration * 2)
        {
            float scaleT = (timer - scaleUpDuration) / scaleUpDuration;
            float scale = Mathf.Lerp(scaleUpAmount, 1f, scaleT);
            transform.localScale = originalScale * scale;
        }

        // Fade out
        if (timer > fadeStartTime)
        {
            float fadeT = (timer - fadeStartTime) / (lifetime - fadeStartTime);
            Color fadedColor = originalColor;
            fadedColor.a = Mathf.Lerp(1f, 0f, fadeT);

            if (textMesh != null)
                textMesh.color = fadedColor;
            else if (textMeshUI != null)
                textMeshUI.color = fadedColor;
        }

        // Billboard - always face camera
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }

    /// <summary>
    /// Set up the damage popup with the damage value.
    /// </summary>
    public void Setup(int damage, Color? color = null)
    {
        string text = damage.ToString();
        
        if (textMesh != null)
        {
            textMesh.text = text;
            if (color.HasValue)
            {
                textMesh.color = color.Value;
                originalColor = color.Value;
            }
        }
        else if (textMeshUI != null)
        {
            textMeshUI.text = text;
            if (color.HasValue)
            {
                textMeshUI.color = color.Value;
                originalColor = color.Value;
            }
        }
    }

    /// <summary>
    /// Set up with custom text (for "CRIT!", "MISS", etc.)
    /// </summary>
    public void Setup(string text, Color? color = null)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
            if (color.HasValue)
            {
                textMesh.color = color.Value;
                originalColor = color.Value;
            }
        }
        else if (textMeshUI != null)
        {
            textMeshUI.text = text;
            if (color.HasValue)
            {
                textMeshUI.color = color.Value;
                originalColor = color.Value;
            }
        }
    }
}
