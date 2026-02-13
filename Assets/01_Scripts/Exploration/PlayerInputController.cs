using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInputController : MonoBehaviour
{
    private ExplorationInteractionController interactionController;
    
    // UI 클릭 차단 여부
    private bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    public void Initialize(ExplorationInteractionController interactionCtrl)
    {
        this.interactionController = interactionCtrl;
    }

    void Update()
    {
        // 입력 차단 조건 확인 (필요 시 PlayerMovement나 Manager에서 상태를 받아옴)
        // 여기서는 기본적으로 UI 위 클릭이나, 다른 모달이 떴을 때 입력을 막는 로직을 수행
        
        if (IsPointerOverUI()) return;

        // 좌클릭: 이동 또는 상호작용
        if (Input.GetMouseButtonDown(0))
        {
            if (interactionController != null)
            {
                interactionController.HandleLeftClick(Input.mousePosition);
            }
        }
        
        // 우클릭: 취소
        if (Input.GetMouseButtonDown(1))
        {
            if (interactionController != null)
            {
                interactionController.HandleRightClick();
            }
        }
    }
}
