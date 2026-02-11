# Lemegeton: 전투 시스템 아키텍처 분석 및 실무 적용 제안서

**작성일시**: 2026-02-07
**참조**: `BattleManager.cs`, `BattleUnit.cs`, `EnemyAI.cs`
**목적**: 현재 전투 시스템의 코드 구조를 진단하고, 상용급(Production-Ready) 프로젝트로 발전시키기 위한 **실무적인 개선안(Best Practices)**을 제시함. (최근 탐험 모듈 안정화 이후 차기 중점 과제)

---

## 1. 현황 분석 (Current Status Analysis)

### 1.1 구조적 강점 (Strengths)

1.  **Mediator 패턴 활용**: `BattleManager`가 `GridManager`, `TurnManager` 등 하위 모듈을 중재하는 중앙 집중형 설계를 취하고 있어 의존성 관리가 비교적 명확합니다.
2.  **데이터 주도 설계 (Data-Driven)**: `SkillAsset`, `UnitData` 등 ScriptableObject를 적극 활용하여 로직과 데이터를 분리했습니다.
3.  **컴포넌트 분리 시도**: `EnemyAI`, `StatusController` 등 일부 기능이 별도 컴포넌트로 분리되어 있습니다.

### 1.2 구조적 취약점 & 리스크 (Weaknesses)

1.  **거대 클래스 (God Class) 문제 - `BattleUnit.cs`**
    - **현상**: 약 1,000줄에 달하는 코드가 스탯 계산, 이동(Move), 애니메이션, ATB 연산, 리소스(HP/MP) 관리를 모두 처리하고 있습니다.
    - **리스크**: 수정 시 사이드 이펙트 발생 확률이 매우 높으며, 유닛의 행동을 확장하기 어렵습니다. (예: "비행 유닛" 추가 시 대대적 수정 필요)
2.  **Enum 기반의 상태 관리 - `BattleState`**
    - **현상**: `BattleManager`에서 `switch(state)` 문으로 흐름을 제어합니다.
    - **리스크**: 전투 흐름이 복잡해질수록(예: 컷신, 튜토리얼 개입, 네트워크 대기) `switch` 문이 비대해지고 상태 전이 로직이 꼬이기 쉽습니다.
3.  **명령(Command) 패턴의 부재**
    - **현상**: 스킬 사용과 이동 로직이 즉시 실행(`Direct Call`)됩니다.
    - **리스크**: "행동 취소(Undo)", "리플레이(Replay)", "스킬 큐(Queue)" 기능을 구현하기 불가능한 구조입니다.

---

## 2. 실무 적용을 위한 핵심 기술 및 패턴 (Required Technologies & Patterns)

상용 수준의 턴제 전술 게임(SRPG)을 위해 다음 4가지 핵심 패턴 도입을 제안합니다.

### 2.1 Finite State Machine (FSM) - 전투 흐름 제어

단순 `Enum` 대신 **클래스 기반의 상태 패턴**을 사용하여 전투의 단계별 로직을 격리해야 합니다.

- **구조**: `BattleStateMachine` (Context) ↔ `BattleState` (Abstract Class)
- **상태 예시**:
  - `SetupState`: 초기 배치 및 카메라 연출
  - `PlayerTurnState`: 입력 대기 및 유닛 선택
  - `ActionExecutionState`: 스킬/이동 애니메이션 재생 (입력 차단)
  - `EnemyTurnState`: AI 연산 및 실행
  - `ResultState`: 보상 및 결과창

### 2.2 Command Pattern - 행동의 캡슐화

유닛의 모든 행동(이동, 공격, 대기)을 객체화하여 관리합니다. 이를 통해 **비동기 실행**과 **실행 취소**를 지원합니다.

- **ActionCmd (Abstract)**: `Execute()`, `Undo()`
  - `MoveCommand`: 좌표 A -> B 이동
  - `SkillCommand`: 스킬 시전 및 데미지 처리
- **CommandQueue**: 실행할 명령을 쌓아두고 순차적으로 처리 (비동기 애니메이션 대기 용이).

