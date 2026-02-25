using UnityEngine;

/// <summary>
/// 일반 포탈 컨트롤러: 연결된 맵으로 이동합니다.
/// MapConnectionData에 등록된 연결 맵만 이동 가능하며,
/// 연결되지 않은 맵으로의 이동 시도는 차단됩니다.
///
/// Inspector에서 destinationMapId에 이동할 맵 ID를 입력하세요 (예: "moat", "camp").
/// 이 ID는 MapConnectionData의 mapId, 도착 맵의 ExplorationMapData.mapId와 일치해야 합니다.
/// </summary>
public class PortalController : MonoBehaviour
{
    [Header("목적지 맵 ID")]
    [Tooltip("이동할 맵의 ID. MapConnectionData의 mapId와 일치해야 합니다. (예: moat, camp, village)")]
    public string destinationMapId;

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
    /// 포탈 사용: MapTransitionManager를 통해 맵 이동을 요청합니다.
    /// 연결되지 않은 맵이면 이동이 차단됩니다.
    /// </summary>
    public void UsePortal()
    {
        if (MapTransitionManager.Instance == null)
        {
            Debug.LogError("[PortalController] MapTransitionManager 인스턴스가 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(destinationMapId))
        {
            Debug.LogWarning("[PortalController] destinationMapId가 설정되지 않았습니다.");
            return;
        }

        // MapTransitionManager에서 연결 여부 검증 + 이동 처리
        MapTransitionManager.Instance.TravelToMap(destinationMapId);
    }
}

