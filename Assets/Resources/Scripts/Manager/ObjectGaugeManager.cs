using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEditor.PlayerSettings;

// 맵 내 전체 상자 개수 대비
// 열린 상자 + 발동된 함정 수를 게이지로 계산
public class ObjectGaugeManager : MonoBehaviour, IResettable
{
    public bool IsBattleNoticeActive { get; private set; } = false;

    [Header("Object 게이지")]
    [Tooltip("게이지 임계치 (0~1) 이상일 때 이벤트 발생")]
    public float thresholdPercent = 0.66f;
    public UnityEvent onThresholdReached;

    [Header("전투 진입 컨텍스트(스테이지 번호 전달)")]
    [SerializeField] private StageNormalMapData currentStageData; // 인스펙터에서 현재 스테이지 데이터 연결
    [SerializeField] private int stageNumberOverride = -1;        // 데이터가 없으면 임시로 넘길 번호

    private int totalBoxes;
    private int openedBoxes;
    private int triggeredTraps;
    private bool thresholdReached;

    [Header("인지 게이지")]
    public int awarenessMax = 5;
    public Text awarenessGaugeText;
    public GameObject battleNoticeUI;
    public float battleNoticeDuration = 3f;

    private int awarenessGauge = 0;
    private bool battleTriggered = false;

    void Awake()
    {
        if(Shared.ObjectGaugeManager == null) Shared.ObjectGaugeManager = this;
        else { Destroy(gameObject); return; }

        if (awarenessGaugeText != null)
        {
            awarenessGaugeText.gameObject.SetActive(true);
            UpdateAwarenessUI(); // 초기값 0 표시
        }

        if (battleNoticeUI != null)
            battleNoticeUI.SetActive(false);
    }

    #region Object 게이지
    // 맵 생성 후 상자 총 개수를 등록
    public void RegisterTotalBoxes(int count)
    {
        totalBoxes = count;
        openedBoxes = 0;
        triggeredTraps = 0;
        thresholdReached = false;
        Debug.Log($"[ObjectGaugeManager] 맵에 생성된 상자 총 갯수: {totalBoxes}");
    }

    // 상자 개봉 시 호출
    public void IncrementChest()
    {
        openedBoxes++;
        Debug.Log($"[ObjectGaugeManager] 개봉된 상자 수: {openedBoxes}");
        CheckThreshold();
    }

    // 함정 발동 시 호출
    public void RegisterTrapTriggeredByPlayer()
    {
        triggeredTraps++;
        IncrementAwarenessByTrap();  // 인지게이지만 여기서 올림
        CheckThreshold();
        Debug.Log($"[ObjectGaugeManager] 발동된 함정 수: {triggeredTraps}");
    }
    public void RegisterTrapClearedByPush() // 인지 게이지는 올리지 않음
    {
        triggeredTraps++;            // 통계는 올림        
        CheckThreshold();
    }

    private void CheckThreshold()
    {
        if (thresholdReached || totalBoxes <= 0) return;

        float percent = (openedBoxes + triggeredTraps) / (float)totalBoxes;
        Debug.Log($"[ObjectGaugeManager] 현재 오브젝트 게이지: {percent * 100f}%");

        if (percent >= thresholdPercent)
        {
            thresholdReached = true;
            Debug.Log("[ObjectGaugeManager] 게이지 조건 충족. 이벤트 실행.");
            onThresholdReached?.Invoke();
        }
    }

    // 현재 게이지 퍼센트 반환 (0~1)
    public float GetGaugePercent()
    {
        if (totalBoxes <= 0) return 0f;
        return Mathf.Clamp01((openedBoxes + triggeredTraps) / (float)totalBoxes);
    }
    #endregion

    #region 인지 게이지
    public void TryIncrementAwarenessByChest()  //box개봉 시 인지게이지 상승
    {
        if (battleTriggered) return;
        
        IncrementAwareness();
    }

    public void IncrementAwarenessByTrap()  //함점 발동 시 인지게이지 상승
    {
        if (battleTriggered) return;
        IncrementAwareness();
    }

    public void IncrementAwarenessByTimer()
    {
        if (battleTriggered) return;
        IncrementAwareness();
    }

    private void IncrementAwareness()
    {
        awarenessGauge = Mathf.Min(awarenessGauge + 1, awarenessMax);
        UpdateAwarenessUI();

        if (awarenessGauge >= awarenessMax && !battleTriggered)
        {
            battleTriggered = true;
            StartCoroutine(StartBattleRoutine());
        }
    }

