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
        if (playerMovement.PushHandler.IsPushSelectMode)
        {
            // 클릭한 타일이 유효한 타겟인지 PlayerMovement가 위임받아 처리
            playerMovement.PushHandler.ProcessPushTargetClick(clickedCell);
            return;
        }

        // 1. 클릭 지점에 상호작용 가능한 오브젝트가 있는지 검사
        IInteractable clickedInteractable = null;
        Collider2D clickedCollider = null;
        DescriptionData clickedDesc = null;
        PushObject clickedPush = null;

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

            // 상자, 포탈 등 IInteractable 통합 검사
            var interactableObj = h.GetComponentInParent<IInteractable>();
            if (interactableObj != null)
            {
                if (interactableObj.CanInteract == false) continue;
                if (clickedInteractable == null) clickedInteractable = interactableObj;
                if (!clickedCollider) clickedCollider = h;
            }

            // 설명 데이터
            if (clickedDesc == null && h.TryGetComponent<DescriptionData>(out var descriptiondata))
            {
                clickedDesc = descriptiondata;
                if (!clickedCollider) clickedCollider = h;
            }
        }

        // 2. PushObject 처리
        if (clickedPush != null)
        {
            playerMovement.PushHandler.ProcessPushObjectClick(clickedPush);
            return;
        }

        // 3. 상호작용(상자, 포탈 등) 처리
        if (clickedInteractable != null || clickedCollider != null)
        {
            Transform targetTr = clickedInteractable != null ? clickedInteractable.GetTransform() :
                                    clickedCollider.transform;

            playerMovement.ProcessInteractionClick(clickedCell, targetTr, clickedInteractable, clickedCollider, clickedDesc);
            return;
        }

        // 4. 일반 이동 (바닥 클릭)
        if (!PathfindingSystem.Instance.IsWalkableCell(clickedCell))
        {
            Debug.Log($"[이동 불가] 좌표: {clickedCell}");
            return;
        }

        ProcessMoveClick(clickedCell);
    }

    public void HandleRightClick()
    {
        ProcessRightClick();
    }

    // ==========================================
    // Interaction Process Logic (Moved from PlayerMovement)
    // ==========================================

    public void OnClickSurveyButton()
    {
        DescriptionDialogUI.Instance?.Hide();
        InteractionHintUI.Instance?.HideAll();

        if (playerMovement.IsInputBlocked) return;
        if (playerMovement.IsMoving) return;

        // Push 상자 관련 처리는 PlayerMovement > PlayerPushHandler로 이관됨.
        if (playerMovement.PushHandler.PendingPushBox != null)
        {
            var box = playerMovement.PushHandler.PendingPushBox;
            var playerCell = playerMovement.floorTilemap.WorldToCell(playerMovement.rb.position);
            var boxCell = playerMovement.floorTilemap.WorldToCell(box.transform.position);

            if (playerMovement.PushHandler.IsAdjacentOrSame(playerCell, boxCell))
            {
                playerMovement.PushHandler.EnterPushSelectMode(box);
                return;
            }

            var pathToReady = playerMovement.PushHandler.FindPathToPushReadyCell(playerCell, boxCell, box);
            if (pathToReady == null || pathToReady.Count < 2)
            {
                ExplorationLogUI.Instance?.Push("해당 상자를 밀 수 있는 위치로 이동할 수 없습니다.");
                box.SetHighlight(false);
                playerMovement.PushHandler.HaltPushImmediately();
                InteractionHintUI.Instance?.HideAll();
                return;
            }

            InteractionHintUI.Instance?.HideAll();

            System.Action onArrive = () =>
            {
                if (box == null) return;
                playerMovement.PushHandler.EnterPushSelectMode(box);
            };

            playerMovement.StartPathMove(pathToReady, onArrive);
            return;
        }

        // 상호작용 지점이 가깝거나 사거리 내인 경우 즉시 실행
        if (playerMovement.currentPathCells == null || playerMovement.currentPathCells.Count < 2)
        {
            playerMovement.InteractionHandler.ExecuteSurvey();
            playerMovement.ClearPath();
            return;
        }

        // 목표 지점까지 이동 후 상호작용 실행
        System.Action onArriveSurvey = () =>
        {
            playerMovement.InteractionHandler.ExecuteSurvey();
        };

        playerMovement.StartPathMove(playerMovement.currentPathCells, onArriveSurvey);
    }

    public void OnClickCommunicationButton()
    {
        if (playerMovement.IsInputBlocked) return;
        if (playerMovement.IsMoving) return;

        // 사거리 내 즉시 실행
        if (playerMovement.currentPathCells == null || playerMovement.currentPathCells.Count < 2)
        {
            playerMovement.InteractionHandler.ExecuteCommunication();
            return;
        }

        // 타겟까지 이동 후 실행
        System.Action onArriveComm = () =>
        {
            playerMovement.InteractionHandler.ExecuteCommunication();
        };

        playerMovement.StartPathMove(playerMovement.currentPathCells, onArriveComm);
    }

    public bool HandleGlobalClickBlocking()
    {
        if (DescriptionDialogUI.Instance != null && DescriptionDialogUI.Instance.IsOpen)
        {
            DescriptionDialogUI.Instance.Hide();
            playerMovement.HaltImmediately();
            return true; // 입력 소비됨
        }

        if (playerMovement.IsInputBlocked)
        {
            if (!playerMovement.IsMoving) playerMovement.HaltImmediately();
            return true;
        }
        return false;
    }

    public void ProcessRightClick()
    {
        // 우클릭은 다이얼로그 등을 닫는 동작으로 사용될 수도 있지만,
        // 기존 로직에서는 좌클릭으로 닫았음.
        // 우클릭 시에도 일단 블락 체크
        if (playerMovement.IsInputBlocked) return;

        if (playerMovement.PushHandler.IsPushSelectMode)
        {
            playerMovement.PushHandler.ExitPushSelectMode();
            return;
        }

        if (playerMovement.PushHandler.PendingPushBox != null)
        {
            playerMovement.PushHandler.HaltPushImmediately();
            InteractionHintUI.Instance?.HideAll();
            return;
        }

        // 일반 이동/상호작용 취소
        if (playerMovement.selectedTargetCell.HasValue || (playerMovement.currentPathCells != null && playerMovement.currentPathCells.Count > 0))
        {
            CancelSelectionAndHint();
        }
    }

    public void ProcessInteractionClick(Vector3Int clickedCell, Transform targetTr, IInteractable interactable, Collider2D collider, DescriptionData desc)
    {
        if (HandleGlobalClickBlocking()) return;
        if (playerMovement.IsMoving) return;

        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(playerMovement.rb.position);
        currentCell.z = 0;

        bool isAdjacentOrSame = false;
        if (currentCell == clickedCell)
        {
            isAdjacentOrSame = true;
        }
        else
        {
             Direction[] dirs = { Direction.West, Direction.East, Direction.NW, Direction.NE, Direction.SW, Direction.SE };
             bool odd = (clickedCell.y & 1) != 0;
             foreach (var dir in dirs)
             {
                 Vector3Int offset = PathfindingSystem.Instance.GetOffsetForDirection(dir, odd);
                 if (clickedCell + offset == currentCell) { isAdjacentOrSame = true; break; }
             }
        }

        if (isAdjacentOrSame)
        {
            playerMovement.selectedTargetCell = currentCell;
            playerMovement.currentPathCells = new List<Vector3Int> { currentCell };

            playerMovement.InteractionHandler.SetPendingInteraction(interactable, collider ?? (interactable != null ? interactable.GetTransform().GetComponent<Collider2D>() : null), desc);

            playerMovement.ShowPathPreview(playerMovement.currentPathCells);

            InteractionHintUI.Instance?.HideAll();

            if (interactable != null && desc != null)
                InteractionHintUI.Instance?.ShowBothAt(targetTr, interactable.GetInteractLabel());
            else if (interactable != null)
                InteractionHintUI.Instance?.ShowSurveyAt(targetTr, interactable.GetInteractLabel());
            else if (desc != null)
                InteractionHintUI.Instance?.ShowBothAt(targetTr);
            else
                InteractionHintUI.Instance?.ShowSurveyAt(targetTr);

            InteractionHintUI.Instance?.ShowCancelAt(targetTr);
            return;
        }

        var newPath = PathfindingSystem.Instance.FindPathToAdjacentCell(currentCell, clickedCell);

        if (newPath == null || newPath.Count < 2)
        {
            ClearAllSelection();
            return;
        }

        playerMovement.selectedTargetCell = newPath[newPath.Count - 1];
        playerMovement.currentPathCells = newPath;

        playerMovement.InteractionHandler.SetPendingInteraction(interactable, collider ?? (interactable != null ? interactable.GetTransform().GetComponent<Collider2D>() : null), desc);

        playerMovement.ShowPathPreview(newPath);

        if (interactable != null && desc != null)
            InteractionHintUI.Instance?.ShowBothAt(targetTr, interactable.GetInteractLabel());
        else if (interactable != null)
            InteractionHintUI.Instance?.ShowSurveyAt(targetTr, interactable.GetInteractLabel());
        else if (desc != null)
            InteractionHintUI.Instance?.ShowBothAt(targetTr);
        else
            InteractionHintUI.Instance?.ShowSurveyAt(targetTr);

        InteractionHintUI.Instance?.ShowCancelAt(targetTr);
    }

    public void ProcessMoveClick(Vector3Int clickedCell)
    {
        if (HandleGlobalClickBlocking()) return;
        if (playerMovement.IsMoving) return;

        if (playerMovement.selectedTargetCell.HasValue
            && playerMovement.selectedTargetCell.Value == clickedCell
            && playerMovement.currentPathCells != null
            && playerMovement.currentPathCells.Count >= 2)
        {
            playerMovement.StartPathMove(playerMovement.currentPathCells, playerMovement.pathArrivalCallback);
            return;
        }

        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(playerMovement.rb.position);
        currentCell.z = 0;

        var newPath = PathfindingSystem.Instance.FindPath(currentCell, clickedCell);

        if (newPath == null || newPath.Count <= 1)
        {
             ClearAllSelection();
             return;
        }

        playerMovement.selectedTargetCell = clickedCell;
        playerMovement.currentPathCells = newPath;

        playerMovement.InteractionHandler.ClearInteractTargets(); // 기존 상호작용 타겟 초기화

        playerMovement.ShowPathPreview(newPath);
        InteractionHintUI.Instance?.HideAll();
    }

    public void ClearAllSelection()
    {
        playerMovement.selectedTargetCell = null;
        playerMovement.currentPathCells.Clear();
        playerMovement.ClearPathPreview();
        playerMovement.pathArrivalCallback = null;
        playerMovement.InteractionHandler.ClearInteractTargets();
        InteractionHintUI.Instance?.HideAll();
    }

    //HintUi 공통 취소 메서드
    public void CancelSelectionAndHint()
    {
        playerMovement.selectedTargetCell = null;
        playerMovement.currentPathCells.Clear();
        playerMovement.ClearPathPreview();
        playerMovement.pathArrivalCallback = null;
        playerMovement.InteractionHandler.ClearInteractTargets();
        InteractionHintUI.Instance?.HideAll();
    }
}
