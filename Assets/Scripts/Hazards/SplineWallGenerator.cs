using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

/// <summary>
/// Generates a curved wall mesh and collider along a Unity Spline.
/// Attach to a GameObject with a SplineContainer component.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(SplineContainer))]
public class SplineWallGenerator : MonoBehaviour
{
    [Header("Wall Dimensions")]
    [SerializeField, Tooltip("Height of the wall")]
    private float wallHeight = 3f;
    
    [SerializeField, Tooltip("Thickness of the wall")]
    private float wallThickness = 1f;
    
    [Header("Resolution")]
    [SerializeField, Tooltip("Number of segments along the spline (higher = smoother curves)")]
    [Range(4, 200)]
    private int segments = 5;
    
    [Header("Collider Settings")]
    [SerializeField, Tooltip("Tag to assign to the generated collider object")]
    private string colliderTag = "Wall";
    
    [SerializeField, Tooltip("Layer for the collider object")]
    private int colliderLayer = 0;
    
    [SerializeField, Tooltip("Is the collider a trigger?")]
    private bool isTrigger = false;
    
    [Header("Mesh Settings")]
    [SerializeField, Tooltip("Generate a visual mesh (disable for invisible walls)")]
    private bool generateMesh = false;
    
    [SerializeField, Tooltip("Material for the wall mesh")]
    private Material wallMaterial;
    
    [Header("Generation")]
    [SerializeField, Tooltip("Automatically generate wall on Start (for prefabs/chunks)")]
    private bool generateOnStart = true;
    
    [SerializeField, Tooltip("Auto-regenerate when spline changes (Editor only)")]
    private bool autoRegenerate = true;
    
    private SplineContainer splineContainer;
    private bool hasGeneratedAtRuntime = false;
    private GameObject wallMeshObject;
    private GameObject wallColliderObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    
    private void OnEnable()
    {
        splineContainer = GetComponent<SplineContainer>();
        
        if (splineContainer != null && splineContainer.Spline != null)
        {
            Spline.Changed += OnSplineChanged;
        }
    }
    
