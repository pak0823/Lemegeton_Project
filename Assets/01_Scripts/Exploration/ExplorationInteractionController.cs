using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ExplorationInteractionController : MonoBehaviour
{
    private PlayerMovement playerMovement;
    
    // PlayerMovement에서 가져온 로직을 위해 필요한 참조들
    // 초기화 시 주입받거나 Find로 찾음
    
    public void Initialize(PlayerMovement movement)
    {
        this.playerMovement = movement;
    }

    public void HandleLeftClick(Vector3 mousePos)
    {
        // 카메라를 통해 월드 좌표 계산
        var cam = Camera.main;
        if (cam == null) return;
        
        if (!cam.pixelRect.Contains(mousePos)) return;

        float zDist = cam.orthographic ? 0f : (transform.position.z - cam.transform.position.z);
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, zDist));
        wp.z = 0;

        // 타일 좌표계 변환
        Vector3Int clickedCell = PathfindingSystem.Instance.GetCellFromWorldPos(wp);
        // PlayerMovement.HandleTileClickInput 로직을 이곳으로 이관 예정
        
        Debug.Log($"[Interaction] Clicked Cell: {clickedCell}");

        // 이동 중이면 입력 무시
        if (playerMovement.IsMoving) return;

        // Push 타겟 선택 모드인지 확인
        if (playerMovement.IsPushSelectMode)
        {
            // 클릭한 타일이 유효한 타겟인지 PlayerMovement가 위임받아 처리
            playerMovement.ProcessPushTargetClick(clickedCell);
            return;
        }

        // 1. 클릭 지점에 상호작용 가능한 오브젝트가 있는지 검사
        IInteractable clickedInteractable = null;
        Collider2D clickedCollider = null;
        DescriptionData clickedDesc = null;
        PushObject clickedPush = null;
        PortalController clickedPortal = null;

        var hits = Physics2D.OverlapPointAll(wp);

        foreach (var h in hits)
        {
            // PushObject 감지
            var push = h.GetComponentInParent<PushObject>();
            if (push != null)
            {
                clickedPush = push;
                if (!clickedCollider) clickedCollider = h;
            }

            // 상자(부모 포함) 검사
            var chest = h.GetComponentInParent<IInteractable>();
            if (chest != null)
            {
                if (chest.CanInteract == false) continue;
                if (clickedInteractable == null) clickedInteractable = chest;
                if (!clickedCollider) clickedCollider = h;
            }

            // 설명 데이터
            if (clickedDesc == null && h.TryGetComponent<DescriptionData>(out var descriptiondata))
            {
                clickedDesc = descriptiondata;
                if (!clickedCollider) clickedCollider = h;
            }

            // Portal 감지
            var portal = h.GetComponentInParent<PortalController>();
            if (portal != null)
            {
                if (clickedPortal == null) clickedPortal = portal;
                if (!clickedCollider) clickedCollider = h;
            }
        }

        // 2. PushObject 처리
        if (clickedPush != null)
        {
            playerMovement.ProcessPushObjectClick(clickedPush);
            return;
        }

        // 3. 상호작용(상자, 포탈 등) 처리
        if (clickedInteractable != null || clickedPortal != null || clickedCollider != null)
        {
            Transform targetTr = clickedInteractable != null ? clickedInteractable.GetTransform() :
                                (clickedPortal != null ? clickedPortal.transform :
                                    clickedCollider.transform);

            playerMovement.ProcessInteractionClick(clickedCell, targetTr, clickedInteractable, clickedPortal, clickedCollider, clickedDesc);
            return;
        }

        // 4. 일반 이동 (바닥 클릭)
        if (!PathfindingSystem.Instance.IsWalkableCell(clickedCell))
        {
            Debug.Log($"[이동 불가] 좌표: {clickedCell}");
            return;
        }

        playerMovement.ProcessMoveClick(clickedCell);
    }

    public void HandleRightClick()
    {
        playerMovement.ProcessRightClick();
    }
}
