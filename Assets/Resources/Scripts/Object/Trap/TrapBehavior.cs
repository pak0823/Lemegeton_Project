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

        var PushBox = other.GetComponent<PushObject>();
        if (PushBox != null)
        {
            // 박스가 활성화시킨 경우도 동일하게 Persist/통계를 남기고 종료 처리
            TrapPersist?.MarkTriggered();
            Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
            TriggerTrap(other);
            return;
        }
    }

    public void TriggerTrap(Collider2D player)
    {
        if (isTriggered) return;

        isTriggered = true;

        if (applyOnce)
            gameObject.SetActive(false); // Destroy 대신 비활성화로 처리

        var current = player.GetComponent<PlayerDebuffController>();
        if (current == null) return;  //플레이어 작동이 아닐 시
        

        int idx = triggerDescriptions ? triggerDescriptions.PickIndex() : -1;
        if (idx >= 0 && idx < triggerDescriptions.entries.Length)
        {
            switch (idx)
            {
                case 0:
                    current.ApplyDebuff(debuffData);
                    TrapPersist?.MarkTriggered();
                    Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
                    break;
                case 1: Shared.ObjectGaugeManager.RegisterTrapTriggeredByPlayer(); break;
                case 2:
                    Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
                    break;
                case 3:
                    current.ApplyDebuff(debuffData);
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