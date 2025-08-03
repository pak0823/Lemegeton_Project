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
        if (applyOnce && isTriggered)
            gameObject.SetActive(false);
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
            controller.ApplyDebuff(debuffData);
            TriggerTrap();
        }

        var PushBox = other.GetComponent<PushObject>();
        if (PushBox != null)
        {
            gameObject.SetActive(false);
        }
    }

    public void TriggerTrap()
    {
        if (isTriggered) return;

        isTriggered = true;
        ObjectGaugeManager.Instance.IncrementTrap();

        if (applyOnce)
            gameObject.SetActive(false); // Destroy 대신 비활성화로 처리
    }
}