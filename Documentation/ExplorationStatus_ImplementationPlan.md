# [탐험 상태이상(Exploration Status) 시스템 구축 및 과중 구현 계획]

사용자의 요청에 따라 단순한 '과중' 기능 구현을 넘어, 향후 다양한 버프/디버프(예: 이동 속도 증가, 함정 피해 감소 등)를 확장성 있게 추가할 수 있는 **통합 상태이상 시스템**을 설계하고 구현합니다.

## User Review Required

> [!IMPORTANT]
> **확장성 고려**: 새로운 상태이상이 추가될 때마다 Enum과 데이터(아이콘 등)만 추가하면 자동으로 기능이 연동되도록 설계했습니다.
> **UI 리소스**: 상태별 아이콘을 관리할 `ScriptableObject` 데이터베이스 생성이 필요합니다.

## Proposed Changes

### 1. Core System Architecture

#### [NEW] [ExplorationStatusID.cs] (Enum)

- 탐험 씬에서 사용되는 모든 상태이상의 ID를 정의합니다.
- 예: `None`, `Overweight` (과중), `LightStep` (가벼운 발걸음), `Fatigue` (피로) 등.

#### [NEW] [ExplorationStatusManager.cs] (Singleton)

- **역할**: 탐험 씬의 버프/디버프를 총괄 관리하는 매니저입니다.
- **주요 기능**:
  - `Dictionary<ExplorationStatusID, int> activeStatuses`: 현재 적용 중인 상태와 중첩 수 관리.
  - `AddStatus(id)`, `RemoveStatus(id)`: 상태 부여 및 해제.
  - `GetVigorCostMultiplier()`: 현재 적용된 모든 상태를 순회하여 활기 소모 배율을 계산해 반환 (예: 과중(x2) + 가벼운발걸음(x0.5) = x1.0).
  - `event Action<ExplorationStatusID, bool> OnStatusChanged`: UI 갱신을 위한 이벤트 송출.

#### [MODIFY] [VigorManager.cs](file:///c:/Users/ziwoo/Documents/GitHub/Lemegeton_Project/Assets/01_Scripts/Exploration/VigorManager.cs)

- **역할 변경**: 직접 패널티를 계산하지 않고, `ExplorationStatusManager`에 문의합니다.
- **변경 사항**:
  - `TrySpend()`: `StatusManager.Instance.GetVigorCostMultiplier()`를 호출하여 최종 비용 계산.
  - **과중 트리거 로직**:
    - `InventoryManager`의 이벤트를 구독하여, 아이템 개수가 10개를 초과하면 `StatusManager.AddStatus(Overweight)` 호출, 이하면 `RemoveStatus(Overweight)` 호출.
    - 즉, `VigorManager`는 '과중'이라는 상태의 **조건 감지자(Trigger)** 역할만 수행합니다.

### 2. Data & UI Layer

#### [NEW] [ExplorationStatusDataSO.cs] (ScriptableObject)

- 상태이상 ID 별 메타데이터(이름, 설명, 아이콘 Sprite, 디버프 여부)를 저장합니다.
- 이를 통해 코드 수정 없이 기획 데이터만으로 UI 표현이 가능해집니다.

#### [NEW] [ExplorationStatusUI.cs]

- **역할**: `ExplorationStatusManager`의 이벤트를 받아 화면에 아이콘을 그리거나 지웁니다.
- **기능**:
  - 상태 추가 시: 데이터베이스(SO)에서 아이콘을 찾아 UI 슬롯 생성.
  - 상태 제거 시: 해당 UI 슬롯 파괴 또는 비활성화.
  - 툴팁 기능(선택 사항): 아이콘에 마우스 오버 시 설명 표시.

## Implementation Steps

1.  **기반 시스템 작성**: `ExplorationStatusID`, `ExplorationStatusManager` 작성.
2.  **데이터 구조 작성**: `ExplorationStatusDataSO` 작성.
3.  **로직 연동**: `VigorManager`를 수정하여 인벤토리 감시 및 비용 계산 로직 위임.
4.  **UI 구현**: `ExplorationStatusUI` 작성 및 씬 배치.

## Verification Plan

### Manual Verification

1.  **시스템 확장성 검증**:
    - `Overweight` 외에 가상의 상태(`TestBuff`)를 코드로 강제 추가하여 아이콘이 2개 뜨는지, 비용 계산이 합산되는지 확인.
2.  **과중 기능 검증**:
    - 인벤토리 11개 이상 시 `Overweight` 상태 자동 등록 확인.
    - 활기 소모 시 `GetVigorCostMultiplier`가 2.0을 리턴하는지 로그 확인.
    - 인벤토리 비울 시 상태 해제 확인.