    private void Start()
    {
        // Auto-generate at runtime when prefab spawns
        if (Application.isPlaying && generateOnStart && !hasGeneratedAtRuntime)
        {
            hasGeneratedAtRuntime = true;
            GenerateWall();
        }
    }
    
    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }
    
    private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        if (autoRegenerate && splineContainer != null && splineContainer.Spline == spline)
        {
            GenerateWall();
        }
    }
    
    /// <summary>
    /// Generates the wall mesh and collider based on current settings.
    /// </summary>
    [ContextMenu("Generate Wall")]
    public void GenerateWall()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();
        
        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
        {
            Debug.LogWarning("[SplineWallGenerator] No valid spline found. Add at least 2 knots.");
            return;
        }
        
        ClearWall();
        
        // Create or get the wall mesh object
        wallMeshObject = new GameObject("WallMesh");
        wallMeshObject.transform.SetParent(transform);
        wallMeshObject.transform.localPosition = Vector3.zero;
        wallMeshObject.transform.localRotation = Quaternion.identity;
        wallMeshObject.transform.localScale = Vector3.one;
        
        // Create or get the wall collider object
        wallColliderObject = new GameObject("WallCollider");
        wallColliderObject.transform.SetParent(transform);
        wallColliderObject.transform.localPosition = Vector3.zero;
        wallColliderObject.transform.localRotation = Quaternion.identity;
        wallColliderObject.transform.localScale = Vector3.one;
        
        // Apply tag and layer to collider object
        if (!string.IsNullOrEmpty(colliderTag))
        {
            try
            {
                wallColliderObject.tag = colliderTag;
            }
            catch (UnityException e)
            {
                Debug.LogError($"[SplineWallGenerator] Tag '{colliderTag}' does not exist. Create it in Tags and Layers. Error: {e.Message}");
            }
        }
        wallColliderObject.layer = colliderLayer;
        
        // Generate the mesh
        Mesh mesh = GenerateWallMesh();
        
        if (generateMesh)
        {
            meshFilter = wallMeshObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            
            meshRenderer = wallMeshObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = wallMaterial != null ? wallMaterial : GetDefaultMaterial();
        }
        
        // Add collider
        meshCollider = wallColliderObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.isTrigger = isTrigger;
        meshCollider.convex = isTrigger; // Triggers require convex colliders
        
        Debug.Log($"[SplineWallGenerator] Wall generated with {segments} segments. Collider tag: '{colliderTag}'");
    }
    
    /// <summary>
    /// Clears the generated wall objects.
    /// </summary>
    [ContextMenu("Clear Wall")]
    public void ClearWall()
    {
        // Find and destroy existing child objects
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "WallMesh" || child.name == "WallCollider")
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
        
        wallMeshObject = null;
        wallColliderObject = null;
        meshFilter = null;
        meshRenderer = null;
        meshCollider = null;
    }
    
    private Mesh GenerateWallMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "SplineWallMesh";
        
        Spline spline = splineContainer.Spline;
        int vertexCount = (segments + 1) * 4; // 4 vertices per segment (front bottom, front top, back bottom, back top)
        
        List<Vector3> vertices = new List<Vector3>(vertexCount);
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>(vertexCount);
        List<Vector3> normals = new List<Vector3>(vertexCount);
        
        float halfThickness = wallThickness / 2f;
        float splineLength = spline.GetLength();
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            
            // Get position and tangent at this point on the spline
            Vector3 position = spline.EvaluatePosition(t);
            Vector3 tangent = spline.EvaluateTangent(t);
            Vector3 up = spline.EvaluateUpVector(t);
            
            // Transform to world space, then back to local (in case spline is offset)
            position = transform.TransformPoint(position);
            tangent = transform.TransformDirection(tangent);
            up = transform.TransformDirection(up);
            
            // Transform back to local space for the mesh
            position = transform.InverseTransformPoint(position);
            tangent = transform.InverseTransformDirection(tangent);
            up = transform.InverseTransformDirection(up);
            
            // Calculate right vector (perpendicular to tangent and up)
            Vector3 right = Vector3.Cross(tangent.normalized, up.normalized).normalized;
            if (right == Vector3.zero)
            {
                right = Vector3.Cross(tangent.normalized, Vector3.up).normalized;
                if (right == Vector3.zero)
                    right = Vector3.right;
            }
            
            // Calculate UV coordinates based on distance along spline
            float u = t * splineLength / wallHeight; // Tile based on wall height
            
            // Front side vertices (facing outward from wall)
            Vector3 frontBottom = position - right * halfThickness;
            Vector3 frontTop = frontBottom + up.normalized * wallHeight;
            
            // Back side vertices
            Vector3 backBottom = position + right * halfThickness;
            Vector3 backTop = backBottom + up.normalized * wallHeight;
            
            // Add vertices in order: front bottom, front top, back bottom, back top
            vertices.Add(frontBottom);  // 0 + i*4
            vertices.Add(frontTop);     // 1 + i*4
            vertices.Add(backBottom);   // 2 + i*4
            vertices.Add(backTop);      // 3 + i*4
            
            // UVs
            uvs.Add(new Vector2(u, 0));
            uvs.Add(new Vector2(u, 1));
            uvs.Add(new Vector2(u, 0));
            uvs.Add(new Vector2(u, 1));
            
            // Normals
            normals.Add(-right);
            normals.Add(-right);
            normals.Add(right);
            normals.Add(right);
        }
        
        // Generate triangles
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 4;
            int nextBaseIndex = (i + 1) * 4;
            
            // Front face (facing -right direction)
            triangles.Add(baseIndex);           // front bottom current
            triangles.Add(baseIndex + 1);       // front top current
            triangles.Add(nextBaseIndex);       // front bottom next
            
            triangles.Add(nextBaseIndex);       // front bottom next
            triangles.Add(baseIndex + 1);       // front top current
            triangles.Add(nextBaseIndex + 1);   // front top next
            
            // Back face (facing +right direction)
            triangles.Add(baseIndex + 2);       // back bottom current
            triangles.Add(nextBaseIndex + 2);   // back bottom next
            triangles.Add(baseIndex + 3);       // back top current
            
            triangles.Add(nextBaseIndex + 2);   // back bottom next
            triangles.Add(nextBaseIndex + 3);   // back top next
            triangles.Add(baseIndex + 3);       // back top current
            
            // Top face
            triangles.Add(baseIndex + 1);       // front top current
            triangles.Add(baseIndex + 3);       // back top current
            triangles.Add(nextBaseIndex + 1);   // front top next
            
            triangles.Add(nextBaseIndex + 1);   // front top next
            triangles.Add(baseIndex + 3);       // back top current
            triangles.Add(nextBaseIndex + 3);   // back top next
            
            // Bottom face
            triangles.Add(baseIndex);           // front bottom current
            triangles.Add(nextBaseIndex);       // front bottom next
            triangles.Add(baseIndex + 2);       // back bottom current
            
            triangles.Add(nextBaseIndex);       // front bottom next
            triangles.Add(nextBaseIndex + 2);   // back bottom next
            triangles.Add(baseIndex + 2);       // back bottom current
        }
        
        // Cap the ends
        // Start cap
        triangles.Add(0);
        triangles.Add(2);
        triangles.Add(1);
        triangles.Add(2);
        triangles.Add(3);
        triangles.Add(1);
        
        // End cap
        int lastBase = segments * 4;
        triangles.Add(lastBase);
        triangles.Add(lastBase + 1);
        triangles.Add(lastBase + 2);
        triangles.Add(lastBase + 2);
        triangles.Add(lastBase + 1);
        triangles.Add(lastBase + 3);
        
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    private Material GetDefaultMaterial()
    {
        // Try to find the URP Lit shader first, fall back to Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            return mat;
        }
        
        return null;
    }
    
    private void OnDrawGizmos()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();
        
        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            return;
        
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        
        // Draw wall outline
        Spline spline = splineContainer.Spline;
        float halfThickness = wallThickness / 2f;
        
        for (int i = 0; i < segments; i++)
        {
            float t1 = (float)i / segments;
            float t2 = (float)(i + 1) / segments;
            
            Vector3 pos1 = transform.TransformPoint(spline.EvaluatePosition(t1));
            Vector3 pos2 = transform.TransformPoint(spline.EvaluatePosition(t2));
            
            Gizmos.DrawLine(pos1, pos2);
            Gizmos.DrawLine(pos1 + Vector3.up * wallHeight, pos2 + Vector3.up * wallHeight);
        }
    }
}