    private void UpdateAwarenessUI()
    {
        if (awarenessGaugeText != null)
            awarenessGaugeText.text = $"인지 게이지: {awarenessGauge}/{awarenessMax}";
    }

    #endregion

    private bool isTransitioningToBattle = false; //재진입 가드

    private IEnumerator StartBattleRoutine()
    {
        if (isTransitioningToBattle) yield break;
        isTransitioningToBattle = true;

        // 입력/이동 정지 → 한 프레임 동기화 → 바로 스냅샷
        IsBattleNoticeActive = true;
        if (Shared.PlayerMovement != null) Shared.PlayerMovement.HaltImmediately();
        yield return new WaitForEndOfFrame();          // (1프레임 안정화)

        Shared.SceneTransitionManager.ClearExplorationSnapshot();   // 이전 스냅샷 비우기

        // 현재 상태로 새 스냅샷 저장
        var snap = new ExplorationSnapshot();
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb is IExplorationPersistable ip)
                snap.objects.Add(ip.SaveState());
        }
        snap.totalBoxes = totalBoxes;
        snap.openedBoxes = openedBoxes;
        snap.triggeredTraps = triggeredTraps;
        snap.thresholdReached = thresholdReached;
        Shared.SceneTransitionManager.SaveExplorationSnapshot(snap);
        Debug.Log($"[Snapshot] saved: objs={snap.objects.Count}, boxes {openedBoxes}/{totalBoxes}, traps={triggeredTraps}");

        // 복귀 지점 저장(스냅샷 뒤로 이동)
        string sceneName = SceneManager.GetActiveScene().name;
        Vector3 pos = (Shared.PlayerMovement != null)
                    ? Shared.PlayerMovement.transform.position
                    : (Shared.MapToggleManager != null
                        ? Shared.MapToggleManager.GetPlayerStartPosition()
                        : Vector3.zero);
        Shared.SceneTransitionManager.SaveReturnPoint(sceneName, pos);

        // 배틀 배너 보여주기
        if (battleNoticeUI != null)
        {
            battleNoticeUI.SetActive(true);
            IsBattleNoticeActive = true; // 입력 차단 시작
            yield return new WaitForSeconds(battleNoticeDuration);
            battleNoticeUI.SetActive(false);
            IsBattleNoticeActive = false; // 입력 차단 종료
        }

        var traps = snap.objects.FindAll(o => o.kind == "Trap");
        var trig = traps.FindAll(o => o.b1 || !o.b2);
        Debug.Log($"[Snapshot] traps saved triggered-or-inactive = {trig.Count}/{traps.Count}");

        // 게이지 초기화 및 컨텍스트
        awarenessGauge = 0;
        if (StageRuntimeContext.Instance == null)
            new GameObject("StageRuntimeContext").AddComponent<StageRuntimeContext>();

        int stageNo = (currentStageData != null) ? currentStageData.stageNumber
                  : (stageNumberOverride >= 0 ? stageNumberOverride : -1);
        if (stageNo < 0)
            Debug.LogWarning("[ObjectGaugeManager] stage number not set. (currentStageData or stageNumberOverride)");

        var timer = FindObjectOfType<ExplorationTimerUi>(true);
        if (timer != null)
        {
            timer.SaveRuntime();
            Debug.Log("[ExplorationTimerUi] runtime saved before battle.");
        }
        StageRuntimeContext.Instance.SetStageNumber(stageNo);
        StageRuntimeContext.Instance.SetBattleContext(BattleContext.TrapEncounter);
        Shared.SceneTransitionManager.FadeToScene("BattleScene");
        isTransitioningToBattle = false;
    }

    public void SetObjectGaugeFromSnapshot(int total, int opened, int traps, bool reached)
    {
        totalBoxes = total;
        openedBoxes = opened;
        triggeredTraps = traps;
        thresholdReached = reached;
        // UI 등 필요한 반영
        // 인지 게이지(awareness)는 0 유지 (전투 진입 직전에 0으로 내려가 있음)
    }

    #region IResettable
    public void ResetState()
    {
        openedBoxes = 0;
        triggeredTraps = 0;
        thresholdReached = false;

        awarenessGauge = 0;
        battleTriggered = false;
        UpdateAwarenessUI();

        Debug.Log("[ObjectGaugeManager] 게이지 초기화 완료");
    }
    #endregion


}
