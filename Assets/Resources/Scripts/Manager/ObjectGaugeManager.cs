using UnityEngine;
using UnityEngine.Events;

// 맵 내 전체 상자 개수 대비
// 열린 상자 + 발동된 함정 수를 게이지로 계산
public class ObjectGaugeManager : MonoBehaviour
{
    public static ObjectGaugeManager Instance { get; private set; }

    [Tooltip("게이지 임계치 (0~1) 이상일 때 이벤트 발생")] public float thresholdPercent = 0.6f;
    public UnityEvent onThresholdReached;

    private int totalBoxes;
    private int openedBoxes;
    private int triggeredTraps;
    private bool thresholdReached;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        // 씬 전환 등에도 유지하고 싶다면: DontDestroyOnLoad(gameObject);
    }

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
}
