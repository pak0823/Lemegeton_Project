# 드래그 앤 드롭 시각 효과(Ghost Image) 구현 결과 보고서

## 1. 개요

프로젝트 내 `Inventory`(탐험 씬)와 `CampUi`(캠프 씬)에서 드래그 앤 드롭 시, 아이템/캐릭터의 원본이 이동하는 대신 **Ghost Image(반투명 복제 이미지)**가 마우스를 따라다니도록 기능을 개선했습니다. 이를 통해 UI 레이아웃이 깨지는 현상을 방지하고, 더 직관적인 사용자 경험을 제공합니다.

## 2. 변경 사항 상세

### 2.1. 탐험 씬 (Exploration Scene)

#### `InventoryUI.cs`

- **Ghost Image 기능 추가**:
  - `dragGhostImage` (Image) 변수 추가.
  - `StartDrag(Sprite)`, `UpdateDrag(Vector2)`, `EndDrag()` 메서드를 통해 고스트 이미지의 활성화/이동/비활성화를 중앙에서 관리하도록 구현.
  - `SetAsLastSibling()`을 사용하여 드래그 이미지가 항상 최상위에 표시되도록 설정.

#### `InventoryDragHandler.cs`

- **로직 변경**:
  - 기존: `transform.position`을 직접 변경하여 슬롯 자체를 이동
  - **변경**:
    - `OnBeginDrag`: `InventoryUI.StartDrag`를 호출하여 고스트 이미지 표시. 본체(`canvasGroup`)는 `alpha = 0.5f`로 반투명 처리.
    - `OnDrag`: `InventoryUI.UpdateDrag`를 호출하여 고스트 이미지만 이동.
    - `OnEndDrag`: `InventoryUI.EndDrag`로 고스트 이미지 숨김. 본체 투명도 원상 복구.

### 2.2. 캠프 씬 (Camp Scene)

#### `CampUIManager.cs`

- **안전 장치 추가**:
  - `StartDrag` 메서드에 `dragGhostImage.raycastTarget = false;` 코드를 추가하여, 고스트 이미지가 드롭 이벤트(레이캐스트)를 가로막지 않도록 보완.

#### `FormationSlotUI.cs`

- **시각 효과 통일**:
  - 드래그 시 원본 이미지의 투명도를 인벤토리와 동일하게 `0.5f`로 설정하여 통일감 부여.

## 3. 트러블 슈팅 (Troubleshooting)

### 3.1. 파일 수정 미반영 문제

- **현상**: `InventoryDragHandler.cs`의 코드를 부분 수정(`replace_file_content`)하려 했으나, 문맥(Context) 불일치로 인해 수정이 제대로 적용되지 않는 현상 발생.
- **원인**: 여러 번의 수정 시도 과정에서 파일의 줄 바꿈이나 공백 등이 예상과 달라져, AI가 기존 코드를 찾지 못함.
- **해결**: 파일 전체를 올바른 코드로 덮어쓰는(`write_to_file` + Overwrite) 방식을 사용하여 확실하게 수정 사항을 반영함.

### 3.2. Raycast 차단 문제

- **현상**: 고스트 이미지가 마우스를 따라다닐 때, 드롭 지점(Slot)을 가려서 `OnDrop` 이벤트가 발생하지 않을 가능성 확인.
- **해결**: 모든 `StartDrag` 로직에서 `dragGhostImage.raycastTarget = false`를 강제로 설정하도록 코드를 추가하여 근본적인 원인 차단.

## 4. 결론

인벤토리와 캠프 상태창 모두에서 안정적이고 일관된 드래그 앤 드롭 시각 효과가 구현되었습니다. 향후 다른 UI에서 유사한 기능이 필요할 경우, `InventoryUI`나 `CampUIManager`에 구현된 `StartDrag/UpdateDrag/EndDrag` 패턴을 참고하여 확장할 수 있습니다.
