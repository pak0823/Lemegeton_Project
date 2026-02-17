using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("페이드용 CanvasGroup")]
    public CanvasGroup fader;
    [Header("페이드 지속시간")]
    private float fadeDuration = 0.5f; // 페이드 시간 단축 (로딩 UI가 있으므로)

    [Header("로딩 UI (Optional)")]
    public Slider loadingProgressBar;
    public Text loadingText; // TMP_Text 권장하나 기존 호환성 위해 Text 사용. 필요시 변경.
    public GameObject loadingPanel; // 로딩 바/텍스트가 포함된 패널

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

    [Header("전투 보상 데이터")]
    public List<RewardData> pendingRewards = new List<RewardData>();

    public void SetPendingRewards(List<RewardData> rewards)
    {
        pendingRewards = rewards;
    }

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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // sceneName 씬으로 페이드아웃 → 로드 → 페이드인
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }
    
    // [Refactor] 통합 로딩 코루틴
    IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // 1. 입력 차단 및 페이드 아웃
        if (fader != null) fader.blocksRaycasts = true; // 터치 차단
        
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            if(fader) fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        // 2. 로딩 UI 표시
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingProgressBar != null) loadingProgressBar.value = 0f;
        if (loadingText != null) loadingText.text = "Loading...";

        // 3. 메모리 정리 (GC) - 씬 넘어가기 전 안전한 타이밍
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        // 4. 비동기 로딩 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 바로 넘어가지 않게 대기

        float timer = 0.0f;
        
        // 최소 로딩 시간 보장 (깜빡임 방지)
        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            // Unity의 progress는 0.9에서 멈춤
            float currentProgress = op.progress;
            
            // UI 업데이트 (Fake 100% 연출 포함)
            if (currentProgress >= 0.9f)
            {
                currentProgress = 1f; // 0.9 -> 1.0 보간
                
                // 최소 1초 이상 지났을 때만 전환 허용
                if (timer > 1.0f)
                {
                    if (loadingProgressBar != null) loadingProgressBar.value = 1f;
                    op.allowSceneActivation = true;
                }
            }
            else
            {
                // 로딩 중에는 실제 진행률 반영
                if (loadingProgressBar != null) loadingProgressBar.value = currentProgress;
            }
            
            if (loadingText != null) 
                loadingText.text = $"Loading... {(int)(currentProgress * 100)}%";
        }

        // 5. 로딩 완료 및 UI 숨김
        if (loadingPanel != null) loadingPanel.SetActive(false);

        // 6. 페이드 인
        t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            if(fader) fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        
        // 7. 입력 차단 해제
        if (fader != null) fader.blocksRaycasts = false;
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
        // 1. 페이드 아웃 & 입력 차단
        if (fader != null) fader.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            if(fader) fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        
        // 2. 로딩 UI 표시
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingProgressBar != null) loadingProgressBar.value = 0f;
        if (loadingText != null) loadingText.text = "Returning...";

        // 3. 메모리 정리
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        // 4. 탐험 씬 로드 (비동기)
        AsyncOperation op = SceneManager.LoadSceneAsync(pendingReturnScene);
        op.allowSceneActivation = false;
        
        float timer = 0f;
        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;
            
            float p = op.progress;
            if (p >= 0.9f)
            {
                p = 1f;
                // 복귀도 최소 시간 보장
                if (timer > 1.0f) op.allowSceneActivation = true;
            }
            if (loadingProgressBar != null) loadingProgressBar.value = p;
            if (loadingText != null) loadingText.text = $"Returning... {(int)(p * 100)}%";
        }

        // 5. 로딩 UI 숨김
        if (loadingPanel != null) loadingPanel.SetActive(false);

        // 6. 탐험 씬 플레이어 준비 대기
        // (이 부분은 로딩이 끝난 직후라 바로 실행됨)
        
        // 탐험 씬에서 PlayerMovement 준비될 때까지 대기 후 순간이동
        int safety = 300; // ~5초(60FPS 가정)
        while (PlayerMovement.Instance == null && safety-- > 0)
            yield return null;

        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.TeleportTo(pendingReturnPosition);

            if (HasSavedVigor && VigorManager.Instance != null)
            {
                int v = ConsumeVigor();
                VigorManager.Instance.SetCurrentVigor(v);
            }

            // 남은 경로가 있으면 이어서 이동
            var resume = ConsumeResumePath();
            
            bool hasRewards = pendingRewards != null && pendingRewards.Count > 0;
            bool hasResumePath = resume != null && resume.Count >= 2;

            if (hasResumePath || hasRewards)
            {
                // 보상창 닫힐 때까지 입력/이동 차단
                PlayerMovement.Instance?.LockMovementIndefinite();

                // 프레젠터 준비 대기(탐험 씬 UI가 아직 Awake 전일 수 있으므로)
                yield return new WaitUntil(() => ExplorationModalPresenter.Instance != null);

                // 보상 데이터 전달 (없으면 빈 리스트 전달)
                var rewardsToFullfill = pendingRewards ?? new List<RewardData>();

                ExplorationModalPresenter.Instance.ShowRewardPopup(rewardsToFullfill, () =>
                {
                    pendingRewards = null; // 보상 수령 완료 후 초기화
                    PlayerMovement.Instance?.UnlockMovementIndefinite();
                    if (hasResumePath)
                        PlayerMovement.Instance?.ResumePathAfterBattle(resume);
                });
            }
            else
            {
                // resume가 없으면 잠금 풀기(혹시 남아있다면)
                PlayerMovement.Instance?.UnlockMovementIndefinite();
            }
            Debug.Log($"[Return] Teleport to {pendingReturnPosition}");
        }
        else
        {
            Debug.LogWarning("[Return] PlayerMovement 미발견 → 텔레포트 생략");
        }

        // 7. 페이드 인
        t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            if(fader) fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        
        // 8. 입력 차단 해제 & 컨텍스트 정리
        if (fader != null) fader.blocksRaycasts = false;
        
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
        PlayerMovement.Instance?.LockMovementIndefinite();

        string msg = $"{monsterName}과 마주쳤습니다. 전투에 돌입합니다.";
        ExplorationModalPresenter.Instance.ShowEncounterBanner(msg, encounterBannerSeconds, () =>
        {
            FadeToScene(battleScene);
        });
    }

}
