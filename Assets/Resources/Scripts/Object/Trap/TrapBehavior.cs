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
            // 플레이어가 밟은 경우: 디버프 + Persist 상태 마킹 + 통계 + 종료 일원화
            controller.ApplyDebuff(debuffData);
            TrapPersist?.MarkTriggered();
            Shared.ObjectGaugeManager.RegisterTrapTriggeredByPlayer(); // 인지+트랩 카운트
            TriggerTrap();
            return;
        }

        var PushBox = other.GetComponent<PushObject>();
        if (PushBox != null)
        {
            // 박스가 활성화시킨 경우도 동일하게 Persist/통계를 남기고 종료 처리
            TrapPersist?.MarkTriggered();
            Shared.ObjectGaugeManager.RegisterTrapClearedByPush();
            TriggerTrap();
            return;
        }
    }

    public void TriggerTrap()
    {
        if (isTriggered) return;

        isTriggered = true;

        if (applyOnce)
            gameObject.SetActive(false); // Destroy 대신 비활성화로 처리
    }
}