### 2.3 Event Bus / Message Broker - 결합도 감소

`BattleUnit`이 UI(`UnitStatusPanelUI`)를 직접 참조하거나 이벤트를 1:1로 연결하는 대신, 중앙 버스를 통해 메시지를 발행합니다.

- **방식**: `EventBus.Publish(new DamageEvent(target, amount))`
- **효과**: UI 시스템이 게임 로직을 전혀 몰라도(No Reference) 돌아가는 완전한 분리 가능.

### 2.4 UniTask (Async/Await) - 코루틴 대체

복잡한 연출(이동 후 -> 이펙트 -> 데미지 -> UI 갱신) 시퀀스를 `Coroutine`보다 가독성이 뛰어난 `async/await`로 처리합니다.

---

## 3. 제안 아키텍처 (Proposed Architecture)

### 3.1 BattleUnit 리팩토링 (컴포넌트 기반)

`BattleUnit`은 껍데기(Facade) 역할만 하고, 실제 기능을 하위 컴포넌트로 위임합니다.

```mermaidx
classDiagram
    class BattleUnit {
        +Stats: UnitStats
        +Mover: UnitMover
        +Visual: UnitVisual
    }
    class UnitStats {
        +HP, MP, STR...
        +CalculateMultipliers()
    }
    class UnitMover {
        +MoveTo(Vector3Int)
        +GridPosition
    }
    class UnitVisual {
        +PlayAnim()
        +SpawnVFX()
    }

    BattleUnit --> UnitStats
    BattleUnit --> UnitMover
    BattleUnit --> UnitVisual
```

### 3.2 전투 루프 (Flow) 개선안

1.  **Turn Start**: `TurnManager`가 다음 순서 유닛 결정 -> `BattleManager`에 통보.
2.  **State Transition**: `BattleStateMachine`이 `PlayerInputState`로 전환.
3.  **Command Generation**: 플레이어 입력(클릭) -> `MoveCommand` 생성 -> `CommandQueue`에 등록.
4.  **Execution**: `CommandQueue`가 `ActionExecutionState`로 전환 후 명령 실행.
    - `UnitMover`가 이동 처리 (Async)
    - 이동 완료 후 `PlayerInputState` 복귀 (또는 턴 종료)

---

## 4. 단계별 적용 로드맵 (Action Plan)

### 1단계: 기반 마련 (Foundation)

- [ ] **UniTask 도입**: 비동기 처리를 위한 라이브러리 설치/설정.
- [ ] **FSM 구조 구현**: `BattleState` 기본 클래스 및 `StateMachine` 작성.

### 2단계: 유닛 구조 개선 (Refactoring)

- [ ] `BattleUnit.cs`에서 **이동 로직**을 `UnitMover.cs`로 분리.
- [ ] `BattleUnit.cs`에서 **스탯 연산**을 `UnitStats.cs`로 분리.
- [ ] `BattleUnit`은 이들을 연결해주는 중계자 역할로 축소.

### 3단계: 커맨드 패턴 적용 (Command)

- [ ] 이동, 스킬 사용을 `ICommand` 인터페이스로 래핑.
- [ ] `BattleManager`의 `Direct Call`을 `CommandQueue.Enqueue()`로 변경.

### 4단계: UI 완전 분리 (Decoupling)

- [ ] `EventBus` 시스템 도입.
- [ ] `BattleUnit` 내부의 `OnDamaged`, `OnHealed` C# 이벤트를 `EventBus` 발행으로 교체.

---

## 5. 결론 (Conclusion)

현재 Lemegeton 프로젝트는 기능적으로는 완성도가 높으나, **확장성 및 유지보수성** 측면에서 리팩토링이 필요한 시점입니다.
특히 **FSM 도입**과 **유닛 클래스 분할(SRP 준수)**은 장기적인 프로젝트 성공을 위해 "선택이 아닌 필수" 과제입니다.
위 제안된 구조를 적용한다면, 다가올 컨텐츠(복잡한 스킬, 다양한 승리 조건, 튜토리얼) 구현 속도가 획기적으로 빨라질 것입니다.
