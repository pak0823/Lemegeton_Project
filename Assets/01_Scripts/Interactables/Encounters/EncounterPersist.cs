using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EncounterPersist : MonoBehaviour, IExplorationPersistable
{
    private ExplorationPersistId pid;

    [SerializeField] private bool consumed = false; // 이미 인카운터로 소비되었는지

    public bool IsConsumed => consumed;

    void Awake()
    {
        pid = GetComponent<ExplorationPersistId>();
        if (!pid) pid = gameObject.AddComponent<ExplorationPersistId>();
    }

    public void MarkConsumed()
    {
        if (consumed) return;
        consumed = true;

        // 인카운터는 “다시 밟히면 안 됨” → 비활성화(또는 리소스 변경)
        // 지금은 최소 처리로 비활성화
        gameObject.SetActive(false);
    }

    // IExplorationPersistable
    public string PersistID => pid.Id;

    public ExplorationObjectState SaveState()
    {
        // 현재 활성 여부를 기준으로 consumed 재산출도 가능
        bool consumedNow = consumed || !gameObject.activeInHierarchy;

        return new ExplorationObjectState
        {
            id = PersistID,
            kind = "Encounter",
            prefabName = gameObject.name.Replace("(Clone)", "").Trim(),
            position = transform.position,
            b1 = consumedNow, // consumed 여부
            b2 = gameObject.activeInHierarchy // 참고용(확장)
        };
    }

    public void LoadState(ExplorationObjectState s)
    {
        transform.position = s.position;
        consumed = s.b1;

        if (consumed)
        {
            gameObject.SetActive(false);
            return;
        }

        // 소비되지 않은 상태면 활성화 보정
        gameObject.SetActive(true);
        var col = GetComponent<Collider2D>();
        if (col) col.enabled = true;
    }
}
