using UnityEngine;

/// <summary>
/// 일반 포탈 컨트롤러: 연결된 맵으로 이동합니다.
/// MapConnectionData에 등록된 연결 맵만 이동 가능하며,
/// 연결되지 않은 맵으로의 이동 시도는 차단됩니다.
///
/// Inspector에서 destinationMapId에 이동할 맵 ID를 입력하세요 (예: "moat", "camp").
/// 이 ID는 MapConnectionData의 mapId, 도착 맵의 ExplorationMapData.mapId와 일치해야 합니다.
/// </summary>
public class PortalController : MonoBehaviour, IInteractable
{
    [Header("목적지 맵 ID")]
    [Tooltip("이동할 맵의 ID. MapConnectionData의 mapId와 일치해야 합니다. (예: moat, camp, village)")]
    public string destinationMapId;

    [Header("힌트 UI 라벨")]
    [Tooltip("플레이어가 포탈에 가까이 갔을 때 표시되는 상호작용 힌트 텍스트")]
    [SerializeField] private string hintLabel = "이동";

    // IInteractable Property
    public bool CanInteract => !string.IsNullOrEmpty(destinationMapId);
    public string GetInteractLabel() => hintLabel;

    // InteractionHintUI에서 라벨을 가져가기 위한 레거시 지원 (필요시 삭제 가능)
    public string GetHintLabel() => hintLabel;

    public void OnInteract()
    {
        UsePortal();
    }

    public void SetHighlight(bool isActive)
    {
        // 포탈 하이라이트 연출이 필요하다면 여기에 구현 (현재는 생략)
    }

    public Transform GetTransform()
    {
        return transform;
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

