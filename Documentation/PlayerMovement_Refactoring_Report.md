# PlayerMovement 스크립트 리팩토링 및 분할 보고서

## 1. 개요

본 문서는 Lemegeton 게임 내 탐험 씬의 핵심 코어 스크립트인 `PlayerMovement.cs`의 구조 개선 및 클래스 분할 작업을 기록한 문서입니다.
기존의 `PlayerMovement`는 **약 2,500줄**(최근 1,600여 줄에서 압축 중이었음)에 달하는 거대한 God Object로, 캐릭터의 단순 이동뿐 아니라 NPC 상호작용, 함정 발동, 상자 열기, 소코반 퍼즐(박스 푸시) 등 전혀 다른 성격의 기능들이 하나로 뭉쳐 있었습니다.
이에 **단일 책임 원칙(SRP)**에 입각하여 코어 로직을 3개의 클래스(`PlayerMovement`, `PlayerInteractionHandler`, `PlayerPushHandler`)로 나누었습니다.

---

## 2. 주요 변경 사항 및 분할 아키텍처

### 2.1 `PlayerMovement` (순수 이동 메커니즘 전담)

- **변경 전**: 입력 감지, 맵 콜라이더 감지, 이동 경로 계산, 인카운터 및 퍼즐 푸시에 이르는 거의 모든 동작 트리거.
- **변경 후**:
    - 코어 이동 로직(`Co_MoveAlongPath`, `StartPathMove`, `ProcessMoveClick`)과 애니메이션 조작, 최상위 참조 유지(`Rigidbody`, `Animator` 등) 역할로 한정되었습니다.
    - 상호작용 관련 입력은 `InteractionHandler` 및 `PushHandler`로 중계(라우팅)하는 브로커 역할을 수행합니다.
    - **결과**: 클래스 크기가 1,600여 줄에서 **약 667줄**로 획기적으로 감축되었습니다.

### 2.2 `PlayerInteractionHandler` (신규 생성)

- **책임**: 포탈 탑승(`PortalController`), 상자 및 NPC 상호작용(`IInteractable`), 그리고 조사를 위한 UI 표출 로직 담당.
- **핵심 역할**:
    - `ProcessInteractionClick`: 상호작용 대상 클릭 감지 및 다이얼로그(`DescriptionData`) 연동
    - `TryGetEncounterAtCell`, `TryTriggerTrapAtCell`: 이동 중 이벤트 발생/함정 발동 판단

### 2.3 `PlayerPushHandler` (신규 생성)

- **책임**: 맵 내 밀기 가능한 사물(`PushObject`)을 인식하고 밀기 동작을 수행하는 소코반 기믹 담당.
- **핵심 역할**:
    - `EnterPushSelectMode`, `ProcessPushTargetClick`: 푸시 진입 및 타일러 방식의 선택 모드 지원
    - `Co_MoveToPushReadyAndPush`, `PerformPushToTarget`: 캐릭터를 특정 상자 앞까지 이동시킨 뒤, 방향에 맞춰 밀어내고 애니메이션과 Vigor 차감을 동기화
    - 잦은 콜백 대신 `PlayerMovement`의 `isMovingByPath` 변수 및 길찾기 시스템(Pathfinding)과 상호 연계

### 2.4 의존성 분리 (UI 및 기타 객체)

- `ExplorationInteractionController` 계층에서 입력을 받아들일 때, 예전처럼 PlayerMovement로 일괄 송신하던 것을 `PlayerMovement.PushHandler`와 같은 접근자를 통해 분리 호출하도록 수정.
- `InteractionHintUI` 와 같이 구형 변수를 참조하던 UI 로직들의 경로를 재조정.

---

## 3. 트러블슈팅 및 리스크 관리

1. **상태 머신 미비로 인한 코루틴 꼬임 현상**
    - 상호작용 트리거 후 코루틴(`pathMoveRoutine`)이 겹치면서 오류가 발생하는 이슈가 있었습니다.
    - **해결**: `HandleGlobalClickBlocking` 공통화 및 `CancelSelectionAndHint`를 통한 기존 예약 이동 취소 로직을 엄격하게 관리하여 이중 입력 차단.

2. **접근 제어자 (Public/Private) 문법 충돌**
    - 멤버 변수를 안전하게 제공하기 위해 Getter 등을 구현하는 과정에서 기존 강결합된 하강/퍼즐 로직들이 `Public` 접미사를 호출하려다 CS1061, CS0246 문법 에러가 대거 발생.
    - **해결**: 코드 전체 그렙 매칭(Grep)을 통해 잘못 변경된 레퍼런스를 원상복구하고, `PushHandler` 쪽에 완전 이관.

---

## 4. 진행 결론

결과적으로 거대했던 하나의 스크립트를 도메인 성격에 맞는 3개의 핸들러로 잘라내어 **추후 멀티플레이어 고려 시의 확장성**과, **유지보수 가독성**을 매우 높일 수 있었습니다.
유니티 에디터 인게임 테스트 결과 이동, 뷰포인트 상호작용, 푸시박스 3개의 기믹이 모두 병행되어 안정적으로 동작함을 확인하였습니다.
