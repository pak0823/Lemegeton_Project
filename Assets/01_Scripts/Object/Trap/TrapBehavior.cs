using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TrapBehavior : MonoBehaviour
{
    public static readonly List<TrapBehavior> allTraps = new List<TrapBehavior>();

    [Header("Trap Settings")]
    [SerializeField] private bool applyOnce = true;
    [SerializeField] private WeightedDescriptionsSO triggerDescriptions;

    // (선택) 타일을 명시적으로 박아두고 싶으면 사용
    // 비워두면 transform.position을 floorMap.WorldToCell로 환산해서 사용
    [SerializeField] private bool useExplicitCell = false;
    [SerializeField] private Vector3Int explicitCell;

    private bool isTriggered;

    void Awake()
    {
        if (!allTraps.Contains(this))
            allTraps.Add(this);
    }

    void OnDestroy()
    {
        if (allTraps.Contains(this))
            allTraps.Remove(this);
    }

    // 플레이어가 함정이 있는 셀에 존재하면 발동
    public void TryTriggerByPlayer(Tilemap floorMap, Vector3Int playerCell)
    {
        if (isTriggered) return;
        if (floorMap == null) return;

        Vector3Int trapCell = useExplicitCell ? explicitCell : floorMap.WorldToCell(transform.position);
        if (trapCell != playerCell) return;

        isTriggered = true;

        // 활기 소모 (즉시 소모 유지)
        var vigor = VigorManager.Instance;
        if (vigor != null)
        {
            int cost = Mathf.Max(0, vigor.costTriggerTrap);
            if (cost > 0 && !vigor.TrySpend(cost, VigorSpendReason.TriggerTrap))
            {
                vigor.FailExploration($"활기가 부족해 트랩 피해를 감당하지 못했습니다. (필요 {cost}, 현재 {vigor.CurrentVigor})");
                return;
            }
        }

        // 로그 출력
        int idx = triggerDescriptions ? triggerDescriptions.PickIndex() : -1;
        if (idx >= 0 && idx < triggerDescriptions.entries.Length)
        {
            var text = triggerDescriptions.entries[idx].text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                ExplorationLogUI.Instance?.Push(text);
                // 여기서 UI를 닫는 게 “이동 정지”에 영향을 주는 구조가 아니라면 유지 가능
                InteractionHintUI.Instance?.HideAll();
            }
        }

        // 1회성 함정이면 비활성화
        if (applyOnce)
            gameObject.SetActive(false);
    }

    // 박스가가 함정이 있는 셀에 존재하면 발동
    public void TryConsumeByBox(Tilemap floorMap, Vector3Int boxCell)
    {
        if (isTriggered) return;
        if (floorMap == null) return;

        Vector3Int trapCell = useExplicitCell ? explicitCell : floorMap.WorldToCell(transform.position);
        if (trapCell != boxCell) return;

        // “상자에 의해 제거”이므로 발동 처리(활기/로그 없음)
        isTriggered = true;

        // 1회성 함정이면 비활성화(또는 Destroy로 바꿔도 됨)
        if (applyOnce)
            gameObject.SetActive(false);
    }
}
