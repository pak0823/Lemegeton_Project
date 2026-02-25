using UnityEngine;

/// <summary>
/// 숨겨진 맵 탈출 포탈 컨트롤러.
/// 숨겨진 맵 내에 배치하며, 플레이어가 이 오브젝트와 상호작용(UsePortal)하면
/// MapTransitionManager.ExitHiddenMap()을 호출하여 원래 맵으로 복귀합니다.
///
/// [설정 방법]
/// - 숨겨진 맵 프리팹에 이 컴포넌트를 가진 오브젝트를 배치합니다.
/// - 기존 포탈 오브젝트와 동일하게 IInteractable 인터페이스를 통해 상호작용합니다.
/// </summary>
public class ExitHiddenPortalController : MonoBehaviour
{
    private void OnEnable()
    {
        PlayerMovement.OnTileStepped += HandleTileStepped;
    }

    private void OnDisable()
    {
        PlayerMovement.OnTileStepped -= HandleTileStepped;
    }

    private void HandleTileStepped(Vector3Int arrivedCell)
    {
        if (PathfindingSystem.Instance == null || PathfindingSystem.Instance.floorTilemap == null) return;

        // 1. 콜라이더 바운딩 박스 겹침 확인 (직관적인 시각적 매칭)
        // 플레이어 몸체(Collider) 와 포탈 스프라이트(Collider) 영역이 조금이라도 겹치면 포탈 작동
        Collider2D myCol = GetComponent<Collider2D>();
        var player = PlayerMovement.Instance;
        if (myCol != null && player != null)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol != null && myCol.bounds.Intersects(playerCol.bounds))
            {
                UsePortal();
                return;
            }
        }

        // 2. 콜라이더가 제대로 없을 경우를 대비한 최후의 셀 매칭 (정 중앙 배치용)
        var tilemap = PathfindingSystem.Instance.floorTilemap;
        Vector3 anchorOffset = tilemap.tileAnchor;
        Vector3 logicalPosition = transform.position - anchorOffset;
        Vector3Int myCell = tilemap.WorldToCell(logicalPosition);

        if (arrivedCell == myCell)
        {
            UsePortal();
        }
    }

    /// <summary>
    /// 탈출 포탈 사용: 저장된 원래 맵으로 복귀합니다.
    /// </summary>
    public void UsePortal()
    {
        if (MapTransitionManager.Instance == null)
        {
            Debug.LogError("[ExitHiddenPortalController] MapTransitionManager 인스턴스가 없습니다.");
            return;
        }

        if (!MapTransitionManager.Instance.IsInHiddenMap)
        {
            Debug.LogWarning("[ExitHiddenPortalController] 현재 숨겨진 맵에 있지 않습니다.");
            return;
        }

        Debug.Log("[ExitHiddenPortalController] 숨겨진 맵 탈출 요청");
        MapTransitionManager.Instance.ExitHiddenMap();
    }
}

