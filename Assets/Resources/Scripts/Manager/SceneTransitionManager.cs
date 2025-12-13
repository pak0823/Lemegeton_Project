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

    // 전투 복귀 후 이어서 이동할 경로(셀 기준)
    public List<Vector3Int> pendingResumeCells;
    public bool HasPendingResume => pendingResumeCells != null && pendingResumeCells.Count >= 2;

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

    public void SetResumePath(List<Vector3Int> cells)
    {
        pendingResumeCells = cells;
    }

    public List<Vector3Int> ConsumeResumePath()
    {
        var tmp = pendingResumeCells;
        pendingResumeCells = null;
        return tmp;
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
            // 남은 경로가 있으면 이어서 이동
            var resume = ConsumeResumePath();
            if (resume != null && resume.Count >= 2)
                Shared.PlayerMovement.ResumePathAfterBattle(resume);
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

    //IExplorationPersistable를 가지고 있는 오브젝트는 모두 스냅샷에 추가
    public ExplorationSnapshot BuildExplorationSnapshotFromScene()
    {
        var snap = new ExplorationSnapshot();

        // 씬 내 모든 Persistable 상태 수집
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb is IExplorationPersistable ip)
            {
                snap.objects.Add(ip.SaveState());
            }
        }

        // ObjectGaugeManager 스냅샷은 프로젝트 코드가 없어 확정할 수 없어서,
        // 있으면 리플렉션으로 값을 읽어 채우고, 없으면 0 유지(= 복원 호출에서 내부 기본값 유지 필요)
        var og = Shared.ObjectGaugeManager;
        if (og != null)
        {
            try
            {
                var t = og.GetType();
                snap.totalBoxes = (int)(t.GetField("totalBoxes")?.GetValue(og) ?? snap.totalBoxes);
                snap.openedBoxes = (int)(t.GetField("openedBoxes")?.GetValue(og) ?? snap.openedBoxes);
                snap.triggeredTraps = (int)(t.GetField("triggeredTraps")?.GetValue(og) ?? snap.triggeredTraps);
                snap.thresholdReached = (bool)(t.GetField("thresholdReached")?.GetValue(og) ?? snap.thresholdReached);
            }
            catch { /* gauge가 다르면 무시 */ }
        }

        return snap;
    }
}
