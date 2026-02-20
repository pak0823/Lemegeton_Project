# PushObject & BoxInteract 콜라이더 과잉 판정 해결 보고서

**작성일:** 2026년 2월 21일
**담당:** Antigravity AI Assistant

---

## 1. 작업 개요

### 문제 상황

탐험 씬 내 배치된 `BoxInteract`(보상 아이템 상자) 및 `PushObject`(소코반 상자)의 `PolygonCollider2D` 크기가 타일 1칸을 초과하는 경우, `PathfindingSystem.IsWalkableCell()`의 `OverlapBox` 물리 판정이 인접한 다른 타일까지 이동 불가로 처리하는 버그가 발생하였습니다.

### 선택한 해결 방식

**방법 2: 데이터 기반 타일 점유 캐싱** — 물리 판정에 의존하지 않고, 각 오브젝트가 자신의 타일 좌표를 직접 `PathfindingSystem`에 등록/해제하는 방식

---

## 2. 변경 파일 및 수정 내용

### `PathfindingSystem.cs`

- `HashSet<Vector3Int> occupiedCells` 필드 추가
- `RegisterObstacle(Vector3Int cell)` / `UnregisterObstacle(Vector3Int cell)` 공용 API 추가
- `IsWalkableCell()` 메서드 상단에 `occupiedCells.Contains(cell)` 최우선 검사 추가
- `impassableLayerMask`에서 상자 레이어를 제외해야 한다는 구조적 주석 보강

### `BoxInteract.cs`

- `_currentOccupiedCell` 필드 추가
- `Start()` 시 자신의 タイル 좌표를 `PathfindingSystem`에 등록 (`RegisterObstacle`)
- `ApplyPostOpenBehavior()` (열린 후 상자 제거 처리) 시점에 점유 해제 (`UnregisterObstacle`)
- `OnDestroy()` 시 점유 해제 (안전망)

### `PushObject.cs`

- `_currentOccupiedCell` 필드 추가
- `Start()` 시 자신의 타일 좌표를 `PathfindingSystem`에 등록
- `UpdateObstaclePosition()` 공개 메서드 추가 — 밀기 완료 후 위치 변경 시 점유 좌표를 갱신
- `OnDestroy()` 시 점유 해제 (안전망)

### `PlayerPushHandler.cs`

- `PerformPush()` 코루틴 완료 직후 `box.UpdateObstaclePosition()` 호출 추가 → 상자가 새 위치로 이동한 직후 즉시 점유 갱신

---

## 3. 아키텍처 다이어그램

```
[BoxInteract / PushObject]
    ├── Start()          → PathfindingSystem.RegisterObstacle(cell)
    ├── 이동/열림/제거   → PathfindingSystem.UnregisterObstacle(oldCell)
    │                       PathfindingSystem.RegisterObstacle(newCell)
    └── OnDestroy()      → PathfindingSystem.UnregisterObstacle(cell)

[PathfindingSystem.IsWalkableCell(cell)]
    ├── 0. occupiedCells.Contains(cell) → false (최우선)
    ├── 1. 바닥 타일 체크
    ├── 2. 장애물/벽 타일 체크
    └── 3. OverlapBox 물리 체크 (정적 구조물만)
```

---

## 4. 트러블슈팅 기록

| 증상                                        | 원인                                                              | 해결                                            |
| ------------------------------------------- | ----------------------------------------------------------------- | ----------------------------------------------- |
| `multi_replace_file_content` 치환 실패 반복 | PushObject.cs가 `\r\n\r\n` 형식의 줄 끝을 가지고 있어 매칭 불일치 | 파일 전체를 `write_to_file`로 안전하게 덮어씀   |
| Unity MCP 연결 단절로 컴파일 확인 불가      | 세션 사이 MCP 브릿지 재연결 필요                                  | 유저가 Unity 재시작 후 컴파일 및 기능 검증 완료 |

---

## 5. 인스펙터 설정 안내 (필수)

> [!IMPORTANT]
> Unity Inspector에서 `PathfindingSystem` 컴포넌트의 **Impassable Layer Mask** 값을 확인하세요.
> `PushObject` 및 `BoxInteract`가 사용하는 레이어(예: `Interactable`, `Box` 등)가 이 마스크에 **포함되어 있다면 제거**해야 합니다.
> 이제 이 두 오브젝트는 데이터 기반(`occupiedCells`)으로 판정하므로 물리 레이어가 겹치면 이중 판정이 발생할 수 있습니다.

---

## 6. 결과 및 효과

- 상자의 콜라이더 크기와 무관하게 정확히 **1칸(Hex 타일)** 만 이동 불가 판정
- 보상 상자 열기 완료 후 해당 타일이 **즉시 이동 가능** 상태로 전환
- 소코반 상자 밀기 후 **새 위치만 이동 불가**, 기존 위치는 즉시 해제
- 유지보수성 향상: 새 장애물 오브젝트 추가 시 `RegisterObstacle` 한 줄만 호출하면 시스템에 통합 가능
