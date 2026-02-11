# [과중 상태 이동속도 감소 구현 계획]

**목표**: 과중(Overweight) 상태일 때 플레이어의 이동 속도를 50%로 감소시켜, 디버프 효과를 체감할 수 있도록 합니다.

## User Review Required

> [!NOTE]
> **수치 조정**: 기본 속도의 **0.5배(50%)** 로 설정합니다. 추후 `ExplorationStatusManager`에서 수치를 조정할 수 있습니다.

## Proposed Changes

### 1. ExplorationStatusManager.cs

상태이상에 따른 이동속도 배율을 계산하는 로직을 추가합니다.

#### [MODIFY] [ExplorationStatusManager.cs](file:///c:/Users/ziwoo/Documents/GitHub/Lemegeton_Project/Assets/01_Scripts/Exploration/ExplorationStatusManager.cs)

- **메서드 추가**: `public float GetMoveSpeedMultiplier()`
- **로직**:
  - 활기 소모 배율(`GetVigorCostMultiplier`)과 유사하게 동작.
  - `Overweight` 상태가 있으면 배율 `0.5f` 적용.
  - 추후 `Haste`(가속) 등의 버프가 추가되면 연산(`* 1.5f` 등) 가능하도록 구조 마련.

### 2. PlayerMovement.cs

실제 이동 로직에서 배율을 적용합니다.

#### [MODIFY] [PlayerMovement.cs](file:///c:/Users/ziwoo/Documents/GitHub/Lemegeton_Project/Assets/01_Scripts/Exploration/PlayerMovement.cs)

- **위치**: 이동 코루틴 내부 (경로 이동 루프).
- **변경**:
  - 기존: `float speed = Mathf.Max(0.01f, defaultMoveSpeed);`
  - 변경:
    ```csharp
    float multiplier = ExplorationStatusManager.Instance != null ? ExplorationStatusManager.Instance.GetMoveSpeedMultiplier() : 1f;
    float speed = Mathf.Max(0.01f, defaultMoveSpeed * multiplier);
    ```
  - 이를 통해 이동 중 실시간으로 상태가 변해도 다음 타일 이동 시(또는 매 프레임) 속도가 갱신되도록 합니다. (현재 구조상 타일 단위 이동이므로, 타일 출발 시점 혹은 이동 중에 적용)

## Verification Plan

### Manual Verification

1.  **정상 속도 확인**: 인벤토리 10개 이하일 때 기본 속도 확인.
2.  **감소 속도 확인**: 아이템을 11개 획득하여 과중 상태 진입 후, 이동 속도가 눈에 띄게 느려지는지 확인.
3.  **상태 해제 확인**: 아이템을 버려서 과중 해제 시 속도 복구 확인.
