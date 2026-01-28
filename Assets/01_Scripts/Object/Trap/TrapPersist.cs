using UnityEngine;

public class TrapPersist : MonoBehaviour, IExplorationPersistable
{
    private ExplorationPersistId pid;
    [SerializeField] private bool isTriggered = false;
    [SerializeField] private bool isActive = true;

    public bool IsTriggered => isTriggered;
    public bool IsActive => isActive;

    void Awake()
    {
        pid = GetComponent<ExplorationPersistId>();
        if (!pid) pid = gameObject.AddComponent<ExplorationPersistId>();
    }

    // 게임 중 함정이 발동될 때 이 메서드를 호출
    public void MarkTriggered()
    {
        isTriggered = true;
        isActive = false;
        var col = GetComponent<Collider2D>(); if (col) col.enabled = false;
        var sr = GetComponent<SpriteRenderer>(); if (sr) sr.color = new Color(1, 1, 1, 0.4f);
    }

    // IExplorationPersistable
    public string PersistID => pid.Id;

    public ExplorationObjectState SaveState()
    {
        // 실제 활성/비활성 상태를 강제로 재산출해서 저장
        var col = GetComponent<Collider2D>();
        bool activeNow = gameObject.activeInHierarchy && (col == null || col.enabled);
        bool triggeredNow = isTriggered || !gameObject.activeInHierarchy || (col != null && !col.enabled);

        return new ExplorationObjectState
        {
            id = PersistID,
            kind = "Trap",
            prefabName = gameObject.name.Replace("(Clone)", "").Trim(),
            position = transform.position,
            b1 = triggeredNow, // Triggered
            b2 = activeNow // Active
        };
    }

    public void LoadState(ExplorationObjectState s)
    {
        transform.position = s.position;
        isTriggered = s.b1; 
        isActive = s.b2;

        // Triggered이거나 Inactive면 곧장 사라져야 함
        if (s.b1 || !s.b2)
        {
            gameObject.SetActive(false);
            return;
        }

        // 활성 상태면, 안전하게 콜라이더/비주얼 재설정
        var col = GetComponent<Collider2D>(); if (col) col.enabled = true;
        var sr = GetComponent<SpriteRenderer>(); if (sr) sr.color = Color.white;

    }
}
