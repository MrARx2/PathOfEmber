using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FloatingJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Joystick Settings")]
    [SerializeField] private float joystickRange = 50f;
    [SerializeField] private float deadZone = 0.1f;
    
    [Header("UI References")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private CanvasGroup canvasGroup;
    
    // Public input values
    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }
    public bool IsActive { get; private set; }
    
    private void Start()
    {
        // Auto-find components if not assigned
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        
        // Hide joystick at start
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        
        IsActive = false;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        IsActive = true;
        
        // Show joystick
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        
        // Move joystick background to touch position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background.parent as RectTransform, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 localPoint);
        
        background.anchoredPosition = localPoint;
        
        // Start dragging immediately
        OnDrag(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!IsActive) return;
        
        // Get touch position relative to joystick background
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 position);
        
        // Clamp to joystick range
        position = Vector2.ClampMagnitude(position, joystickRange);
        handle.anchoredPosition = position;
        
        // Calculate normalized input (-1 to 1)
        Vector2 normalized = position / joystickRange;
        
        // Apply dead zone
        if (normalized.magnitude < deadZone)
        {
            normalized = Vector2.zero;
        }
        
        Horizontal = normalized.x;
        Vertical = normalized.y;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        IsActive = false;
        
        // Hide joystick
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        
        // Reset handle position
        handle.anchoredPosition = Vector2.zero;
        
        // Reset input values
        Horizontal = 0f;
        Vertical = 0f;
    }
}
