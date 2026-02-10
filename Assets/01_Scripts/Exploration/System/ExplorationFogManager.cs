using UnityEngine;

public class ExplorationFogManager : MonoBehaviour
{
    public static ExplorationFogManager Instance;

    [Header("Fog Settings")]
    public Material fogDisplayMat; // Assign "FogMat" here
    public float clearRadius = 3.0f;
    [Range(0.001f, 0.05f)]
    public float darkenSpeed = 0.005f;
    
    [Header("Orientation")]
    public bool flipX = false;
    public bool flipY = false;

    private Transform _player;
    private RenderTexture _fogRT;
    private Material _logicMat; // Custom/FogFade
    private Transform _fogPlane;
    private Vector2 _mapSize;
    private Vector2 _mapOrigin;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        // Load Logic Shader
        Shader logicShader = Shader.Find("Custom/FogFade");
        if (logicShader != null)
        {
            _logicMat = new Material(logicShader);
        }
        else
        {
            Debug.LogError("[ExplorationFogManager] Could not find shader 'Custom/FogFade'!");
        }
    }

    public void Initialize(Transform player, Bounds mapBounds)
    {
        _player = player;
        
        // Calculate Map Size & Origin
        _mapSize = new Vector2(mapBounds.size.x, mapBounds.size.y);
        _mapOrigin = new Vector2(mapBounds.center.x, mapBounds.center.y);

        // Setup Fog Plane
        SetupFogPlane(mapBounds);

        // Setup RenderTexture
        SetupRenderTexture((int)_mapSize.x * 10, (int)_mapSize.y * 10); // 10 pixels per unit resolution
    }

    void SetupFogPlane(Bounds bounds)
    {
        if (_fogPlane == null)
        {
            // Create Plane if not exists
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "FogPlane";
            go.transform.SetParent(transform);
            
            // Remove Collider (not needed for visual fog)
            Destroy(go.GetComponent<Collider>());
            
            // Assign Material
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.material = fogDisplayMat;
            
            _fogPlane = go.transform;
        }

        // Position
        _fogPlane.position = new Vector3(bounds.center.x, bounds.center.y, -5f); 
        
        // Rotation
        _fogPlane.rotation = Quaternion.Euler(-90, 0, 0); 
        
        // Scale Calculation (World Size -> Local Scale)
        // Unity Plane default size is 10x10.
        float targetWorldWidth = bounds.size.x;
        float targetWorldHeight = bounds.size.y;
        
        Vector3 parentScale = _fogPlane.parent ? _fogPlane.parent.lossyScale : Vector3.one;
        
        // We need: LocalScale * ParentScale * 10 = TargetSize
        // So: LocalScale = TargetSize / (ParentScale * 10)
        
        float scaleX = targetWorldWidth / (10f * parentScale.x);
        float scaleY = targetWorldHeight / (10f * parentScale.z); 
        
        // Note: Plane uses X, Z for width/height in its local space.
        // When rotated -90 on X, Local Z points UP (World Y).
        
        // scaleX is already calculated above.
        float scaleZ = targetWorldHeight / (10f * parentScale.y); 
        
        _fogPlane.localScale = new Vector3(scaleX, 1, scaleZ);

        Debug.Log($"[ExplorationFogManager] FogPlane Created. Bounds: {bounds}, Scale: {_fogPlane.localScale}, ParentScale: {parentScale}");

        // Setup RenderTexture
        SetupRenderTexture((int)_mapSize.x * 10, (int)_mapSize.y * 10); // 10 pixels per unit resolution
    }

    void SetupRenderTexture(int width, int height)
    {
        // Safety Check for Texture Size
        if (width <= 0 || height <= 0)
        {
            Debug.LogWarning($"[ExplorationFogManager] Invalid Texture Size: {width}x{height}. Defaulting to 1024x1024.");
            width = 1024;
            height = 1024;
        }
        // Cleanup old
        if (_fogRT != null)
        {
            _fogRT.Release();
        }

        // Create new
        _fogRT = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
        _fogRT.Create();
        
        // Clear to Black (Fog)
        RenderTexture.active = _fogRT;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = null;

        // Assign to Display Material
        if (fogDisplayMat != null)
        {
            fogDisplayMat.SetTexture("_MainTex", _fogRT);
        }
    }
    
    void Update()
    {
        // Auto-assign player if missing
        if (_player == null)
        {
            if (PlayerMovement.Instance != null)
            {
                _player = PlayerMovement.Instance.transform;
            }
        }

        if (_fogRT == null || _player == null || _logicMat == null) return;

        // 1. Pass Params
        _logicMat.SetVector("_PlayerPos", new Vector4(_player.position.x, _player.position.y, 0, 0));
        _logicMat.SetVector("_MapSize", new Vector4(_mapSize.x, _mapSize.y, 0, 0));
        _logicMat.SetVector("_MapOrigin", new Vector4(_mapOrigin.x, _mapOrigin.y, 0, 0));
        _logicMat.SetFloat("_ClearRadius", clearRadius);
        _logicMat.SetFloat("_DarkenSpeed", darkenSpeed);
        
        // Orientation
        Vector4 uvMult = new Vector4(flipX ? -1f : 1f, flipY ? -1f : 1f, 0, 0);
        _logicMat.SetVector("_UVMult", uvMult);

        // 2. Double Buffer
        RenderTexture temp = RenderTexture.GetTemporary(_fogRT.width, _fogRT.height, 0, _fogRT.format);
        Graphics.Blit(_fogRT, temp, _logicMat);
        Graphics.Blit(temp, _fogRT);
        RenderTexture.ReleaseTemporary(temp);
    }
    
    private void OnDestroy()
    {
        if (_fogRT != null) _fogRT.Release();
    }
}
