using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Auto-setup script for prayer wheel FBX with named socket bones.
/// Finds bones by name (C1-C5, R1-R5, L1-L5) and creates socket UI.
/// Attach this to the root of your prayer wheel FBX prefab.
/// </summary>
public class PrayerWheelSetup : MonoBehaviour
{
    [Header("Socket Naming")]
    [SerializeField, Tooltip("Prefix for Common sockets (e.g., 'C' for C1, C2...)")]
    private string commonPrefix = "C";
    
    [SerializeField, Tooltip("Prefix for Rare sockets (e.g., 'R' for R1, R2...)")]
    private string rarePrefix = "R";
    
    [SerializeField, Tooltip("Prefix for Legendary sockets (e.g., 'L' for L1, L2...)")]
    private string legendaryPrefix = "L";

    [Header("Socket UI Settings")]
    [SerializeField, Tooltip("Size of the talent icon on each socket")]
    private Vector2 iconSize = new Vector2(64, 64);
    
    [SerializeField, Tooltip("Scale of the World Space Canvas (adjust to fit wheel size)")]
    private Vector3 canvasScale = new Vector3(0.008f, 0.008f, 0.008f);

    [SerializeField, Tooltip("Position offset for icons relative to socket bone")]
    private Vector3 iconPositionOffset = Vector3.zero;

    [SerializeField, Tooltip("Rotation for icons (e.g. 0, 90, 0 to face outward)")]
    private Vector3 iconRotation = Vector3.zero;
    
    [SerializeField, Tooltip("Optional: Default sprite for empty sockets")]
    private Sprite defaultSocketSprite;

    [Header("Materials (for tinting)")]
    [SerializeField, Tooltip("Material for Common floor tinting")]
    public Material commonMaterial;
    
    [SerializeField, Tooltip("Material for Rare floor tinting")]
    public Material rareMaterial;
    
    [SerializeField, Tooltip("Material for Legendary floor tinting")]
    public Material legendaryMaterial;

    [Header("Auto-Found References (Read-Only)")]
    [SerializeField] private Transform[] commonSockets = new Transform[5];
    [SerializeField] private Transform[] rareSockets = new Transform[5];
    [SerializeField] private Transform[] legendarySockets = new Transform[5];
    
    [SerializeField] private Image[] commonImages = new Image[5];
    [SerializeField] private Image[] rareImages = new Image[5];
    [SerializeField] private Image[] legendaryImages = new Image[5];

    // Public accessors for PrayerWheelController
    public Transform[] CommonSockets => commonSockets;
    public Transform[] RareSockets => rareSockets;
    public Transform[] LegendarySockets => legendarySockets;
    
    public Image[] CommonImages => commonImages;
    public Image[] RareImages => rareImages;
    public Image[] LegendaryImages => legendaryImages;

    /// <summary>
    /// Finds all socket bones and creates UI Images on them.
    /// Call this from the Editor or at runtime.
    /// </summary>
    [ContextMenu("Auto-Setup Wheel")]
    public void AutoSetupWheel()
    {
        FindSocketBones();
        CreateSocketImages();
        Debug.Log($"[PrayerWheelSetup] Setup complete for {gameObject.name}!");
    }

    /// <summary>
    /// Updates properties of existing icons (scale, rotation, offset) without destroying them.
    /// </summary>
    [ContextMenu("Update Icon Transforms")]
    public void UpdateIconTransforms()
    {
        UpdateTransformsForArray(commonImages, commonSockets);
        UpdateTransformsForArray(rareImages, rareSockets);
        UpdateTransformsForArray(legendaryImages, legendarySockets);
        Debug.Log($"[PrayerWheelSetup] Updated transforms for icons.");
    }

    private void FindSocketBones()
    {
        // Find Common sockets (C1-C5)
        FindSocketsRecursive(commonPrefix, commonSockets);
        // Find Rare sockets (R1-R5)
        FindSocketsRecursive(rarePrefix, rareSockets);
        // Find Legendary sockets (L1-L5)
        FindSocketsRecursive(legendaryPrefix, legendarySockets);
    }

