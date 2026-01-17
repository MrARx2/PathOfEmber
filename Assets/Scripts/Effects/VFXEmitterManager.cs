using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized manager that handles distance-based culling for all VFX emitters.
/// Automatically finds emitters on the specified layer.
/// Uses rectangular bounds for optimal mobile (vertical view) culling.
/// Singleton pattern - place on a persistent GameObject in the scene.
/// </summary>
public class VFXEmitterManager : MonoBehaviour
{
    public static VFXEmitterManager Instance { get; private set; }
    
    [Header("Detection Settings")]
    [Tooltip("Layer name for VFX emitters")]
    public string vfxLayerName = "VFX Emitters";
    
    [Header("Visibility Bounds (Rectangular)")]
    [Tooltip("Horizontal range (X axis) - set narrower for portrait/vertical screens")]
    public float horizontalRange = 4f;
    
    [Tooltip("Vertical range (Z axis) - set wider to match vertical screen space")]
    public float verticalRange = 7f;
    
    [Header("Performance Settings")]
    [Tooltip("How many emitters to process per frame. Lower = smoother but slower response.")]
    public int emittersPerFrame = 10;
    
    [Tooltip("How often to scan for new emitters (for procedural chunks)")]
    public float rescanInterval = 2f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    private List<EmitterData> emitters = new List<EmitterData>();
    private Transform playerTransform;
    private int currentIndex = 0;
    private float nextRescanTime;
    private int vfxLayerMask;
    
    private class EmitterData
    {
        public Transform transform;
        public ParticleSystem[] particleSystems;
        public bool isPlaying;
        public bool isValid;
        
        public EmitterData(GameObject go)
        {
            transform = go.transform;
            particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
            isPlaying = true;
            isValid = particleSystems.Length > 0;
        }
    }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        vfxLayerMask = LayerMask.NameToLayer(vfxLayerName);
    }
    
    private void Start()
    {
        FindPlayer();
        ScanForEmitters();
        nextRescanTime = Time.time + rescanInterval;
    }
    
    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }
        
        // Periodic rescan for new emitters (from procedural chunks)
        if (Time.time >= nextRescanTime)
        {
            nextRescanTime = Time.time + rescanInterval;
            ScanForEmitters();
        }
        
        // Process a batch of emitters each frame
        ProcessEmitterBatch();
    }
    
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }
    
    /// <summary>
    /// Scan scene for all VFX emitters on the specified layer.
    /// Called periodically to catch newly spawned emitters.
    /// </summary>
    public void ScanForEmitters()
    {
        // Clean up destroyed emitters first
        emitters.RemoveAll(e => e.transform == null);
        
        // Create a hash set of existing transforms for fast lookup
        HashSet<Transform> existingTransforms = new HashSet<Transform>();
        foreach (var emitter in emitters)
        {
            if (emitter.transform != null)
                existingTransforms.Add(emitter.transform);
        }
        
        // Find all GameObjects on the VFX layer
        ParticleSystem[] allParticles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        
        foreach (var ps in allParticles)
        {
            // Check if on correct layer and not already tracked
            if (ps.gameObject.layer == vfxLayerMask && !existingTransforms.Contains(ps.transform))
            {
                // Only track root particle system to avoid duplicates
                ParticleSystem rootPS = ps.transform.GetComponentInParent<ParticleSystem>();
                Transform rootTransform = (rootPS != null) ? rootPS.transform : ps.transform;
                
                if (!existingTransforms.Contains(rootTransform))
                {
                    EmitterData data = new EmitterData(rootTransform.gameObject);
                    if (data.isValid)
                    {
                        emitters.Add(data);
                        existingTransforms.Add(rootTransform);
                    }
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[VFXEmitterManager] Tracking {emitters.Count} emitters");
        }
    }
    
    /// <summary>
    /// Register an emitter manually (useful for runtime instantiation).
    /// </summary>
    public void RegisterEmitter(GameObject emitterObject)
    {
        if (emitterObject == null) return;
        
        // Check if already registered
        foreach (var existing in emitters)
        {
            if (existing.transform == emitterObject.transform)
                return;
        }
        
        EmitterData data = new EmitterData(emitterObject);
        if (data.isValid)
        {
            emitters.Add(data);
        }
    }
    
    /// <summary>
    /// Unregister an emitter (call before destroying).
    /// </summary>
    public void UnregisterEmitter(GameObject emitterObject)
    {
        if (emitterObject == null) return;
        emitters.RemoveAll(e => e.transform == emitterObject.transform);
    }
    
    private void ProcessEmitterBatch()
    {
        if (emitters.Count == 0) return;
        
        Vector3 playerPos = playerTransform.position;
        int processed = 0;
        
        while (processed < emittersPerFrame && emitters.Count > 0)
        {
            // Wrap around if needed
            if (currentIndex >= emitters.Count)
                currentIndex = 0;
            
            EmitterData emitter = emitters[currentIndex];
            
            // Clean up destroyed emitters
            if (emitter.transform == null)
            {
                emitters.RemoveAt(currentIndex);
                continue;
            }
            
            // Calculate rectangular distance (screen-aligned for vertical mobile view)
            Vector3 offset = emitter.transform.position - playerPos;
            bool shouldPlay = Mathf.Abs(offset.x) <= horizontalRange && Mathf.Abs(offset.z) <= verticalRange;
            
            if (shouldPlay != emitter.isPlaying)
            {
                emitter.isPlaying = shouldPlay;
                SetEmitterActive(emitter, shouldPlay);
            }
            
            currentIndex++;
            processed++;
        }
    }
    
    private void SetEmitterActive(EmitterData emitter, bool active)
    {
        foreach (var ps in emitter.particleSystems)
        {
            if (ps == null) continue;
            
            if (active)
            {
                if (!ps.isPlaying)
                    ps.Play();
            }
            else
            {
                if (ps.isPlaying)
                    ps.Pause();
            }
        }
    }
    
    private void OnValidate()
    {
        // Clamp ranges to reasonable values
        horizontalRange = Mathf.Max(0.1f, horizontalRange);
        verticalRange = Mathf.Max(0.1f, verticalRange);
    }
    
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        int playing = 0;
        int paused = 0;
        foreach (var e in emitters)
        {
            if (e.isPlaying) playing++;
            else paused++;
        }
        
        GUI.Label(new Rect(10, 10, 300, 60), 
            $"VFX Emitters: {emitters.Count}\nPlaying: {playing} | Paused: {paused}");
    }
}
