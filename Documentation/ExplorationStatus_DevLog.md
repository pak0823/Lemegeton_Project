# 개발 로그: 탐험 상태이상 시스템 (Exploration Status System)

## 개요

기존의 단순한 '과중(Overweight)' 기능 요구사항을 확장 하여, 향후 다양한 버프/디버프를 추가하기 용이한 **통합 상태이상 시스템**으로 구현하였습니다.

## 구현 상세

### 1. 아키텍처

- **Centralized Manager**: `ExplorationStatusManager`가 모든 상태이상(Status)의 라이프사이클과 효과 계산을 중앙 관리합니다.
- **Event-Driven UI**: 상태 변경 시 이벤트(`OnStatusChanged`)를 발생시켜 UI가 능동적으로 반응하도록 하여, 매니저와 뷰의 결합도를 낮췄습니다.
- **Data-Driven Design**: `ExplorationStatusDataSO`를 통해 코드를 수정하지 않고도 새로운 상태이상의 아이콘과 설명을 추가할 수 있습니다.

### 2. 수정된 파일

- `ExplorationStatusID.cs` (New): 상태 식별자 Enum.
- `ExplorationStatusManager.cs` (New): 핵심 로직 매니저.
- `ExplorationStatusDataSO.cs` (New): 데이터 컨테이너.
- `ExplorationStatusUI.cs` (New): UI 표현 계층.
- `VigorManager.cs` (Modified):
  - 인벤토리 감지 로직 추가.
  - 직접 계산하던 로직을 `StatusManager.GetVigorCostMultiplier()` 호출로 변경.

## 트러블 슈팅 (Troubleshooting)

### Q1. 아이템이 10개가 넘었는데 아이콘이 안 나옵니다.

- **체크 1**: 씬에 `ExplorationStatusManager`가 있는 오브젝트가 존재하는지 확인하십시오.
- **체크 2**: `ExplorationStatusUI` 컴포넌트의 `Data DB`에 `Overweight` ID를 가진 데이터가 등록되어 있는지 확인하십시오. ID가 일치하지 않으면 아이콘이 생성되지 않습니다.

### Q2. 활기 소모량이 변하지 않습니다.

- **체크 1**: `VigorManager`가 씬에 로드되어 있고 (`Instance` 정상), `Start()`에서 `OnInventoryChanged`를 정상적으로 구독했는지 확인하십시오.
- **체크 2**: `Start()` 시점에 `InventoryManager`가 초기화되어 있어야 합니다. Script Execution Order 문제라면 `VigorManager`를 조금 늦게 초기화되도록 설정해보세요.

### Q3. 새로운 버프를 추가하고 싶습니다.

1. `ExplorationStatusID` Enum에 새 이름을 추가합니다 (예: `Haste`).
2. 스크립트(`VigorManager` 등)에서 조건에 따라 `ExplorationStatusManager.Instance.AddStatus(ExplorationStatusID.Haste)`를 호출합니다.
3. `ExplorationStatusData` 에셋에 `Haste` 항목을 추가하고 아이콘을 설정합니다.
4. 효과 구현: 효과가 필요한 곳(예: 이동속도)에서 `ExplorationStatusManager.Instance.HasStatus(...)` 등을 체크하여 로직을 작성합니다.

## 추가 구현: 과중 시 이동속도 감소 (v1.1)

- **기능**: 과중 상태(`Overweight`)일 때 플레이어의 이동 속도가 50%로 감소합니다.
- **구현**:
  - `ExplorationStatusManager`에 `GetMoveSpeedMultiplier()` 추가.
  - `PlayerMovement`의 이동 코루틴에서 해당 배율을 곱하여 속도(`speed`) 결정.
