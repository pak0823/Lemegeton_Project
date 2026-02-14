using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Project.Data;

public class GatherableObject : MonoBehaviour, IInteractable, IExplorationPersistable
{
    [Header("설정 데이터")]
    public GatherableDataSO data;

    [Header("상태")]
    [SerializeField] private bool isInteracted = false;
    private ExplorationPersistId pid;

    [Header("시각 효과")]
    public SpriteRenderer spriteRenderer;
    public Sprite interactedSprite; // 상호작용 후 변경될 스프라이트 (선택사항)
    private Color originalColor;
    private bool isHighlighted = false;

    // IInteracable 구현
    public bool CanInteract => !isInteracted;

    private void Awake()
    {
        pid = GetComponent<ExplorationPersistId>();
        if (!pid) pid = gameObject.AddComponent<ExplorationPersistId>();

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer) originalColor = spriteRenderer.color;
    }

    private void Start()
    {
        if (isInteracted)
        {
            ApplyInteractedState();
        }
    }

    public Transform GetTransform() => transform;

    public void SetHighlight(bool isActive)
    {
        if (isInteracted) return;

        if (spriteRenderer && isHighlighted != isActive)
        {
            isHighlighted = isActive;
            spriteRenderer.color = isActive ? new Color(0.4f, 1f, 1f, 1f) : originalColor;
        }
    }

    public void OnInteract()
    {
        if (isInteracted || data == null) return;

        // 1. 활기 소모 확인
        if (VigorManager.Instance != null)
        {
            if (!VigorManager.Instance.TrySpend(data.vigorCost, VigorSpendReason.InspectBox))
            {
                // TrySpend 내부에서 실패 팝업/로그 처리가 될 수도 있지만, 
                // 명시적으로 "부족" 메시지를 띄우고 싶다면 여기서 처리 가능
                ExplorationLogUI.Instance?.Push($"활기가 부족합니다. (필요: {data.vigorCost})");
                return;
            }
        }

        // 2. 상호작용 처리
        PerformInteraction();
    }

    private void PerformInteraction()
    {
        isInteracted = true;
        SetHighlight(false);

        // 결과 추첨
        var outcome = data.PickOutcome();
        if (outcome != null)
        {
            // 로그 출력 (결과 텍스트)
            if (!string.IsNullOrEmpty(outcome.resultText))
            {
                ExplorationLogUI.Instance?.Push(outcome.resultText);
            }

            // 실제 효과 실행 (보상, 함정 등)
            // 현재 플레이어 캐릭터를 찾아 전달 (PlayerMovement -> UnitData?) 
            // *주의* 현재 ExplorationScene에서는 PlayerMovement가 "Leader" 유닛을 대변함.
            // 정확한 "상호작용 주체"를 찾기 위해 PlayerDataManager의 첫 번째 유닛을 사용하거나, 
            // PlayerMovement에서 현재 리더 정보를 가져와야 함.
            
            UnitData targetUnit = GetInteractionTarget();
            if (targetUnit != null && outcome.outcome != null)
            {
                outcome.outcome.Execute(targetUnit);
            }
        }

        ApplyInteractedState();
    }

    private UnitData GetInteractionTarget()
    {
        // 간소화: 현재 파티의 리더(0번) 혹은 첫 번째 생존 유닛을 대상으로 함
        if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.ownedUnits.Count > 0)
        {
            return PlayerDataManager.Instance.ownedUnits[0]; 
        }
        return null;
    }

    private void ApplyInteractedState()
    {
        // 상호작용 후 시각적 변화
        if (interactedSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = interactedSprite;
        }
        else
        {
            // 스프라이트가 없으면 오브젝트를 비활성화 하거나 투명하게 처리
             gameObject.SetActive(false);
        }
    }

    // 저장 시스템 연동
    public string PersistID => pid.Id;

    public ExplorationObjectState SaveState()
    {
        return new ExplorationObjectState
        {
            id = PersistID,
            kind = "Gatherable",
            prefabName = gameObject.name.Replace("(Clone)", "").Trim(),
            position = transform.position,
            b1 = isInteracted
        };
    }

    public void LoadState(ExplorationObjectState s)
    {
        transform.position = s.position;
        isInteracted = s.b1;
        if (isInteracted)
        {
            ApplyInteractedState();
        }
    }
}