    private void FindSocketsRecursive(string prefix, Transform[] socketsArray)
    {
        for (int i = 0; i < 5; i++)
        {
            string boneName = $"{prefix}{i + 1}";
            socketsArray[i] = FindBoneRecursive(transform, boneName);
            if (socketsArray[i] == null)
            {
                Debug.LogWarning($"[PrayerWheelSetup] Could not find bone: {boneName}");
            }
        }
    }

    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    private void CreateSocketImages()
    {
        CreateImagesForSockets(commonSockets, commonImages, "Common");
        CreateImagesForSockets(rareSockets, rareImages, "Rare");
        CreateImagesForSockets(legendarySockets, legendaryImages, "Legendary");
    }

    private void CreateImagesForSockets(Transform[] sockets, Image[] images, string tierName)
    {
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] == null) continue;

            // Check if we already have an image child
            Image existingImage = sockets[i].GetComponentInChildren<Image>();
            GameObject canvasObj;

            if (existingImage != null)
            {
                images[i] = existingImage;
                canvasObj = existingImage.transform.parent.gameObject;
            }
            else
            {
                canvasObj = CreateCanvasObject(tierName, i + 1, sockets[i]);
                images[i] = canvasObj.GetComponentInChildren<Image>();
            }

            // Apply calculated alignment
            AlignCanvasToBone(canvasObj.transform, sockets[i]);
        }
    }

    private GameObject CreateCanvasObject(string tierName, int index, Transform parent)
    {
        GameObject canvasObj = new GameObject($"{tierName}Icon_{index}");
        canvasObj.transform.SetParent(parent);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        GameObject imageObj = new GameObject("Icon");
        imageObj.transform.SetParent(canvasObj.transform);
        
        RectTransform imageRect = imageObj.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageRect.localScale = Vector3.one;

        Image img = imageObj.AddComponent<Image>();
        img.sprite = defaultSocketSprite;
        img.preserveAspect = true;

        return canvasObj;
    }

    [Header("Alignment Settings")]
    [SerializeField] private AlignmentMode alignmentMode = AlignmentMode.BoneAxis;
    public enum AlignmentMode { BoneAxis, Radial, SocketRotation }

    [SerializeField, Tooltip("Enable to draw debug lines for Forward (Blue) and Up (Green)")]
    private bool previewAlignment = true;

    // ... (rest of methods)

    private void AlignCanvasToBone(Transform canvasTrans, Transform socketBone)
    {
        // 1. Position with offset
        canvasTrans.localPosition = iconPositionOffset;

        Vector3 forwardVector = socketBone.forward;
        Vector3 upVector = Vector3.up; 

        switch (alignmentMode)
        {
            case AlignmentMode.BoneAxis:
                Transform endBone = null;
                if (socketBone.childCount > 0)
                {
                    foreach(Transform child in socketBone) {
                        if(child.name.EndsWith("_end") || child.name.EndsWith("_End")) {
                            endBone = child;
                            break;
                        }
                    }
                    if (endBone == null && socketBone.childCount > 0) endBone = socketBone.GetChild(0); 
                }

                if (endBone != null)
                {
                    forwardVector = (endBone.position - socketBone.position).normalized;
                }
                else
                {
                    forwardVector = (socketBone.position - transform.position).normalized;
                    forwardVector.y = 0; 
                    forwardVector.Normalize();
                }
                upVector = Vector3.up; 
                break;

            case AlignmentMode.Radial:
                forwardVector = (socketBone.position - transform.position).normalized;
                forwardVector.y = 0; 
                forwardVector.Normalize();
                upVector = Vector3.up;
                break;

            case AlignmentMode.SocketRotation:
                forwardVector = socketBone.forward;
                upVector = socketBone.up;
                break;
        }

        // 3. Apply Rotation 
        if (forwardVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(forwardVector, upVector);
            canvasTrans.rotation = targetRotation * Quaternion.Euler(iconRotation);
        }

        // 2. Enforce Position LAST (Critical for fixing drift)
        // This ensures that no matter what the rotation did to the transform matrix,
        // the local position is strictly forced to the offset (usually 0,0,0).
        canvasTrans.localPosition = iconPositionOffset;

        // 3. Reset Child Rotation & Position (The actual Image RectTransform)
        // This protects against users accidentally moving the child image
        if (canvasTrans.childCount > 0)
        {
            Transform child = canvasTrans.GetChild(0);
            child.localRotation = Quaternion.identity;
            child.localPosition = Vector3.zero;
        }

        // 5. Scale
        canvasTrans.localScale = Vector3.one; 
        RectTransform rt = canvasTrans.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = iconSize;
            rt.localScale = canvasScale;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!previewAlignment) return;
        
        // Draw Target Lines (Blue/Green)
        DrawDebugForSockets(commonSockets, true);
        DrawDebugForSockets(rareSockets, true);
        DrawDebugForSockets(legendarySockets, true);

        // Draw Actual Orientation (Red)
        DrawDebugForImages(commonImages);
        DrawDebugForImages(rareImages);
        DrawDebugForImages(legendaryImages);
    }

    private void DrawDebugForSockets(Transform[] sockets, bool isTarget)
    {
        if (sockets == null) return;
        foreach (var s in sockets)
        {
            if (s == null) continue;
            
            Vector3 fwd = s.forward;
            Vector3 pos = s.position;

            if (isTarget)
            {
               if (alignmentMode == AlignmentMode.BoneAxis && s.childCount > 0)
               {
                   Transform end = s.GetChild(0); 
                   if (s.name.Contains("C1")) // Debug just one to avoid clutter? No, do all.
                   foreach(Transform c in s) if(c.name.Contains("_end")) end = c;

                   if (end) fwd = (end.position - s.position).normalized;
               }
               else if (alignmentMode == AlignmentMode.Radial)
               {
                   fwd = (s.position - transform.position).normalized;
                   fwd.y = 0; fwd.Normalize();
               }

               Gizmos.color = Color.blue; // Target Forward
               Gizmos.DrawLine(pos, pos + fwd * 0.6f);
               Gizmos.color = Color.green; // Target Up
               Gizmos.DrawLine(pos, pos + Vector3.up * 0.3f);
            }
        }
    }

    private void DrawDebugForImages(Image[] images)
    {
        if (images == null) return;
        foreach (var img in images)
        {
            if (img != null && img.transform.parent != null)
            {
                Transform t = img.transform.parent;
                Gizmos.color = Color.red; // Actual Forward
                Gizmos.DrawLine(t.position, t.position + t.forward * 0.4f);
            }
        }
    }

    private void UpdateTransformsForArray(Image[] images, Transform[] sockets)
    {
        for(int i=0; i<images.Length; i++)
        {
            if (images[i] != null && images[i].transform.parent != null && sockets[i] != null)
            {
                AlignCanvasToBone(images[i].transform.parent, sockets[i]);
            }
        }
    }

    private int CountNonNull(Transform[] array)
    {
        int count = 0;
        foreach (var t in array)
        {
            if (t != null) count++;
        }
        return count;
    }

    #region Editor Helpers
    [ContextMenu("Clear Socket References")]
    public void ClearReferences()
    {
        commonSockets = new Transform[5];
        rareSockets = new Transform[5];
        legendarySockets = new Transform[5];
        commonImages = new Image[5];
        rareImages = new Image[5];
        legendaryImages = new Image[5];
        Debug.Log("[PrayerWheelSetup] References cleared.");
    }

    [ContextMenu("Log Found Bones")]
    public void LogFoundBones()
    {
        Debug.Log("=== Common Sockets ===");
        for (int i = 0; i < 5; i++)
            Debug.Log($"  C{i + 1}: {(commonSockets[i] != null ? commonSockets[i].name : "NOT FOUND")}");
        
        Debug.Log("=== Rare Sockets ===");
        for (int i = 0; i < 5; i++)
            Debug.Log($"  R{i + 1}: {(rareSockets[i] != null ? rareSockets[i].name : "NOT FOUND")}");
        
        Debug.Log("=== Legendary Sockets ===");
        for (int i = 0; i < 5; i++)
            Debug.Log($"  L{i + 1}: {(legendarySockets[i] != null ? legendarySockets[i].name : "NOT FOUND")}");
    }
    #endregion
}
