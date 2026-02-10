using UnityEngine;

public class FogManager : MonoBehaviour
{
    // public Material fadeMaterial; // Removed: We create this internally now safely
    private Material fadeMaterial; 
    public float fadeSpeed = 0.05f; // 안개가 다시 차오르는 속도
    
    private RenderTexture fogRT;
    private RenderTexture tempRT;

    void Start()
    {
        // 1. FogCamera 및 RT 가져오기
        Camera fogCam = GetComponent<Camera>();
        if (fogCam == null)
        {
            Debug.LogError("[FogManager] No Camera component found!");
            return;
        }
        
        fogRT = fogCam.targetTexture;
        if (fogRT == null)
        {
             Debug.LogError("[FogManager] Fog Camera missing Target Texture!");
             return;
        }

        // 2. FogScreen의 머티리얼에 RT 연결 (중요: 이것이 없으면 안개가 안 보일 수 있음)
        GameObject fogScreen = GameObject.Find("FogScreen");
        if (fogScreen != null)
        {
            Renderer screenRenderer = fogScreen.GetComponent<Renderer>();
            if (screenRenderer != null)
            {
                // 인스턴스 머티리얼 생성 및 할당 (다른 씬에 영향 안 주도록)
                screenRenderer.material.SetTexture("_FogTex", fogRT);
                // 혹시 모르니 FogColor 등의 파라미터도 확인 가능하지만 쉐이더 기본값 사용
                Debug.Log("[FogManager] Connected foggy RT to FogScreen material.");
            }
        }
        else
        {
            Debug.LogWarning("[FogManager] Could not find 'FogScreen' object. Fog overlay might not be visible.");
        }

        // 3. 셰이더 로드 및 머티리얼 생성 (Fade 효과용)
        Shader fadeShader = Shader.Find("Custom/FogFade");
        if (fadeShader == null)
        {
            Debug.LogError("[FogManager] Shader 'Custom/FogFade' not found!");
            return;
        }
        fadeMaterial = new Material(fadeShader);

        // 4. RT 초기화 (검은색 = 아직 안개 덮임, 흰색 = 걷힘. 초기 상태는 검은색이어야 함?)
        // FogOverlayFixed 쉐이더: Red 채널이 1이면 투명(걷힘), 0이면 불투명(안개).
        // 따라서 초기 상태는 0(검은색)이어야 함.
        tempRT = new RenderTexture(fogRT.width, fogRT.height, 0, fogRT.format);
        
        // 초기화: 검은색으로 밀기
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = fogRT;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = active;
    }

    void LateUpdate()
    {
        if (fogRT == null || fadeMaterial == null) return;

        // 1. 페이드 양 조절 (지나온 길이 얼마나 빨리 어두워질지 -> 다시 안개가 덮임)
        // FogFade 쉐이더: _FadeAmount만큼 빼거나 더해서 어둡게(0으로) 만듦.
        fadeMaterial.SetFloat("_FadeAmount", fadeSpeed * Time.deltaTime);

        // 2. 핑퐁 텍스처를 사용하여 이전 기록에 페이드 효과를 입혀 유지함
        RenderTexture temp = RenderTexture.GetTemporary(fogRT.width, fogRT.height, 0, fogRT.format);
        
        // 현재 fogRT(이전 프레임 기록 포함)를 가져와서 조금 어둡게(fadeMaterial) 만든 뒤 temp에 저장
        Graphics.Blit(fogRT, temp, fadeMaterial);
        
        // 어두워진 데이터를 다시 fogRT로 복사하여 이번 프레임의 결과물로 확정
        Graphics.Blit(temp, fogRT);
        
        RenderTexture.ReleaseTemporary(temp);
    }
}