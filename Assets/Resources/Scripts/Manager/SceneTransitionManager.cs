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
    private float fadeDuration = 2f;

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

    [Header("활기 스냅샷")]
    public int savedVigor = -1;
    public bool HasSavedVigor => savedVigor >= 0;

    // 전투 복귀 후 이어서 이동할 경로(셀 기준)
    public List<Vector3Int> pendingResumeCells;
    public bool HasPendingResume => pendingResumeCells != null && pendingResumeCells.Count >= 2;

    [Header("전투 복귀 후 정산할 이동 활기 비용(전투 전 이동분)")]
    public int pendingPlannedMoveVigorCost = 0;
    public bool HasDeferredMoveCost => pendingPlannedMoveVigorCost > 0;

    private float encounterBannerSeconds = 1.5f;

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

    public void SetDeferredMoveCost(int cost)
    {
        pendingPlannedMoveVigorCost = Mathf.Max(0, cost);
    }

    public int ConsumeDeferredMoveCost()
    {
        int tmp = pendingPlannedMoveVigorCost;
        pendingPlannedMoveVigorCost = 0;
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

            if (HasSavedVigor && Shared.VigorManager != null)
            {
                int v = ConsumeVigor();
                Shared.VigorManager.SetCurrentVigor(v);
            }

            // 남은 경로가 있으면 이어서 이동
            var resume = ConsumeResumePath();
            if (resume != null && resume.Count >= 2)
            {
                // 보상창 닫힐 때까지 입력/이동 차단
                Shared.PlayerMovement?.LockMovementIndefinite();

                // 프레젠터 준비 대기(탐험 씬 UI가 아직 Awake 전일 수 있으므로)
                yield return new WaitUntil(() => ExplorationModalPresenter.Instance != null);

                ExplorationModalPresenter.Instance.ShowRewardPopup(() =>
                {
                    Shared.PlayerMovement?.UnlockMovementIndefinite();
                    Shared.PlayerMovement?.ResumePathAfterBattle(resume);
                });
            }
            else
            {
                // resume가 없으면 잠금 풀기(혹시 남아있다면)
                Shared.PlayerMovement?.UnlockMovementIndefinite();
            }
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

        return snap;
    }

    public void SaveVigor(int v) => savedVigor = Mathf.Max(0, v);
    public int ConsumeVigor()
    {
        int v = savedVigor;
        savedVigor = -1;
        return v;
    }

    public void EnterBattleWithEncounterBanner(string monsterName, string battleScene)
    {
        // 전투 진입 전까지 입력 차단(타일 클릭 등)
        Shared.PlayerMovement?.LockMovementIndefinite();

        string testSecne = "TestScene";//임시 테스트용 - 인카운터로 인한 전투씬으로 가기 전 훈련씬을 거치기 위해 임시 추가

        var presenter = ExplorationModalPresenter.Instance;
        if (presenter == null)
        {
            // 프레젠터가 없으면 즉시 진입(안전 fallback)
            //FadeToScene(battleScene);
            FadeToScene(testSecne);//임시 테스트용 - 인카운터로 인한 전투씬으로 가기 전 훈련씬을 거치기 위해 임시 추가
            return;
        }

        string msg = $"{monsterName}과 마주쳤습니다. 전투에 돌입합니다.";
        presenter.ShowEncounterBanner(msg, encounterBannerSeconds, () =>
        {
            FadeToScene(battleScene);
            //FadeToScene(testSecne); //임시 테스트용 - 인카운터로 인한 전투씬으로 가기 전 훈련씬을 거치기 위해 임시 추가
        });
    }

}
