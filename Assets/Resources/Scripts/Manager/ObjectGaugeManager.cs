using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.UI;

// 맵 내 전체 상자 개수 대비
// 열린 상자 + 발동된 함정 수를 게이지로 계산
public class ObjectGaugeManager : MonoBehaviour, IResettable
{
    public bool IsBattleNoticeActive { get; private set; } = false;

    [Header("Object 게이지")]
    [Tooltip("게이지 임계치 (0~1) 이상일 때 이벤트 발생")]
    public float thresholdPercent = 0.66f;
    public UnityEvent onThresholdReached;

    private int totalBoxes;
    private int openedBoxes;
    private int triggeredTraps;
    private bool thresholdReached;

    [Header("인지 게이지")]
    public int awarenessMax = 5;
    public float chestIncrementChance = 0.2f;
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

        // 인지 게이지 확률 증가
        TryIncrementAwarenessByChest();
    }

    // 함정 발동 시 호출
    public void IncrementTrap()
    {
        triggeredTraps++;
        Debug.Log($"[ObjectGaugeManager] 발동된 함정 수: {triggeredTraps}");
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
    private void TryIncrementAwarenessByChest()
    {
        if (battleTriggered) return;

        if (Random.value <= chestIncrementChance)
            IncrementAwareness();
    }

    public void IncrementAwarenessByTrap()
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

    private IEnumerator StartBattleRoutine()
    {
        if (battleNoticeUI != null)
        {
            battleNoticeUI.SetActive(true);
            IsBattleNoticeActive = true; // 입력 차단 시작
            yield return new WaitForSeconds(battleNoticeDuration);
            battleNoticeUI.SetActive(false);
            IsBattleNoticeActive = false; // 입력 차단 종료
        }

        // 씬 전환 전에 게이지 초기화
        awarenessGauge = 0;

        Shared.SceneTransitionManager.FadeToScene("BattleScene");
    }
    #endregion

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
