using UnityEngine;
using System.Collections.Generic;

public class PlayerInteractionHandler : MonoBehaviour
{
    private PlayerMovement player;

    // 상호작용 대상, 포탈, 콜라이더 등
    public IInteractable PendingInteractable { get; private set; }
    public Collider2D CurrentInteractTarget { get; private set; }
    public DescriptionData CurrentDescData { get; private set; }

    [SerializeField] private LayerMask encounterLayerMask;

    public void Initialize(PlayerMovement playerMovement, LayerMask encounterLayer)
    {
        player = playerMovement;
        // 기존 PlayerMovement에 있던 encounterLayerMask를 복사해옵니다.
        encounterLayerMask = encounterLayer;
    }

    public void SetEncounterLayer(LayerMask layer)
    {
        encounterLayerMask = layer;
    }

    // ==========================================
    // Interaction & Viewpoint Logic
    // ==========================================

    public void ExecuteSurvey()
    {
        if (PendingInteractable != null)
        {
            PendingInteractable.OnInteract();
        }

        InteractionHintUI.Instance?.HideCancel();
        InteractionHintUI.Instance?.HideAll();
        ClearInteractTargets();
    }

    public void ExecuteCommunication()
    {
        if (CurrentDescData != null && DescriptionDialogUI.Instance != null && !DescriptionDialogUI.Instance.IsOpen)
        {
            DescriptionDialogUI.Instance.Show(CurrentDescData.description);
            // 관찰 시 활기가 소모되는지 명확하지 않으나, 원본에 맞게 제외 혹은 필요시 추가
        }

        InteractionHintUI.Instance?.HideCancel();
        InteractionHintUI.Instance?.HideAll();
        ClearInteractTargets();
    }

    public void SetPendingInteraction(IInteractable interactable, Collider2D collider, DescriptionData desc)
    {
        PendingInteractable = interactable;
        CurrentInteractTarget = collider;
        CurrentDescData = desc;
    }

    public void ClearInteractTargets()
    {
        PendingInteractable = null;
        CurrentInteractTarget = null;
        CurrentDescData = null;
    }

    // ==========================================
    // Encounter & Trap Checks (Map Execution)
    // ==========================================

    public bool TryGetEncounterAtCell(Vector3Int cell, out EncounterMonster monster)
    {
        monster = null;

        // PathfindingSystem을 통해 타일의 월드 좌표를 가져와서 BoxCast/OverlapCircle 수행
        if (PathfindingSystem.Instance == null) return false;

        // 가장 높은(현재 캐릭터/몬스터가 서 있는) 맵 레이어의 실제 월드 포지션(Anchor Y 포함)을 획득
        var world = PathfindingSystem.Instance.GetWorldPosForLogic(cell);

        Physics2D.SyncTransforms(); // 빌드 환경 물리 위치 즉시 동기화
        // 타일 크기나 콜라이더 크기에 따라 주변 몬스터가 잡힐 수 있으므로 반경을 0.1f로 확 줄여 정확히 해당 칸만 감지
        var hits = Physics2D.OverlapCircleAll(world, 0.1f, encounterLayerMask);

        if (hits.Length == 0)
        {
            return false;
        }

        foreach (var hit in hits)
        {
            if (hit.CompareTag("EncounterObject"))
            {
                var m = hit.GetComponent<EncounterMonster>();
                if (m != null)
                {
                    monster = m;
                    return true;
                }
            }
        }
        return false;
    }

    public void TryTriggerTrapAtCell(Vector3Int cell)
    {
        if (PathfindingSystem.Instance == null || PathfindingSystem.Instance.floorTilemap == null) return;

        var traps = TrapBehavior.allTraps;
        for (int i = 0; i < traps.Count; i++)
        {
            var trap = traps[i];
            if (trap != null && trap.gameObject.activeInHierarchy)
            {
                trap.TryTriggerByPlayer(PathfindingSystem.Instance.floorTilemap, cell);
            }
        }
    }

    public void TryConsumeTrapByBoxAtCell(Vector3Int cell)
    {
        if (PathfindingSystem.Instance == null || PathfindingSystem.Instance.floorTilemap == null) return;

        var traps = TrapBehavior.allTraps;
        for (int i = 0; i < traps.Count; i++)
        {
            var trap = traps[i];
            if (trap != null && trap.gameObject.activeInHierarchy)
            {
                trap.TryConsumeByBox(PathfindingSystem.Instance.floorTilemap, cell);
            }
        }
    }
}
