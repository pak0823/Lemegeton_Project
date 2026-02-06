using UnityEngine;
using System.Collections.Generic;
using Project.UI; // For potentially common UI interfaces if needed, but not strictly required here

public class ExplorationSidebarController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private List<ExplorationStatusSlot> statusSlots = new List<ExplorationStatusSlot>();
    [SerializeField] private Transform slotsRoot; // 슬롯들이 모여있는 부모 (Stat)

    private void Start()
    {
        // 1. 컴포넌트 찾기
        if (slotsRoot != null)
        {
            statusSlots.Clear();
            foreach (Transform child in slotsRoot)
            {
                var slot = child.GetComponent<ExplorationStatusSlot>();
                if (slot != null) statusSlots.Add(slot);
            }
        }
        else
        {
            // Root가 안잡혀있으면 자식 전체에서 검색 (혹은 직접 할당 유도)
            if (statusSlots.Count == 0)
            {
                var slots = GetComponentsInChildren<ExplorationStatusSlot>(true);
                statusSlots.AddRange(slots);
            }
        }

        if (statusSlots.Count == 0)
        {
            Debug.LogWarning($"[ExplorationSidebarController] No 'ExplorationStatusSlot' found. Please check hierarchy or assign in inspector.");
        }

        // 2. 초기화 시작
        Debug.Log($"[ExplorationSidebarController] Start. Slots found: {statusSlots.Count}");
        InitializeStatusUI();
    }

    private void InitializeStatusUI()
    {
        StartCoroutine(Co_InitializeStatusUI());
    }

    private System.Collections.IEnumerator Co_InitializeStatusUI()
    {
        // 1. 매니저가 준비될 때까지 대기
        while (PlayerDataManager.Instance == null)
        {
            Debug.Log("[ExplorationSidebarController] Waiting for PlayerDataManager...");
            yield return null;
        }

        // 2. 로딩이 끝날 때까지 대기
        while (PlayerDataManager.Instance.IsLoading)
        {
            Debug.Log("[ExplorationSidebarController] Waiting for Unit Loading...");
            yield return null;
        }

        // 3. 유닛 리스트가 확보될 때까지 잠시 대기 (안전장치)
        if (PlayerDataManager.Instance.ownedUnits.Count == 0)
        {
            Debug.LogWarning("[ExplorationSidebarController] Owned units are 0. Waiting for data...");
            // 타임아웃을 둘 수도 있지만, 일단은 이벤트나 상태 변화를 기다리거나,
            // 여기서는 일단 바인딩을 시도합니다. (빈 상태로라도)
        }

        Debug.Log($"[ExplorationSidebarController] Initialization Ready. Binding {PlayerDataManager.Instance.ownedUnits.Count} units.");
        BindUnits();
    }

    private void OnDestroy()
    {
        // Coroutine 사용으로 이벤트 구독 해제 불필요
    }

    private void BindUnits()
    {
        var ownedUnits = PlayerDataManager.Instance.ownedUnits;
        
        for (int i = 0; i < statusSlots.Count; i++)
        {
            if (i < ownedUnits.Count)
            {
                statusSlots[i].Bind(ownedUnits[i]);
            }
            else
            {
                statusSlots[i].Bind(null); // 남는 슬롯은 비활성화
            }
        }
        
        Debug.Log($"[ExplorationSidebarController] Binding {Mathf.Min(ownedUnits.Count, statusSlots.Count)} units. Total Owned: {ownedUnits.Count}");
    }
}
