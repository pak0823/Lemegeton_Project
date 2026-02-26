using UnityEngine;
using UnityEngine.Rendering;

public class ExplorationFogManager : MonoBehaviour
{
    public static ExplorationFogManager Instance;

    [Header("Fog Settings")]
    public Material fogDisplayMat; // Assign "FogMat" (Custom/ExplorationFogDisplay)
    public Shader fogLogicShader; // Assign "Custom/FogFade"
    public float clearRadius = 3.0f;
    [Range(0.001f, 0.05f)]
    public float darkenSpeed = 0.5f;

    private Transform _player;
    private Transform _fogPlane;
    private Camera _fogCamera;

    private RenderTexture _currentRT;
    private RenderTexture _accumRT;
    private Material _fadeMat;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Shader fadeShader = fogLogicShader != null ? fogLogicShader : Shader.Find("Custom/FogFade");
        if (fadeShader != null)
        {
            _fadeMat = new Material(fadeShader);
        }
        else
        {
            Debug.LogError("[ExplorationFogManager] Could not find shader 'Custom/FogFade'!");
        }
    }

    public void Initialize(Transform player, Bounds mapBounds)
    {
        _player = player;

        // Setup Fog Plane
        SetupFogPlane(mapBounds);

        // Setup Orthographic Camera
        SetupFogCamera(mapBounds);
    }

    void SetupFogPlane(Bounds bounds)
    {
        if (_fogPlane == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "FogPlane";
            go.transform.SetParent(transform);
            Destroy(go.GetComponent<Collider>());

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.material = fogDisplayMat;
            _fogPlane = go.transform;

            // FogPlane은 카메라에 찍히면 안 되므로 일반 레이어를 피해줌 (선택사항, 기본값 Default)
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        _fogPlane.position = new Vector3(bounds.center.x, bounds.center.y, -5f);
        _fogPlane.rotation = Quaternion.Euler(-90, 0, 0);

        float targetWorldWidth = bounds.size.x;
        float targetWorldHeight = bounds.size.y;
        Vector3 parentScale = _fogPlane.parent ? _fogPlane.parent.lossyScale : Vector3.one;

        float scaleX = targetWorldWidth / (10f * parentScale.x);
        float scaleZ = targetWorldHeight / (10f * parentScale.y);

        _fogPlane.localScale = new Vector3(scaleX, 1, scaleZ);
    }

    void SetupFogCamera(Bounds bounds)
    {
        if (_fogCamera == null)
        {
            GameObject camObj = new GameObject("InternalFogCamera");
            camObj.transform.SetParent(transform);
            _fogCamera = camObj.AddComponent<Camera>();
            _fogCamera.orthographic = true;
            _fogCamera.clearFlags = CameraClearFlags.SolidColor;
            _fogCamera.backgroundColor = Color.black;
            _fogCamera.depth = -100; // 메인 카메라보다 렌더링 순서 낮춤

            // 플레이어 주변을 밝히는 빛(Layer)만 렌더링하도록 설정할 수 있지만
            // 일단은 전체 렌더링 혹은 특수 레이어 렌더링
            // _fogCamera.cullingMask = 1 << LayerMask.NameToLayer("FogLight");
            // 현재 프로젝트 상 플레이어를 직접 찍어서 렌더러 기반으로 따냈는지 확인
        }

        // Camera Position & Size
        _fogCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
        // 오르토그래픽 사이즈는 세로 길이의 절반
        _fogCamera.orthographicSize = bounds.size.y / 2f;

        int width = (int)bounds.size.x * 10;
        int height = (int)bounds.size.y * 10;
        if (width <= 0) width = 1024;
        if (height <= 0) height = 1024;

        if (_currentRT != null) _currentRT.Release();
        if (_accumRT != null) _accumRT.Release();

        _currentRT = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
        _currentRT.Create();
        _fogCamera.targetTexture = _currentRT;

        _accumRT = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
        _accumRT.Create();

        RenderTexture.active = _accumRT;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = null;

        if (fogDisplayMat != null)
        {
            fogDisplayMat.SetTexture("_MainTex", _accumRT);
        }
    }

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _fogCamera && _currentRT != null && _accumRT != null && _fadeMat != null)
        {
            // Update FogCamera Position to follow player?
            // 아니면 Map 전체를 고정으로 찍음? 현재는 전체 맵 고정.

            _fadeMat.SetFloat("_FadeAmount", darkenSpeed * Time.deltaTime);
            _fadeMat.SetTexture("_CurrentTex", _currentRT);

            RenderTexture temp = RenderTexture.GetTemporary(_accumRT.width, _accumRT.height, 0, _accumRT.format);
            Graphics.Blit(_accumRT, temp, _fadeMat);
            Graphics.Blit(temp, _accumRT);
            RenderTexture.ReleaseTemporary(temp);
        }
    }

    private void OnDestroy()
    {
        if (_currentRT != null) _currentRT.Release();
        if (_accumRT != null) _accumRT.Release();
    }
}
