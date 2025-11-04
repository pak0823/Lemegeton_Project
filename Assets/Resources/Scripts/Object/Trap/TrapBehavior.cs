using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TrapBehavior : MonoBehaviour
{
    [Tooltip("적용할 디버프 데이터")] public DebuffData debuffData;
    [Tooltip("한 번만 적용할지, 계속 적용할지")] public bool applyOnce = true;

    private bool isTriggered = false;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private TrapPersist TrapPersist;

    [Header("발동 조건 보정")]
    [SerializeField] bool requireSameCellForPush = true;          // 박스는 '같은 타일'일 때만 발동
    [SerializeField] float fallbackCellSize = 1.0f;               // 타일맵이 없을 때 거리기반 폴백
    bool _pendingTriggeredByPush = false; // Enter에서 놓친 경우 Stay에서 한 번 더 판정

    [Header("확률 설명 (트랩 발동 시)")]
    public WeightedDescriptionsSO triggerDescriptions;

    private static List<TrapBehavior> allTraps = new();
    public static IReadOnlyList<TrapBehavior> AllTraps => allTraps;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        if (!allTraps.Contains(this))
            allTraps.Add(this);
    }

    void Start()
    {

        TrapPersist = GetComponent<TrapPersist>();

        if(TrapPersist != null)
        {
            // 1회용이고 이미 발동(=비활성)이면 GO 자체 비활성
            if (applyOnce && !TrapPersist.IsActive)
                gameObject.SetActive(false);
        }
    }
    void OnDestroy()
    {
        if (allTraps.Contains(this))
            allTraps.Remove(this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var controller = other.GetComponent<PlayerDebuffController>();
        if (controller != null && debuffData != null)
        {
            TriggerTrap(other);
            return;
        }

        var pushBox = other.GetComponent<PushObject>();
        if (pushBox != null)
        {
            if (!ShouldTriggerByPush(pushBox))    // 같은 셀 아니면 무시
            {
                _pendingTriggeredByPush = true;   // Stay에서 재판정
                return;
            }

            TrapPersist?.MarkTriggered();
            Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
            TriggerTrap(other);
            return;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!_pendingTriggeredByPush || isTriggered) return;

        var pushBox = other.GetComponent<PushObject>();
        if (pushBox == null) return;

        if (ShouldTriggerByPush(pushBox))
        {
            _pendingTriggeredByPush = false;
            TrapPersist?.MarkTriggered();
            Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
            TriggerTrap(other);
        }
    }

    bool ShouldTriggerByPush(PushObject push)
    {
        if (!requireSameCellForPush) return true;

        // 1) 타일맵 기준 셀 비교 (정확)
        if (push.floorTilemap != null)
        {
            var map = push.floorTilemap;
            Vector3Int trapCell = map.WorldToCell(transform.position);
            Vector3Int boxCell = map.WorldToCell(push.transform.position);
            return trapCell == boxCell;
        }

        // 2) 폴백: 월드 거리로 근사 (타일 크기 절반 내에 들어왔을 때만)
        float half = Mathf.Max(0.1f, fallbackCellSize * 0.5f);
        return Vector2.Distance(transform.position, push.transform.position) <= half;
    }

    public void TriggerTrap(Collider2D other)
    {
        if (isTriggered) return;
        isTriggered = true;

        if (applyOnce)
            gameObject.SetActive(false); // Destroy 대신 비활성화로 처리

        var player = other.GetComponent<PlayerDebuffController>();
        if (player == null) return;  //플레이어 작동이 아닐 시
        

        int idx = triggerDescriptions ? triggerDescriptions.PickIndex() : -1;
        if (idx >= 0 && idx < triggerDescriptions.entries.Length)
        {
            switch (idx)
            {
                case 0:
                    player.ApplyDebuff(debuffData);
                    TrapPersist?.MarkTriggered();
                    Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
                    break;
                case 1: Shared.ObjectGaugeManager.RegisterTrapTriggeredByPlayer(); break;
                case 2:
                    Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
                    break;
                case 3:
                    player.ApplyDebuff(debuffData);
                    TrapPersist?.MarkTriggered();
                    Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
                    break;
                default: /* 프리셋 확장 대비 */ break;
            }

            Debug.Log("idx: " + idx);

            var text = triggerDescriptions.entries[idx].text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                // 잠금형으로 1초 정도 유지
                Shared.explorationLogUI?.Push(text);
                Shared.interactionHintUI?.HideAll();
            }   
        }
    }
}