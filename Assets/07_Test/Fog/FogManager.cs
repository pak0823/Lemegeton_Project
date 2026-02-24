using UnityEngine;
using UnityEngine.Rendering;

public class FogManager : MonoBehaviour
{
    private Material fadeMaterial;
    public float fadeSpeed = 0.05f;

    private RenderTexture currentRT;
    private RenderTexture accumRT;

    void Start()
    {
        // 1. Get FogCamera and RT
        Camera fogCam = GetComponent<Camera>();
        if (fogCam == null)
        {
            Debug.LogError("[FogManager] No Camera component found!");
            return;
        }

        currentRT = fogCam.targetTexture;
        if (currentRT == null)
        {
             Debug.LogError("[FogManager] Fog Camera missing Target Texture!");
             return;
        }

        // 2. Load shader and create material
        Shader fadeShader = Shader.Find("Custom/FogFade");
        if (fadeShader == null)
        {
            Debug.LogError("[FogManager] Shader 'Custom/FogFade' not found!");
            return;
        }
        fadeMaterial = new Material(fadeShader);

        // 3. Create accumulation RT
        accumRT = new RenderTexture(currentRT.width, currentRT.height, 0, currentRT.format);
        accumRT.Create();

        // Initialize with black
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = accumRT;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = active;

        // 4. Connect accumulated RT to FogScreen
        GameObject fogScreen = GameObject.Find("FogScreen");
        if (fogScreen != null)
        {
            Renderer screenRenderer = fogScreen.GetComponent<Renderer>();
            if (screenRenderer != null)
            {
                screenRenderer.material.SetTexture("_FogTex", accumRT);
                Debug.Log("[FogManager] Connected accumulated RT to FogScreen material.");
            }
        }
        else
        {
            Debug.LogWarning("[FogManager] Could not find 'FogScreen' object. Fog overlay might not be visible.");
        }
    }

    private void OnEnable()
    {
        // Register URP callback
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        // Unregister callback
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // Process only if it's the FogCamera
        if (camera == GetComponent<Camera>() && currentRT != null && accumRT != null && fadeMaterial != null)
        {
            // 1. Set fade amount
            fadeMaterial.SetFloat("_FadeAmount", fadeSpeed * Time.deltaTime);

            // 2. Pass current render texture
            fadeMaterial.SetTexture("_CurrentTex", currentRT);

            // 3. Create temporary RT
            RenderTexture temp = RenderTexture.GetTemporary(accumRT.width, accumRT.height, 0, accumRT.format);

            // 4. Blit accumulation history with current frame to temp
            Graphics.Blit(accumRT, temp, fadeMaterial);

            // 5. Blit temp back to accumulation RT
            Graphics.Blit(temp, accumRT);

            RenderTexture.ReleaseTemporary(temp);
        }
    }

    private void OnDestroy()
    {
        if (accumRT != null)
        {
            accumRT.Release();
            Destroy(accumRT);
        }
    }
}

