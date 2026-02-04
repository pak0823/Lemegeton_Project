using UnityEngine;

public class EncounterMonster : MonoBehaviour
{
    [SerializeField] private bool destroyOnTrigger = true;
    public bool IsActive { get; private set; } = true;

    public void MarkConsumed()
    {
        if (!IsActive) return;
        IsActive = false;

        if (destroyOnTrigger)
            gameObject.SetActive(false); // 또는 Destroy(gameObject)
        else
        {
            // 나중에 "사용된 리소스"로 외형만 교체하고 클릭/충돌만 막기 등
            var col = GetComponent<Collider2D>(); if (col) col.enabled = false;
        }
    }
}
