using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TrapBehavior : MonoBehaviour
{
    [Tooltip("적용할 디버프 데이터")] public DebuffData debuffData;
    [Tooltip("한 번만 적용할지, 계속 적용할지")] public bool applyOnce = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        var controller = other.GetComponent<PlayerDebuffController>();
        if (controller != null && debuffData != null)
        {
            controller.ApplyDebuff(debuffData);
            ObjectGaugeManager.Instance.IncrementTrap();
            if (applyOnce)
                Destroy(gameObject);
        }
    }
}