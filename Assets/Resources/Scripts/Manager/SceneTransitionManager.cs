using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("페이드용 CanvasGroup")]
    public CanvasGroup fader;
    [Header("페이드 지속시간")]
    public float fadeDuration = 1f;

    [Header("전투 복귀 컨텍스트")]
    public string pendingReturnScene;         // 돌아갈 탐험 씬 이름
    public Vector3 pendingReturnPosition;     // 돌아갈 월드 좌표
    public bool HasPendingReturn => !string.IsNullOrEmpty(pendingReturnScene);

    [Tooltip("탐험맵 재로딩 시 이 프리팹을 사용(랜덤 재추첨 방지)")]
    public GameObject explorationMapPrefabOverride;

    [Header("탐험 스냅샷")]
    public ExplorationSnapshot explorationSnapshot;
    public bool HasExplorationSnapshot => explorationSnapshot != null;
    private bool _isReturning = false;        // 복귀 중복 실행 가드

    public void SaveExplorationSnapshot(ExplorationSnapshot snap)
    {
        explorationSnapshot = snap;
        Debug.Log($"[STM] Snapshot saved. objs={(snap?.objects?.Count ?? 0)}");
    }
    public void ClearExplorationSnapshot()
    {
        explorationSnapshot = null;
        Debug.Log("[STM] Snapshot cleared (before leaving exploration).");
    }

    private void Awake()
    {
        if (Shared.SceneTransitionManager == null)
        {
            Shared.SceneTransitionManager = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // sceneName 씬으로 페이드아웃 → 로드 → 페이드인
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeCoroutine(sceneName));
    }
    IEnumerator FadeCoroutine(string sceneName)
    {
        // 페이드 아웃 (alpha 0 → 1)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        // 씬 로드 (비동기)
        yield return SceneManager.LoadSceneAsync(sceneName);

        // 페이드 인 (alpha 1 → 0)
        t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
    }

    public void SaveReturnPoint(string sceneName, Vector3 worldPos)
    {
        pendingReturnScene = sceneName;
        pendingReturnPosition = worldPos;
        Debug.Log($"[Return] Save: scene={sceneName}, pos={worldPos}");
    }
    public void ReturnToSavedPoint()
    {
        if (!HasPendingReturn)
        {
            Debug.LogWarning("[Return] 저장된 복귀 지점이 없습니다.");
            return;
        }
        if (_isReturning) return;             // 중복 가드
        _isReturning = true;
        StartCoroutine(ReturnCoroutine());
    }

    IEnumerator ReturnCoroutine()
    {
        // 페이드 아웃
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        // 탐험 씬 로드
        yield return SceneManager.LoadSceneAsync(pendingReturnScene);

        // 탐험 씬에서 PlayerMovement 준비될 때까지 대기 후 순간이동
        int safety = 300; // ~5초(60FPS 가정)
        while (Shared.PlayerMovement == null && safety-- > 0)
            yield return null;

        if (Shared.PlayerMovement != null)
        {
            Shared.PlayerMovement.TeleportTo(pendingReturnPosition);
            Debug.Log($"[Return] Teleport to {pendingReturnPosition}");
        }
        else
        {
            Debug.LogWarning("[Return] PlayerMovement 미발견 → 텔레포트 생략");
        }

        // 페이드 인
        t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        // 1회성 컨텍스트 정리
        pendingReturnScene = null;
        _isReturning = false;   // 가드 해제
    }
}
