# Part 1: 전투 시스템 (Battle System) — 상세 분석

**분류:** 파트1 — 전투 시스템
**작성일:** 2026-03-04
**참조 파일:** `Assets/01_Scripts/Battle/` 전체
**관련 문서:** Research.md §5, Lemegeton_Battle_Architecture_Report.md

---

## 목차

1. [전투 아키텍처 전체 구조](#1-전투-아키텍처-전체-구조)
2. [헥스 그리드 시스템](#2-헥스-그리드-시스템)
3. [ATB 턴제 시스템](#3-atb-턴제-시스템)
4. [BattleState FSM (유한 상태 기계)](#4-battlestate-fsm-유한-상태-기계)
5. [전투 서브시스템 7종](#5-전투-서브시스템-7종)
6. [BattleUnit 구조](#6-battleunit-구조)
7. [상태이상 시스템 (2계층)](#7-상태이상-시스템-2계층)
8. [스킬 데미지 계산 흐름](#8-스킬-데미지-계산-흐름)
9. [적 AI 시스템](#9-적-ai-시스템)
10. [Command 패턴 (커맨드 큐)](#10-command-패턴-커맨드-큐)
11. [스킬 에셋 계층 구조](#11-스킬-에셋-계층-구조)
12. [보상 시스템](#12-보상-시스템)
13. [현재 완성도 및 미완성 항목](#13-현재-완성도-및-미완성-항목)

---

## 1. 전투 아키텍처 전체 구조

`Battle/` 폴더는 프로젝트에서 가장 크고 복잡한 영역으로, 9개의 하위 폴더로 체계적으로 세분화되어 있다.

```
Battle/
├── Command/      CommandQueue, ICommand, MoveCommand, SkillCommand (4파일)
├── Core/         전투 핵심 매니저 16개+
├── FSM/          BattleStateMachine, BattleBaseState, BattleConcreteStates (3파일)
├── Projectiles/  ProjectileController
├── Skills/
│   ├── Core/     SkillAsset(abstract), EnemySkill(abstract)
│   ├── Effects/  SmokeZoneRuntime
│   ├── Enemies/  EnemyBasicAttack, WebCastWebTrap
│   └── Player/
│       ├── Core/      StateConditionalMulti
│       ├── Passive/   캐릭터별 패시브 12종
│       ├── Self/      SelfAmbushSkill, SelfVigilanceSkill 등 6종
│       ├── Tactics/   AllyRetreatSwapSkill 등 전술 스킬
│       └── Templates/ ParametricDamageSkill 등 파라미터화 템플릿
├── Units/        BattleUnit, EnemyAI, StatusController, UnitStateController 등
│   └── Components/ UnitStats, UnitMover, UnitVisual (분리 완료)
├── Utils/        HexUtil 등
├── Visuals/      시각 효과 컨트롤러
└── Waves/        웨이브 관련
```

**BattleManager** 가 최상위 오케스트레이터 역할을 하며, 초기화 순서와 이벤트 라우팅을 전담한다.

---

## 2. 헥스 그리드 시스템

### 그리드 구조

오프셋 헥스 그리드(Offset Hex Grid)를 사용한다. `HexUtil.cs`에 축 좌표(Axial) 변환 로직이 구현되어 있으며, 짝수/홀수 열에 따른 인접 셀 계산이 `IsOddColumn()` 헬퍼로 처리된다.

- **플레이어 팀**: 별도 Tilemap(`PlayerFloor`)
- **적 팀**: 별도 Tilemap(`EnemyFloor`)
- **점유 관리**: `BattleGridManager`가 `HashSet<Vector3Int>` 기반으로 양쪽 팀 독립 관리

### 헥스 6방향 오프셋

```
짝수 열(x%2==0): NW(-1,1), NE(0,1), W(-1,0), E(1,0), SW(-1,-1), SE(0,-1)
홀수 열(x%2==1): NW(0,1),  NE(1,1), W(-1,0), E(1,0), SW(0,-1),  SE(1,-1)
```

### BattleGridManager 주요 기능

| 메서드               | 설명                        |
| -------------------- | --------------------------- |
| `IsOccupied(cell)`   | HashSet 기반 O(1) 점유 검사 |
| `GetWalkableCells()` | 이동 가능 셀 목록 반환      |
| `GetUnitsInRange()`  | 범위 내 유닛 목록 조회      |
| `RegisterUnit()`     | 유닛 점유 등록              |
| `UnregisterUnit()`   | 유닛 점유 해제              |

---

## 3. ATB 턴제 시스템

### ATB 충전 흐름

```
[ATBTurnController] — Update()마다 모든 유닛 ATB 충전
    ↓ unit.atbPerSecond = AGI × speedMultiplier (클램프: 1~10000)
    ↓ ATB >= MaxATB(100f)이면 턴 준비 완료
    ↓ 우선순위: Overfill(초과분) → AGI → 랜덤
    ↓ PauseTime() 후 OnTurnReady 이벤트 발송
[BattleManager.HandleTurnReady]
    ↓ BattleTurnManager.StartTurn(unit) 호출
[BattleTurnManager]
    ↓ 상태이상 처리 (수면→스킵, 공포→강제후퇴)
    ↓ 플레이어: ActionSelect 상태
    ↓ 적: EnemyTurnRoutine 시작
턴 종료: ATBTurnController.CompleteTurn() → ATB 리셋, 쿨다운 감소, ResumeTime()
```

### 과로(Overwork) 시스템

`BattleTurnManager`가 행동력 소비를 관리한다. 연속 행동 시 과로 상태이상이 쌓이며, 이는 다음 턴 스탯에 영향을 준다.

### GameSpeedController

`GameSpeedController.cs`가 `Time.timeScale`을 조작하여 게임 속도를 제어한다. ATB 충전 시 `PauseTime()` / `ResumeTime()` 쌍으로 전투를 일시정지/재개한다.

---

## 4. BattleState FSM (유한 상태 기계)

### FSM 구조

`BattleStateMachine`(Context) ↔ `BattleBaseState`(Abstract) ↔ `BattleConcreteStates`(구체 상태들)

```
Idle
  └─ 턴 준비 완료 → ActionSelect
       ├─ 이동 선택 → Moving → [타일 클릭] → Resolving → ActionSelect/EndTurn
       ├─ 스킬 선택 → Targeting → [타겟 확정] → Resolving → FinishActionAfterSkill()
       │                       └─ 넉백 선택 → TargetingKnockback → Resolving
       └─ 휴식/진정 → EndTurn → Idle
```

### FSM 파일 구성

| 파일                      | 역할                                            |
| ------------------------- | ----------------------------------------------- |
| `BattleStateMachine.cs`   | Context 클래스, 상태 전환 관리                  |
| `BattleBaseState.cs`      | 추상 기반 상태 클래스 (`Enter`, `Tick`, `Exit`) |
| `BattleConcreteStates.cs` | 모든 구체 상태 구현 (UniTask 기반)              |

> **구현 참고:** FSM 방식(`BattleStateMachine`)이 `BattleEnums.cs`의 `BattleState` enum 기반 레거시 방식을 완전히 대체하여 통합 완료되었다.

---

## 5. 전투 서브시스템 7종

`BattleManager`가 최상위에서 7개 서브시스템을 오케스트레이션한다.

| 서브시스템             | 책임                                                          |
| ---------------------- | ------------------------------------------------------------- |
| `BattleGridManager`    | 헥스 그리드 점유 조회, 이동 가능 셀 계산, 범위 내 유닛 목록   |
| `BattleFieldManager`   | 타일 변경 장판(SmokeZone 등), 환경 효과, BeastDomain 프리무브 |
| `BattleTurnManager`    | 턴 생명주기 관리, 행동력 소비, 과로(Overwork) 처리            |
| `BattleInputHandler`   | 클릭/탭 입력, 이동/스킬 하이라이팅, 타겟 사이클링             |
| `BattleMapManager`     | 웨이브별 적 진영 Tilemap 로드/교체                            |
| `BattleSkillProcessor` | 스킬 실행 흐름, 데미지 계산, 넉백 처리                        |
| `BattleWaveManager`    | 웨이브 스폰, 다음 웨이브 전환, 전체 클리어 감지               |

### BattleRewardManager

전투 승리 보상 생성을 담당. `WaveSet.RewardProfile`에 따라 아이템 풀(Material + Consumable)에서 랜덤 보상을 생성(3~5종)하여 `SceneTransitionManager.SetPendingRewards()`에 저장한다.

---

## 6. BattleUnit 구조

### 7개 Region 분리

```csharp
public class BattleUnit : MonoBehaviour
{
    // 1. Core Data & Configuration   — UnitData SO 참조
    // 2. Dependencies                — BattleManager, Controller 캐시
    // 3. Runtime Status              — HP/MP/Rage, 현재 위치
    // 4. ATB System                  — ATB 게이지, maxATB, Overfill
    // 5. Visuals & Animation         — Animator, SpriteRenderer, 콜백
    // 6. Internal Logic & Cache      — 패시브 목록, 스탯 캐시
    // 7. Events                      — OnDamaged, OnDied 이벤트
}
```

### 컴포넌트 분리 (Units/Components/ — 완료)

| 컴포넌트     | 역할                          |
| ------------ | ----------------------------- |
| `UnitStats`  | 스탯 계산, 상태이상 배율 적용 |
| `UnitMover`  | 그리드 이동, 위치 관리        |
| `UnitVisual` | 애니메이션 재생, VFX 스폰     |

### 6대 기본 스탯

| 스탯 | 약어  | 역할                     |
| ---- | ----- | ------------------------ |
| 근력 | `STR` | 물리 공격력              |
| 총명 | `CLV` | 마법 공격력              |
| 민첩 | `AGI` | ATB 충전 속도, 탈출 확률 |
| 신체 | `BDY` | 최대 HP (BDY×3 + STR)    |
| 정신 | `MND` | 마법 방어                |
| 통찰 | `INS` | 크리티컬/특수 판정       |

### 유닛 레지스트리

`BattleManager._activeUnits: HashSet<BattleUnit>`으로 씬 내 모든 유닛을 중앙 관리한다. `FindObjectsOfType<BattleUnit>()` 호출 대신 이 레지스트리를 사용하여 O(1) 조회가 가능하다 (완전 교체 완료).

---

## 7. 상태이상 시스템 (2계층)

### StatusController — 스택 기반 수치 상태

```
StatusId 범위:
  1~20   — 스킬 중첩 (Shooting/럭키식스, Action/기간트, Overwork/라스트보르그, Research 등)
  21~40  — 피해 보정 (Defense, Resistance, Weakness, Exhaustion, Slow, Suppression)
  50~55  — 지속 피해 (Bleeding 2%/스택, Poisoning 3%/스택, Ignition 3%/스택)

DebuffTuning.Mult: [1.0, 0.8, 0.6, 0.4, 0.3, 0.2, 0.1] (6스택 최대)
```

### UnitStateController — 불리언 성격의 특수 상태

```
UnitStateId:
  Sleep(수면→턴스킵)
  Fear(공포→강제후퇴)
  Ambush(매복→은신+힐)
  Vigilance(경계→반격)
  Isolation(고립)

UnitStateBuffId:
  SmokeHidden(연막속)
  BeastDomain(야수영역)
```

---

## 8. 스킬 데미지 계산 흐름

`BattleSkillProcessor.GetFinalSkillDamage()`의 계산 순서:

```
baseDamage (스킬 power × 시전자 스탯)
    × FrontlineBonus (전방 보너스, ParametricDamageSkill.UseFrontlineBonus 시)
    × StateStatDB 피해배율 (StateStatModifierDB, 상태별 보정)
    × Physical 학교 시: Exhaustion 스택×1.2, Defense 스택×0.8
    × Magical 학교 시: Weakness 스택×1.2, Resistance 스택×0.8
    × Rage 보정: 1.0 + 0.01 × Rage
    → FloorToInt → 최종 피해량
```

---

## 9. 적 AI 시스템

### AI 계층

| 클래스            | 용도                                                  |
| ----------------- | ----------------------------------------------------- |
| `EnemyAI`         | 기본 AI. 가중치 랜덤 스킬 선택 (WeightedSO 배열 기반) |
| `AnalysisEnemyAI` | 분석형 AI. 플레이어 상태 분석 후 최적 스킬 선택       |
| `BossEnemyAI`     | 보스 AI. 웨이브/체력 조건별 고정 패턴 실행            |

### 타겟 선정 우선순위

`HighestHostility`(적대감 가중치 랜덤, 기본) / `LowestHP`(막타) / `Random` / `Closest`

### EnemyCastState — 사전 시전 시스템

거미 몬스터(`WebCastWebTrap`)처럼 "이번 턴 준비, 다음 턴 발사" 패턴을 구현한다.

- `PendingCast` 구조체에 투사체 프리팹, 목표 셀, 덫 프리팹 등 저장
- 다음 턴 시작 시 `TryTakeReady()`로 꺼내 `Co_EnemyFireWebThenConsume()`으로 실행

---

## 10. Command 패턴 (커맨드 큐)

### 구조

```csharp
public interface ICommand
{
    UniTask Execute();
    UniTask Undo();  // 미래 롤백 지원을 위한 설계
}

// 구체 구현
MoveCommand  : ICommand  // 이동 캡슐화
SkillCommand : ICommand  // 스킬 실행 캡슐화
```

### CommandQueue

`CommandQueue.cs`가 명령을 큐에 쌓고 순차 실행한다. 비동기 애니메이션 대기가 자연스럽게 처리된다.

---

## 11. 스킬 에셋 계층 구조

### 기본 구조

```csharp
public abstract class SkillAsset : ScriptableObject
{
    string id, displayName;
    DamageSchool school;    // Physical / Magical
    AttackAttr attribute;   // 속성
    float power;            // 기본 배수

    SkillCostResource costResource; // MP / Rage
    int cost;
    int cooldownTurns;

    SkillTargetMode targetMode;         // Unit / Tile
    SkillTargetAlignment targetAlignment; // Enemy / Ally / Self / Any
    SkillAnimKind animKind;  // Melee / Ranged / Casting
    bool useGapCloseJump;    // 갭 클로즈 여부
    TrainingRouteInfo[] trainingRoutes; // 훈련 루트 3개
}
```

### 스킬 템플릿 계층

| 클래스                     | 용도                                                     |
| -------------------------- | -------------------------------------------------------- |
| `ParametricDamageSkill`    | 데미지 스킬 (범위, 투사체, 넉백, 상태부여 등 파라미터화) |
| `ParametricHealSkill`      | 치유 스킬                                                |
| `ParametricSupportSkill`   | 지원 스킬 (부활 포함)                                    |
| `ParametricDirectionSkill` | 방향 지정 스킬                                           |
| `SelfStateSkill`           | 자기 자신에게 상태 부여                                  |
| `SelfStateCleanseSkill`    | 자기 상태 해제                                           |
| `SelfAmbushSkill`          | 매복 상태 진입                                           |
| `SelfVigilanceSkill`       | 경계 상태 진입                                           |
| `SelfBeastDomainSkill`     | 야수 영역 발동                                           |
| `SelfIsolationTimedSkill`  | 시간제 고립 상태                                         |
| `SmokeBombSkill`           | 연막 장판 설치                                           |
| `FearOnBleedSkill`         | 출혈 대상에 공포 부여                                    |
| `HostilitySpikeSkill`      | 적의 증가                                                |
| `AllyRetreatSwapSkill`     | 아군 후퇴 교체 (전술)                                    |
| `StateConditionalMulti`    | 상태 조건부 복합 스킬                                    |

### ISkillForStateResolver 인터페이스

캐릭터 상태에 따라 다른 스킬 에셋을 반환하는 전략 패턴. 예: 매복 상태일 때 강화된 스킬로 교체.

```csharp
public interface ISkillForStateResolver
{
    SkillAsset ResolveForCaster(BattleUnit caster);
}
```

### ParametricDamageSkill 주요 파라미터

- `AreaPreset`: Single, Line, Cone, Adjacent 등 범위 프리셋
- `applyStatusOnHit`: 적중 시 부여할 상태이상 목록 (StatusEffectInfo 배열)
- `changeTileTo`: 적중 타일 변경 (지속 장판)
- `projectilePrefab / projectileSpeed`: 투사체 설정
- `conditionalMultipliers`: 특정 상태이상 보유 시 추가 배율
- `UseFrontlineBonus`: 전방 유닛 추가 피해
- `trainingUseAreaOverride`, `trainingUsePostMove` 등: 훈련 루트별 변형

---

## 12. 보상 시스템

전투 승리 시 `BattleRewardManager.GenerateRewards(profile)` 호출:

1. `WaveSet.RewardProfile`에 따라 아이템 풀에서 랜덤 보상 생성 (3~5종)
2. `SceneTransitionManager.SetPendingRewards()`에 저장
3. 탐험씬 복귀 후 `RewardPopupUI`를 통해 플레이어에게 지급

---

## 13. 현재 완성도 및 미완성 항목

### 완성된 항목

- [x] ATB 턴 시스템 (AGI 기반 우선순위)
- [x] BattleState FSM (레거시 enum 방식과 통합 완료)
- [x] 7개 서브시스템 분리 (BattleGridManager, BattleFieldManager 등)
- [x] 상태이상 2계층 (StatusController + UnitStateController)
- [x] 스킬 데미지 계산 공식
- [x] EnemyAI 3계층 (기본/분석/보스)
- [x] Command 패턴 (CommandQueue, ICommand)
- [x] BattleUnit 컴포넌트 분리 (UnitStats, UnitMover, UnitVisual)
- [x] FindObjectsOfType 제거 (레지스트리 방식으로 완전 교체)
- [x] async void → async UniTaskVoid 전환 완료
- [x] UniTask 기반 비동기 통일 완료

### 미완성 항목

- [ ] `LuckySixShootingInsightPassive.cs` — TODO 주석 3개, 실제 스탯 연동 미구현
- [ ] `GigantEndTurnRegenPassive.cs` — 테스트용 강제 진행도 반환 코드 잔존
- [ ] 단위 테스트 전무 (데미지 계산, ATB 순서, 보상 확률)
- [ ] 과도한 싱글톤 (BattleManager 포함 26개+) — 장기 개선 과제

---

_Part 1 Battle System Detail — Lemegeton Project Documentation — 2026-03-04_
