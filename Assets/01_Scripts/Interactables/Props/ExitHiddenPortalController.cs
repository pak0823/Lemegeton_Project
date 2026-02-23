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
public class ExitHiddenPortalController : MonoBehaviour, IInteractable
{
    [Header("힌트 UI 라벨")]
    [SerializeField] private string hintLabel = "탈출";

    // IInteractable Property
    public bool CanInteract => true;
    public string GetInteractLabel() => hintLabel;

    // InteractionHintUI에서 라벨을 가져가기 위한 레거시 메서드 (필요시 삭제)
    public string GetHintLabel() => hintLabel;

    public void OnInteract()
    {
        UsePortal();
    }

    public void SetHighlight(bool isActive)
    {
        // 하이라이트 연출 필요시 구현
    }

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// 탈출 포탈 사용: 저장된 원래 맵으로 복귀합니다.
    /// PlayerInteractionHandler에서 UsePortal() 대신 이 메서드를 호출하거나
    /// PortalController와 동일한 방식으로 상호작용에 연결합니다.
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

