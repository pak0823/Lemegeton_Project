# Lemegeton Project — 심층 분석 보고서 (Research.md)

**작성일:** 2026-02-27  
**분석 대상:** Lemegeton_Project-Dev (Unity 2D RPG, 개발 중)  
**스크립트 수:** 약 150+ `.cs` 파일  
**엔진:** Unity URP 14.0.12 / 2D Tilemap

---

## 목차

1. [프로젝트 전체 개요](#1-프로젝트-전체-개요)
2. [기술 스택 및 패키지](#2-기술-스택-및-패키지)
3. [폴더 구조 및 레이어 분리](#3-폴더-구조-및-레이어-분리)
4. [씬 구성 및 게임 플로우](#4-씬-구성-및-게임-플로우)
5. [전투 시스템 (Battle System) 상세 분석](#5-전투-시스템-battle-system-상세-분석)
6. [탐험 시스템 (Exploration System) 상세 분석](#6-탐험-시스템-exploration-system-상세-분석)
   - 6.1 MapManager 서브시스템 구조
   - 6.2 PlayerMovement
   - 6.3 활기(Vigor) 시스템
   - 6.4 안개(Fog) 시스템
   - 6.5 탐험 영속성 (Persistence)
   - 6.6 상호작용 오브젝트 분류
   - **6.7 PathfindingSystem 상세 구조 (BFS, 다층 맵, 높이 판정)**
   - 6.8 QTE 시스템
   - 6.9 탐험 상태 (ExplorationStatus)
7. [캐릭터 데이터 및 성장 시스템](#7-캐릭터-데이터-및-성장-시스템)
8. [인벤토리 및 아이템 시스템](#8-인벤토리-및-아이템-시스템)
9. [스킬 시스템 구조](#9-스킬-시스템-구조)
10. [캠프 UI 시스템](#10-캠프-ui-시스템)
11. [씬 전환 및 데이터 지속성](#11-씬-전환-및-데이터-지속성)
12. [코어 인프라 시스템](#12-코어-인프라-시스템)
13. [에디터 도구 및 데이터 파이프라인](#13-에디터-도구-및-데이터-파이프라인)
14. [현재 개발 상태 및 미완성 영역](#14-현재-개발-상태-및-미완성-영역)
15. [발견된 기술적 부채 및 개선 과제](#15-발견된-기술적-부채-및-개선-과제)
16. [종합 평가 및 액션 플랜](#16-종합-평가-및-액션-플랜)

---

## 1. 프로젝트 전체 개요

Lemegeton Project는 Unity 2D Tilemap 기반의 **탐험-전투 하이브리드 전술 RPG**다. 게임의 핵심 루프는 다음과 같다.

**메인 루프:** 탐험(Exploration) → 조우(Encounter) → 전투(Battle) → 복귀(Return) → 반복

프로젝트명 "Lemegeton"은 17세기 유럽 마법서(솔로몬의 소열쇠)에서 유래한 것으로, 다크 판타지 세계관을 암시한다. 플레이어는 자원(활기, Vigor)을 소모하며 타일맵을 탐험하고, 몬스터와 조우하면 헥스 그리드 기반의 ATB 턴제 전투 씬으로 전환된다. 전투 승리 후 보상을 획득하고 탐험씬으로 복귀하는 구조다.

특이한 점은 씬 내에 **캠프(Camp) UI**가 내장되어 있어 탐험 도중 성장, 스킬 장착, 아이템 제작을 모달 형태로 수행할 수 있다는 것이다.

---

## 2. 기술 스택 및 패키지

| 분류 | 상세 |
|------|------|
| 렌더 파이프라인 | Universal Render Pipeline (URP) 14.0.12 |
| 맵 시스템 | Unity Tilemap + 2D Feature Pack (`com.unity.feature.2d` 2.0.1) |
| 에셋 관리 | Addressables 1.22.3 (비동기 로딩, 라벨 기반) |
| UI 텍스트 | TextMesh Pro 3.0.7 |
| 비동기 처리 | UniTask (Cysharp, Git 직접 참조) + Coroutine 혼합 |
| 입력 처리 | New Input System 1.14.2 |
| AI 보조 도구 | Unity MCP (CoplayDev, Git 직접 참조) — AI 에디터 연동 |
| 테스트 프레임워크 | Unity Test Framework 1.1.33 (설치됨, 테스트 코드 미작성) |
| 타임라인 | Unity Timeline 1.7.7 |
| IDE | JetBrains Rider 3.0.36 + Visual Studio 2022 2.0.22 |

**주목할 점:** `com.coplaydev.unity-mcp`가 패키지에 포함되어 있어, 개발 과정에서 AI 도구를 Unity 에디터와 직접 연동해 사용하고 있다. 이는 소규모 인디팀이 AI 지원 개발 환경을 구축한 사례다.

---

## 3. 폴더 구조 및 레이어 분리

```
Assets/
├── 00_Scenes/         씬 파일 (Title, Exploration, Battle, Test)
├── 01_Scripts/        C# 스크립트 전체 (도메인별 세분화)
│   ├── Battle/        전투 코어, FSM, 스킬, 유닛, AI, 시각 효과
│   ├── Core/          씬 전환, 인벤토리, 이벤트 버스, 카메라
│   ├── Data/          ScriptableObject 정의, DB, 효과 에셋
│   ├── Editor/        커스텀 에디터 툴 (임포터, 베이커 등)
│   ├── Exploration/   맵 로더, 플레이어 이동, 안개, 영속성
│   ├── Interactables/ 함정, 퍼즐, 상자, 포탈, 인카운터
│   ├── UI/            전투/탐험/캠프/공통 UI
│   └── Utils/         디버그, 어드레서블 테스터
├── 02_Prefabs/        Common, Title, Exploration, Battle 프리팹
├── 03_Data/           ScriptableObject 데이터 에셋
│   ├── Interactions/  상호작용 결과 데이터
│   ├── Item/          아이템 정의
│   ├── Skills/        스킬 에셋 (Player/Enemy 분리)
│   ├── StageDB/       스테이지 데이터베이스
│   ├── Trap/          함정 데이터
│   ├── Unit/          유닛 데이터 (Player/Enemy)
│   └── VisualDB/      시각 효과 DB
├── 04_Art/            스프라이트, 애니메이터, 폰트
├── 07_Test/           개발용 테스트 씬 및 스크립트
├── AddressableAssetsData/  어드레서블 그룹 설정
├── Editor/            에디터 전용 스크립트
└── Documentation/     마크다운 기술 문서 21개
```

**스크립트 도메인 분포:**

| 도메인 | 역할 |
|--------|------|
| `Battle/` (75개, 33%) | 전투 코어/FSM/스킬/유닛/AI |
| `UI/` (68개, 30%) | 전투/탐험/캠프/공통/팝업 UI |
| `Data/` (27개, 12%) | ScriptableObject 정의/DB/효과 |
| `Exploration/` (18개, 8%) | 맵로더/플레이어이동/안개/영속성 |
| `Interactables/` (16개, 7%) | 함정/퍼즐/상자/포탈/인카운터 |
| `Core/` (12개, 5%) | 씬전환/인벤토리/이벤트버스/카메라 |
| `Editor/ + Utils/` (11개, 5%) | 에디터 도구/디버그 |

---

## 4. 씬 구성 및 게임 플로우

### 4.1 씬 목록

`SceneName` enum으로 씬을 타입 안전하게 참조한다.

```
TitleScene     → 타이틀/시작 메뉴
ExplorationScene → 탐험 + 캠프 UI 포함
BattleScene    → 전술 전투
(Test 씬들)    → FogTest, Ui_Test, Test 등 개발용
```

### 4.2 게임 진행 흐름

```
[TitleScene]
    │ 신규 게임 / 불러오기
    ▼
[ExplorationScene]
    │ Tilemap 탐험
    │ 활기(Vigor) 소모 (이동: 2, 상자 검사: 1, 함정: 5, 상자 밀기: 3)
    │ 이벤트 발생: 몬스터 조우 / 상자 / 함정 / 포탈
    │ [Tab] → Camp UI (스킬/진형/제작)
    │
    │ 조우 이벤트 발생 시:
    ▼
[BattleScene]   (SceneTransitionManager가 스냅샷 저장 후 전환)
    │ ATB 헥스 그리드 전투
    │ 웨이브 클리어
    │ 보상 생성 (BattleRewardManager)
    ▼
[ExplorationScene 복귀]
    │ 스냅샷 복원, 보상 지급
    │ 플레이어 체력/MP/분노 동기화 (PlayerDataManager.SyncFromBattle)
    ▼
[패배 시] → TitleScene으로 이동
```

### 4.3 SceneTransitionManager 핵심 역할

전투 진입 전에 `ExplorationSnapshot`(탐험 맵 상태 스냅샷)을 저장하고, 전투 복귀 시 `RestoreSnapshot()`으로 맵 오브젝트 상태(상자 개방 여부, 몬스터 소멸 여부 등)를 복원한다. `pendingReturnScene`, `pendingReturnPosition`, `pendingRewards` 등의 필드로 씬 간 데이터를 전달한다.

---

## 5. 전투 시스템 (Battle System) 상세 분석

전투 시스템이 프로젝트에서 가장 복잡하고 완성도 높은 영역이다. 총 75개 스크립트가 이 도메인에 집중되어 있다.

### 5.1 헥스 그리드 구조

오프셋 헥스 그리드(Offset Hex Grid)를 사용한다. `HexUtil.cs`에 축 좌표(Axial) 변환 로직이 구현되어 있으며, 짝수/홀수 열에 따른 인접 셀 계산이 `IsOddColumn()` 헬퍼로 처리된다. 플레이어 팀과 적 팀은 각각 별도의 Tilemap(`PlayerFloor`, `EnemyFloor`)을 사용하며, `BattleGridManager`가 양쪽 팀의 점유 여부를 `HashSet<Vector3Int>` 기반으로 독립 관리한다.

### 5.2 ATB 턴 시스템

```
[ATBTurnController] — Update()마다 모든 유닛 ATB 충전
    ↓ unit.atbPerSecond = AGI * speedMultiplier (클램프: 1~10000)
    ↓ ATB >= MaxATB(100f)이면 턴 준비 완료
    ↓ 우선순위: Overfill(초과분) → AGI → 랜덤
    ↓ PauseTime() 후 OnTurnReady 이벤트 발송
[BattleManager.HandleTurnReady]
    ↓ BattleTurnManager.StartTurn(unit) 호출
[BattleTurnManager]
    ↓ 상태이상 처리 (수면→스킵, 공포→강제후퇴)
    ↓ 플레이어: ActionSelect 상태
    ↓ 적: EnemyTurnRoutine 시작
턴 종료 시: ATBTurnController.CompleteTurn() → ATB 리셋, 쿨다운 감소, ResumeTime()
```

### 5.3 BattleState FSM

`BattleState` enum으로 상태를 표현하고, `BattleStateMachine`(FSM 클래스)으로 관리한다. FSM 구체 상태들은 `BattleConcreteStates.cs`에 UniTask 기반으로 정의되어 있으나, 현재 레거시 `BattleManager`의 직접 상태 변경 방식과 병행 사용 중이다.

```
Idle
  └─ 턴 준비 완료 → ActionSelect
       ├─ 이동 선택 → Moving → [타일 클릭] → Resolving → (이동 완료) → ActionSelect/EndTurn
       ├─ 스킬 선택 → Targeting → [타겟 확정] → Resolving → FinishActionAfterSkill()
       │                       └─ 넉백 선택 → TargetingKnockback → Resolving
       └─ 휴식/진정 → EndTurn → Idle
```

### 5.4 전투 매니저 아키텍처 (7개 서브시스템)

`BattleManager`가 최상위 오케스트레이터 역할을 하며, 초기화 순서와 이벤트 라우팅을 담당한다.

| 서브시스템 | 책임 |
|-----------|------|
| `BattleGridManager` | 헥스 그리드 점유 조회, 이동 가능 셀 계산, 범위 내 유닛 목록 |
| `BattleFieldManager` | 타일 변경 장판(SmokeZone 등), 환경 효과, BeastDomain 프리무브 |
| `BattleTurnManager` | 턴 생명주기 관리, 행동력 소비, 과로(Overwork) 처리 |
| `BattleInputHandler` | 클릭/탭 입력, 이동/스킬 하이라이팅, 타겟 사이클링 |
| `BattleMapManager` | 웨이브별 적 진영 Tilemap 로드/교체 |
| `BattleSkillProcessor` | 스킬 실행 흐름, 데미지 계산, 넉백 처리 |
| `BattleWaveManager` | 웨이브 스폰, 다음 웨이브 전환, 전체 클리어 감지 |

### 5.5 유닛 등록 및 관리 (`_activeUnits` 레지스트리)

`BattleManager._activeUnits: HashSet<BattleUnit>`으로 씬 내 모든 유닛을 중앙 관리한다. 유닛 생성 시 `RegisterUnit()`, 사망/제거 시 `UnregisterUnit()`을 호출한다. 다수의 `FindObjectsOfType<BattleUnit>()` 호출을 이 레지스트리로 대체하는 최적화 작업이 진행 중이다(코드에 `// [Optimization] Use registry` 주석 18곳 이상).

### 5.6 BattleUnit 구조 (7개 Region으로 분리)

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

`UnitStats`, `UnitMover`, `UnitVisual` 세 컴포넌트로 책임을 분리하는 리팩토링이 진행 중이다.

**스탯 구조 (6개 기본 스탯):**
- `STR` (근력): 물리 공격력
- `CLV` (총명): 마법 공격력  
- `AGI` (민첩): ATB 충전 속도, 탈출 확률
- `BDY` (신체): 최대 HP 계산 (BDY*3 + STR)
- `MND` (정신): 마법 방어
- `INS` (통찰): 크리티컬/특수 판정

### 5.7 상태이상 시스템 (2계층)

전투의 상태이상은 두 계층으로 나뉜다.

**StatusController** — 스택 기반 수치 상태 (출혈, 중독, 방어, 탈진 등):
```
StatusId 범위:
  1~20   — 스킬 중첩 (Shooting/럭키식스, Action/기간트, Overwork/라스트보르그, Research 등)
  21~40  — 피해 보정 (Defense, Resistance, Weakness, Exhaustion, Slow, Suppression)
  50~55  — 지속 피해 (Bleeding 2%/스택, Poisoning 3%/스택, Ignition 3%/스택)

DebuffTuning.Mult: [1.0, 0.8, 0.6, 0.4, 0.3, 0.2, 0.1] (6스택 최대)
```

**UnitStateController** — 불리언 성격의 특수 상태:
```
UnitStateId: Sleep(수면→턴스킵), Fear(공포→강제후퇴), Ambush(매복→은신+힐), 
             Vigilance(경계→반격), Isolation(고립)
UnitStateBuffId: SmokeHidden(연막속), BeastDomain(야수영역)
```

### 5.8 스킬 데미지 계산 흐름 (`BattleSkillProcessor.GetFinalSkillDamage`)

```
baseDamage (스킬 power * 시전자 스탯)
    × FrontlineBonus (전방 보너스, ParametricDamageSkill.UseFrontlineBonus 시)
    × StateStatDB 피해배율 (StateStatModifierDB, 상태별 보정)
    × Physical 학교 시: Exhaustion 스택×1.2, Defense 스택×0.8
    × Magical 학교 시: Weakness 스택×1.2, Resistance 스택×0.8
    × Rage 보정: 1.0 + 0.01 * Rage
    → FloorToInt → 최종 피해량
```

### 5.9 적 AI 계층

| 클래스 | 용도 |
|--------|------|
| `EnemyAI` | 기본 AI. 가중치 랜덤 스킬 선택 (WeightedSO 배열) |
| `AnalysisEnemyAI` | 분석형 AI. 플레이어 상태 분석 후 최적 스킬 선택 |
| `BossEnemyAI` | 보스 AI. 웨이브/체력 조건별 고정 패턴 실행 |

타겟 선정 우선순위: `HighestHostility`(적대감 가중치 랜덤, 기본) / `LowestHP`(막타) / `Random` / `Closest`

### 5.10 EnemyCastState — 사전 시전 시스템

거미 몬스터(`WebCastWebTrap`)처럼 "이번 턴 준비, 다음 턴 발사" 패턴을 구현하는 컴포넌트다. `PendingCast` 구조체에 투사체 프리팹, 목표 셀, 덫 프리팹 등을 저장하고, 다음 턴 시작 시 `TryTakeReady()`로 꺼내 `Co_EnemyFireWebThenConsume()`으로 실행한다.

### 5.11 보상 시스템

전투 승리 시 `BattleRewardManager.GenerateRewards(profile)`이 호출된다. `WaveSet.RewardProfile`에 따라 아이템 풀(Material + Consumable)에서 랜덤 보상을 생성(3~5종)하고, `SceneTransitionManager.SetPendingRewards()`에 저장한 뒤 탐험씬 복귀 후 지급된다.

---

## 6. 탐험 시스템 (Exploration System) 상세 분석

### 6.1 MapManager 및 서브시스템 구조

`MapManager`가 탐험씬의 최상위 오케스트레이터로, 4개의 서브시스템을 통해 맵을 관리한다.

```
MapManager (Singleton)
├── ExplorationMapLoader    — Addressables 비동기 맵 프리팹 로드
├── ExplorationEntitySpawner — 적, 상자, 함정 등 오브젝트 스폰
├── ExplorationPersistenceManager — 씬 복귀 시 스냅샷 복원
└── PathfindingSystem       — BFS 기반 경로 탐색 (헥스 오프셋 그리드)
```

`StageDatabase`와 `currentStage`로 현재 스테이지를 추적하며, `MapConnectionData`로 맵 간 포탈 연결 정보를 관리한다.

### 6.2 PlayerMovement (1,255줄 — 리팩토링 대상)

탐험씬의 플레이어 이동 전담 클래스로, 현재 다음 책임이 혼재되어 있다.

**경로 이동 시스템:** 타일 클릭 시 `PathfindingSystem`으로 BFS 경로를 구하고, 코루틴으로 셀 단위 이동을 수행한다. 이동 중 `OnTileStepped` 정적 이벤트를 발송해 포탈, 함정 등이 감지할 수 있게 한다. 이동 마커(`pathMarkerPrefab`)와 목표 마커(`goalMarkerPrefab`)로 경로를 시각화한다.

**2단계 이동 UI:** 첫 번째 클릭은 목표 셀 선택(경로 미리보기), 두 번째 클릭은 이동 확정 방식이다. `selectedTargetCell`과 `isMovingByPath` 플래그로 상태를 구분한다.

**이동 잠금 시스템:** 시간 기반(`movementLockUntil`)과 토큰 기반(`_hardLockTokens`) 두 가지 잠금 방식을 지원한다.

**관련 핸들러:** `PlayerPushHandler`(상자 밀기), `PlayerInteractionHandler`(오브젝트 상호작용) 컴포넌트를 통해 기능을 위임한다.

### 6.3 활기(Vigor) 시스템

```
VigorManager (Singleton)
├── maxVigor = 30 (기본값)
├── costMovePerTile = 2     (타일 이동 1칸)
├── costInspectBox = 1      (상자 검사)
├── costTriggerTrap = 5     (함정 발동)
└── costPushBoxPerTile = 3  (상자 밀기 1칸)

과중량(Overweight) 처리:
└── InventoryManager 아이템 수 >= overweightThreshold(10) 시
    ExplorationStatusManager.AddStatus(ExplorationStatusID.Overweight) 추가
    → 이동 비용 증가 효과 (UI에 상태 아이콘 표시)

Vigor 고갈 시: onExplorationFailed UnityEvent 발동 → TitleScene 이동
```

### 6.4 안개(Fog) 시스템

`ExplorationFogManager`가 탐험 중 방문하지 않은 타일에 안개를 덮는다. 플레이어 이동 시 주변 타일의 안개를 걷어 내며, 방문 기록을 `ExplorationSnapshot`에 저장한다.

### 6.5 탐험 영속성 (Persistence)

씬 전환 시 `ExplorationPersistenceManager.RestoreSnapshot()`이 `ExplorationSnapshot` 데이터를 기반으로 맵 오브젝트 상태를 복원한다.

`IExplorationPersistable` 인터페이스를 구현하는 오브젝트(상자, 함정, 조우 등)는 고유 `PersistID`를 가지며, 상태(개방 여부, 소멸 여부 등)를 직렬화/역직렬화할 수 있다. Addressables를 통한 비동기 스폰도 지원한다.

### 6.6 상호작용 오브젝트 분류

| 카테고리 | 클래스 | 기능 |
|---------|--------|------|
| 채집 | `GatherableObject` | 자원 채집, 채집 가능 횟수 관리 |
| 상자 | `BoxInteract` | 아이템 획득, 팝업 연출 |
| 퍼즐 | `PushObject`, `PuzzleBox`, `BoxGoal` | 소코반 스타일 상자 밀기 퍼즐 |
| 함정 | `TrapBehavior`, `WebTrapController` | 피해/상태이상 부여 |
| 몬스터 | `EncounterMonster` | 접촉 시 전투 진입 트리거 |
| 포탈 | `PortalController`, `ExitHiddenPortalController` | 맵 이동 |
| 배리어 | `BarrierController` | 조건부 통로 차단 |

### 6.7 PathfindingSystem 상세 구조 (코드 확인 완료)

`PathfindingSystem.cs`를 직접 분석한 결과, 알고리즘과 내부 구조는 다음과 같다.

#### 탐색 알고리즘 — BFS (너비 우선 탐색, A* 아님)

순수 BFS다. `Queue<Vector3Int>`와 `Dictionary<Vector3Int, Vector3Int> cameFrom`을 사용하는 고전적 BFS 구현이며, 휴리스틱 비용 없이 최단 홉(hop) 수 경로를 반환한다.

```csharp
var queue = new Queue<Vector3Int>();
var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
// 목표 도달 후 cameFrom을 역추적해 경로 재구성
```

#### 다층 Floor 맵 시스템

`floorMaps: List<Tilemap>`으로 여러 높이의 바닥 레이어를 동시에 관리한다. 특정 좌표에 타일이 있는 맵을 찾을 때 **리스트 뒤쪽(인덱스 높음) + TilemapRenderer.sortingOrder 높음** 순으로 우선순위를 적용해 겹친 타일 중 시각적으로 위에 있는 타일을 정확히 선택한다.

#### IsWalkableCell — 4단계 통과 판정

```
1단계: occupiedCells.Contains(cell)
       → PushObject, BoxInteract 등 동적 장애물 HashSet 최우선 검사
       → 실패 시 즉시 false 반환 (이후 물리 연산 생략)

2단계: GetWalkableMapAt(cell) == null
       → floorMaps 전체 순회, 바닥 타일 자체가 없으면 false

3단계: obstacleMaps + wallMaps 순회
       → 장애물 또는 벽 타일이 존재하면 false

4단계: Physics2D.OverlapBox(worldPos, Vector2(0.8f, 0.8f), impassableLayerMask)
       → 정적 구조물 콜라이더 검사 (isTrigger는 통과)
       → 주의: PushObject/BoxInteract 콜라이더는 이 LayerMask 제외 필수
         (이미 1단계 occupiedCells로 처리하기 때문)
```

#### IsHeightDiffValid — 높이 차이 이동 제한

서로 다른 바닥 레이어 간 이동 가능 여부를 각 Tilemap의 `tileAnchor.y` 값 차이로 판단한다.

```csharp
float diff = Mathf.Abs(toMap.tileAnchor.y - fromMap.tileAnchor.y);
return diff < 0.55f;  // 0.55f 이상 차이나면 이동 불가
```

낮은 절벽은 오르내릴 수 있지만 높은 절벽은 막히는 층 이동 제한이 코드 변경 없이 **tileAnchor 수치 조정만으로** 제어된다.

#### TileAnchor 좌표 보정

`GetCellFromWorldPos()`는 단순 `WorldToCell()` 호출이 아니라 각 Tilemap의 `tileAnchor` 오프셋을 역산해 보정한다. tileAnchor가 (0.5, 0.5, 0) 외의 값으로 설정된 다층 맵에서도 클릭 좌표를 정확한 그리드 셀로 변환하기 위한 처리다.

```csharp
Vector3 anchorOffset = grid.LocalToWorld(grid.CellToLocalInterpolated(map.tileAnchor))
                     - grid.LocalToWorld(grid.CellToLocalInterpolated(Vector3.zero));
Vector3 correctedPos = worldPos - anchorOffset;
```

#### 물리 거리 필터 (BFS 오작동 방지)

BFS 탐색 중 수학적으로 인접 셀로 계산된 좌표라도 실제 월드 거리가 `2.0f`를 초과하면 건너뛴다. 헥스 오프셋 좌표계에서 다른 층이거나 물리적으로 먼 타일이 잘못 인접 셀로 포함되는 경우를 걸러내는 안전장치다.

#### 헥스 6방향 오프셋 (짝수/홀수 행 분기)

탐험 맵도 전투와 동일한 헥스 오프셋 좌표계를 사용한다. BFS 탐색 방향은 6방향(W, E, NW, NE, SW, SE)이며, 행(y)의 홀수/짝수에 따라 오프셋 값이 달라진다.

```
짝수 행(y%2==0): NW(-1,1), NE(0,1), W(-1,0), E(1,0), SW(-1,-1), SE(0,-1)
홀수 행(y%2==1): NW(0,1),  NE(1,1), W(-1,0), E(1,0), SW(0,-1),  SE(1,-1)
```

#### FindPathToAdjacentCell — 오브젝트 상호작용용 특수 경로

상자(`BoxInteract`), 퍼즐 박스 등 특정 오브젝트에 **인접**해야 상호작용이 가능한 경우를 위한 전용 메서드다. 목표 셀 자체가 아닌 목표 셀 주변 6개 인접 셀 각각에 `FindPath`를 시도하고, 그 중 가장 짧은 경로를 반환한다.

```csharp
public List<Vector3Int> FindPathToAdjacentCell(Vector3Int start, Vector3Int objectCell)
// → 목표 인접 6방향 중 도달 가능한 셀 중 최단 경로 반환
```

### 6.8 QTE (Quick Time Event) 시스템

`ExplorationQTEManager`가 탐험 중 특정 이벤트에서 QTE를 처리한다. `BaseQTEController`를 상속하는 `SimpleQTEController`가 UI와 연동되며, 성공/실패에 따른 보상 차등 지급이 구현되어 있다.

### 6.8 탐험 상태 (ExplorationStatus)

`ExplorationStatusID` enum으로 탐험 상태를 정의하고, `ExplorationStatusManager`가 중첩(스택) 기반으로 관리한다.

---

## 7. 캐릭터 데이터 및 성장 시스템

### 7.1 UnitData ScriptableObject

```csharp
public class UnitData : ScriptableObject
{
    int unitID;
    string DisplayName;
    
    // 6대 기본 스탯
    int baseSTR, baseCLV, baseAGI, baseBDY, baseMND, baseINS;
    int baseHostility;  // 적의 타겟팅 가중치
    
    Team team;          // Player / Enemy / Neutral
    ISBOSS isBoss;      // 보스 여부
    
    SkillAnimBinding[] skillAnimBindings;  // 스킬별 애니메이션 매핑
    SkillAsset[] skills;     // 보유 스킬
    PassiveAsset[] passives; // 패시브 목록
    
    int currentBond;    // 유대 수치 (0~12)
    TraitAsset[] traits; // 성격 특성 (2/6/12 유대에 해금)
    
    Sprite UnitIcon, UnitStandImage;
    GameObject battlePrefab;
}
```

### 7.2 런타임 상태 관리 (RuntimeUnitData)

전투 중 변한 HP/MP/Rage는 `RuntimeUnitData`에 저장되고, 전투 종료 시 `PlayerDataManager.SyncFromBattle()`/`SyncToBattle()`로 탐험 씬과 동기화된다.

```csharp
public class RuntimeUnitData
{
    float currentHP, currentMP, currentRage;
    bool isDead;
    Dictionary<string, int> statModifiers; // 영구 스탯 변화량
}
```

### 7.3 진형(Formation) 시스템

`PlayerDataManager.formation: UnitData[19]`으로 최대 19개 슬롯의 진형을 관리한다. `BattleManager.SpawnPlayerUnits()`에서 진형 데이터를 읽어 `BattleMapManager.GetFormationSpawnPoint(i)`의 위치에 전투 프리팹을 소환한다.

### 7.4 패시브 시스템

`PassiveAsset`(abstract ScriptableObject)를 상속하는 캐릭터별 패시브들이 있다. 해금 상태는 `PlayerDataManager._unlockedPassiveIds: HashSet<string>`으로 중앙 관리되며(PlayerPrefs에서 마이그레이션 중), 전투 진입 시 `BattleUnit.InitPassives(battleManager)`에서 해금된 패시브만 활성화한다.

**구현된 캐릭터 패시브들:**

| 캐릭터 | 패시브 | 효과 |
|--------|--------|------|
| 기간트(Gigant) | CounterStack | 대응(Action) 스택 관련 반격 |
| 기간트 | EndTurnRegen | 턴 종료 시 체력 재생 |
| 기간트 | StrengthToBody | STR→BDY 스탯 전환 |
| 노을(NoEul) | BleedCountAgility | 출혈 스택 수에 비례한 AGI 상승 |
| 노을 | DoubleAttack | 이중 공격 발동 |
| 노을 | WeakOnLowestHp | 체력 최저 대상에 취약 부여 |
| 럭키식스(LuckySix) | ShootingInsight | 사격 통찰 (TODO 미완성) |
| 럭키식스 | ReactiveMoveAttack | 이동 후 반응 공격 |
| 럭키식스 | ReactiveAfterMoveAttack | 이동 후 공격 반응 |
| 라스트보르그(LastVorg) | Rage | 분노 중첩 관련 |
| 라스트보르그 | Research | 연구 중첩 관련 |
| 라스트보르그 | Toxic | 독성 관련 |

### 7.5 특성(Trait) 및 훈련(Training)

`TraitAsset`은 유대 수치 2/6/12에 해금되는 캐릭터 고유 특성이다. `TrainingDB`는 스킬별 훈련 루트(최대 3개)를 정의하며, 훈련 루트 활성화로 스킬 비용, 범위, 사후 이동 등의 변형이 가능하다.

---

## 8. 인벤토리 및 아이템 시스템

### 8.1 InventoryManager

```csharp
public class InventoryManager : MonoBehaviour, IInventory
{
    int maxSlots = 12;
    int maxStack = 6;
    InventoryItem[] slots;           // 실제 슬롯 배열
    Dictionary<string, int> _itemCountCache; // O(1) 검색 캐시
}
```

아이템 추가 시 기존 같은 아이템의 여유 슬롯에 먼저 합치고, 부족하면 빈 슬롯을 사용한다. `OnInventoryChanged` 이벤트로 UI에 변경을 통지한다.

### 8.2 ItemData ScriptableObject

```csharp
public class ItemData : ScriptableObject
{
    string itemID;      // Primary Key
    string itemName;
    string atlasAddress, spriteName;  // Addressables Atlas 참조
    ItemType itemType;  // Material / Consumable 등
    int maxStack;
    ItemEffectSO useContextEffect;  // 소비 효과 SO
}
```

아이콘은 Addressables Atlas를 통해 `"ItemAtlas[icon_potion]"` 형식으로 런타임 로드된다.

### 8.3 아이템 효과 (ItemEffectSO 계층)

```
ItemEffectSO (abstract)
├── HealEffectSO       — HP 회복
└── RestoreMPEffectSO  — MP 회복
```

### 8.4 제작 시스템 (Camp Craft)

`CampCraftPage`에서 `CraftRecipe` SO를 기반으로 재료 아이템을 소비하고 결과물을 생성하는 제작 시스템이 구현되어 있다. `CraftResultPopup`으로 제작 결과를 표시한다.

### 8.5 드래그 앤 드롭

`InventoryDragHandler`와 `TrashZone`으로 인벤토리 UI에서 드래그 앤 드롭으로 아이템을 버리거나 슬롯을 이동하는 기능이 구현되어 있다.

---

## 9. 스킬 시스템 구조

### 9.1 SkillAsset 기본 구조

모든 스킬은 `SkillAsset`(abstract ScriptableObject)을 상속한다.

```csharp
public abstract class SkillAsset : ScriptableObject
{
    string id, displayName;
    DamageSchool school;    // Physical / Magical
    AttackAttr attribute;   // 속성
    float power;            // 기본 배수
    
    SkillCostResource costResource; // MP / Rage
    int cost;
    
    SkillTargetMode targetMode;         // Unit / Tile
    SkillTargetAlignment targetAlignment; // Enemy / Ally / Self / Any
    int cooldownTurns;
    
    SkillAnimKind animKind;  // Melee / Ranged / Casting
    bool useGapCloseJump;    // 갭 클로즈 여부
    
    TrainingRouteInfo[] trainingRoutes; // 훈련 루트 3개
    
    virtual IEnumerator Execute(BattleManager bm, BattleUnit caster, 
                                BattleUnit target, Tilemap map, Vector3Int cell);
}
```

### 9.2 스킬 템플릿 계층

```
SkillAsset (abstract)
├── ParametricDamageSkill   — 데미지 스킬 (범위, 투사체, 넉백, 상태부여 등 파라미터화)
├── ParametricHealSkill     — 치유 스킬
├── ParametricSupportSkill  — 지원 스킬 (부활 포함)
├── ParametricDirectionSkill — 방향 지정 스킬
├── SelfStateSkill          — 자기 자신에게 상태 부여
├── SelfStateCleanseSkill   — 자기 상태 해제
├── SelfAmbushSkill         — 매복 상태 진입
├── SelfVigilanceSkill      — 경계 상태 진입
├── SelfBeastDomainSkill    — 야수 영역 발동
├── SelfIsolationTimedSkill — 시간제 고립 상태
├── SmokeBombSkill          — 연막 장판 설치
├── FearOnBleedSkill        — 출혈 대상에 공포 부여
├── HostilitySpikeSkill     — 적의 증가
├── AllyRetreatSwapSkill    — 아군 후퇴 교체 (전술)
├── EnemySkill              — 적 전용 스킬 기반 클래스
│   └── EnemyBasicAttack, WebCastWebTrap 등
└── StateConditionalMulti   — 상태 조건부 복합 스킬
```

### 9.3 ISkillForStateResolver 인터페이스

캐릭터 상태에 따라 다른 스킬 에셋을 반환하는 전략 패턴이다. 예를 들어 매복 상태일 때 일반 공격 스킬이 강화된 버전으로 교체될 수 있다.

```csharp
public interface ISkillForStateResolver
{
    SkillAsset ResolveForCaster(BattleUnit caster);
}
```

### 9.4 ParametricDamageSkill 주요 파라미터

가장 범용적인 데미지 스킬 템플릿으로 다음 기능들이 파라미터로 노출되어 있다.

- `AreaPreset`: Single, Line, Cone, Adjacent 등 범위 프리셋
- `applyStatusOnHit`: 적중 시 부여할 상태이상 목록 (StatusEffectInfo 배열)
- `changeTileTo`: 적중 타일 변경 (지속 장판)
- `projectilePrefab / projectileSpeed`: 투사체 설정
- `conditionalMultipliers`: 특정 상태이상 보유 시 추가 배율
- `UseFrontlineBonus`: 전방 유닛 추가 피해
- `trainingUseAreaOverride`, `trainingUsePostMove` 등: 훈련 루트별 변형

---

## 10. 캠프 UI 시스템

캠프 UI는 `ExplorationScene` 내에 모달로 내장되어 있다(별도 씬 아님). `Tab` 키로 열고 닫는다.

### 10.1 탭 구조

```
CampUIManager (ModalWindowBase)
├── CampTabController    — 탭 전환 관리
├── CampHeaderController — 상단 헤더 (캐릭터 선택 / 아이템 선택)
├── CharacterHeaderController — 캐릭터 선택 UI
│
├── [Status 탭] CampStatusPage   — 캐릭터 스탯, 특성, 유대 정보
├── [Skill 탭] CampSkillPage     — 스킬 슬롯 배치, 훈련 루트 선택, 패시브 해금
├── [Craft 탭] CampCraftPage     — 아이템 제작
└── [Option 탭] CampOptionPage   — 게임 옵션
```

### 10.2 스킬 슬롯 시스템 (CampSkillPage, 1,090줄)

4종류의 슬롯이 있다: `CampSkillSlot`(스킬), `CampSubSkillSlot`(서브), `CampPassiveSlot`(패시브), `CampTrainingSlot`(훈련). `CampTraitSlot`은 특성을 표시하며, `CampBondHeart`는 유대 수치 하트 UI다. 드래그 앤 드롭으로 스킬 슬롯 간 이동 기능이 구현되어 있다.

### 10.3 진형 슬롯 (FormationSlotUI)

`FormationSlotUI`로 전투 진형을 편집한다. `PlayerDataManager.formation` 배열에 유닛을 배치/제거한다.

---

## 11. 씬 전환 및 데이터 지속성

### 11.1 SceneTransitionManager 전체 흐름

`DontDestroyOnLoad`로 씬 간 생존하며, 페이드 전환(CanvasGroup 알파)과 로딩 프로그레스바를 제공한다.

**전투 진입 시 저장하는 데이터:**
- `explorationSnapshot`: 맵 오브젝트 상태
- `pendingReturnScene`: 복귀할 씬 이름
- `pendingReturnPosition`: 복귀 위치
- `savedVigor`: 활기 스냅샷
- `pendingResumeCells`: 전투 후 이어서 이동할 경로
- `pendingPlannedMoveVigorCost`: 계획된 이동 활기 비용

**전투 종료 시 처리:**
- `pendingRewards` 보상 지급
- `PlayerDataManager.SyncFromBattle()` 유닛 상태 동기화
- `RestoreSnapshot()` 맵 복원

### 11.2 PlayerDataManager 저장 시스템 (현재 미완성)

```csharp
public class PlayerDataManager : MonoBehaviour
{
    // 런타임 데이터
    Dictionary<UnitData, RuntimeUnitData> unitStates;
    HashSet<string> _unlockedPassiveIds;
    
    // Addressables 추적
    ResourceTracker _tracker;  // 핸들 누수 방지
    
    void SaveGame()   → PlayerPrefs.SetString("SaveSlot_1", JSON)
    void LoadGame()   → PlayerPrefs.GetString("SaveSlot_1")
}
```

현재 PlayerPrefs JSON 방식으로 저장하고 있으며, `Application.persistentDataPath` 기반 파일 저장으로 마이그레이션이 필요한 상태다.

---

## 12. 코어 인프라 시스템

### 12.1 GameEventBus (타입 안전 이벤트 버스)

```csharp
public static class GameEventBus
{
    static Dictionary<Type, List<Delegate>> _handlers;
    
    public static void Subscribe<T>(Action<T> handler);
    public static void Unsubscribe<T>(Action<T> handler);
    public static void Publish<T>(T eventMessage);  // 역순 반복으로 구독 중 해제 안전
}

// 사용 예시:
GameEventBus.Publish(new UnitDamagedEvent(target, amount, isCrit));
```

현재 `UnitDamagedEvent`가 정의되어 있으나, 대부분의 이벤트는 아직 매니저 간 직접 C# event로 처리한다.

### 12.2 CameraFollow2D + CameraDynamicOffset

카메라 시스템은 두 컴포넌트로 분리된다. `CameraFollow2D`가 기본 팔로우를 담당하고, `CameraDynamicOffset`이 플레이어 이동 방향에 따른 동적 오프셋을 추가한다.

### 12.3 ResourceTracker (Addressables 핸들 관리)

`PlayerDataManager`에서 Addressables 핸들 누수를 방지하기 위해 도입된 유틸 클래스. `LoadAssetsAsync()` 시 핸들을 추적하고, `OnDestroy` 시 `ReleaseAll()`을 호출한다.

### 12.4 StageRuntimeContext

스테이지 진행 중 공유해야 하는 런타임 컨텍스트 데이터를 보관하는 전용 싱글톤이다.

### 12.5 PopupManager + ModalWindowBase

`PopupManager`가 씬 내 모달/팝업의 스택을 관리한다. `ModalWindowBase`를 상속하는 팝업들 (`CampUIManager`, `ConfirmationPopup`, `RewardPopupUI` 등)이 큐에 의해 순차 표시된다. `async UniTask<bool> ConfirmRetreatAsync()`처럼 비동기 팝업 응답을 지원한다.

### 12.6 FloatingText 시스템

`FloatingTextManager`가 전투/탐험 중 화면에 부유하는 텍스트(피해량, 상태 텍스트 등)를 풀링하여 표시한다. `FloatingTextDef` SO로 텍스트 스타일(색상, 폰트 크기, 애니메이션)을 정의한다.

---

## 13. 에디터 도구 및 데이터 파이프라인

### 13.1 CSV → ScriptableObject 자동 임포터

`GatherableDataImporter`가 CSV 파일을 읽어 `GatherableDataSO`를 자동 생성한다. 전용 에디터 윈도우를 통해 맵 데이터도 자동으로 설정한다(`MapDataAutoSetupTool`).

### 13.2 MapIDBaker

맵 프리팹에 고유 ID를 자동으로 부여하는 에디터 도구다.

### 13.3 AddressablesBuilder

에디터 메뉴에서 Addressables 빌드를 트리거하는 커스텀 빌더 스크립트다.

### 13.4 MissingScriptFinder

씬/프리팹에서 누락된 스크립트 컴포넌트를 검색하는 에디터 유틸리티다.

### 13.5 UniversalDataImporter

범용 데이터 임포터로, 다양한 형식의 외부 데이터를 ScriptableObject로 변환하는 파이프라인을 제공한다.

---

## 14. 현재 개발 상태 및 미완성 영역

### 14.1 완성도 높은 영역

- **전투 코어 시스템**: ATB, FSM, 스킬 실행 흐름, 데미지 계산 완성
- **탐험 이동 시스템**: 경로탐색, 2단계 이동 UI, 잠금 시스템 완성
- **스킬 데이터 시스템**: SkillAsset 계층, ParametricDamageSkill 파라미터화 완성
- **상태이상 시스템**: StatusController + UnitStateController 2계층 완성
- **UI 아키텍처**: 전투/캠프/탐험 UI 분리, 모달 관리 완성

### 14.2 진행 중인 작업

- **BattleUnit 리팩토링**: UnitStats/UnitMover/UnitVisual 컴포넌트 분리 진행 중 (`// [RequireComponent...]` 주석으로 확인)
- **FSM 통합**: `BattleStateMachine` + `BattleConcreteStates`가 UniTask 기반으로 구현되었으나, 레거시 직접 상태 변경 방식과 병행 운영 중
- **FindObjectsOfType 제거**: `// [Optimization] Use registry` 주석 18곳 이상, 일부 미완료
- **저장 시스템 마이그레이션**: PlayerPrefs → File JSON 전환 진행 중

### 14.3 미완성/TODO 항목

```
LuckySixShootingInsightPassive.cs — TODO 주석 3개, 실제 스탯 연동 미구현
GigantEndTurnRegenPassive.cs — 테스트용 강제 진행도 반환 코드 잔존
TitleMenuUI.cs — [SerializeField] bool simulateHasSaveData (개발용 플래그)
SceneTransitionManager.cs:357 — "임시 테스트용" 주석
ExplorationStatusSlot.cs — 임시 MaxHP 계산 방식
```

### 14.4 07_Test 폴더 (출시 전 정리 필요)

- 테스트 씬: `Test.unity`, `FogTest.unity`, `Ui_Test.unity`
- 디버그 스크립트: `QTESystemTester`, `TilemapDebugger`, `AddressableLoaderTest`, `AddressableSpawnerTest`, `TileCounter`, `ItemIconLoader`

---

## 15. 발견된 기술적 부채 및 개선 과제

### 15.1 🔴 Critical — 즉시 수정 필요

**[C-1] async void 패턴 — 예외 무음 처리 위험**

다음 5곳에 `async void`가 잔존한다. Unity에서 `async void`는 예외가 UnityException으로 래핑되지 않아 씬이 무응답 상태로 빠질 수 있다.

- `BattleStateMachine.ChangeState()`
- `BattleManager.OnClickEscape()` (일부 — 이미 UniTaskVoid로 개선된 코드 있음)
- `PlayerDataManager.LoadStartingUnitsByLabel()`
- `PlayerDataManager.AddUnitByAddress()`
- `MapObjectSpawner.Spawn()`

수정 방향: `async UniTaskVoid` + try-catch 래핑

**[C-2] 저장 시스템 미완성**

- 인벤토리/패시브/유닛 상태가 `PlayerPrefs`에 JSON으로 분산 저장됨
- `RuntimeUnitData.statModifiers: Dictionary<string, int>`는 `JsonUtility`로 직렬화 불가 (`KeyValuePair` 손실)
- 권장: `Application.persistentDataPath` 기반 파일 저장 + Newtonsoft.Json 또는 커스텀 직렬화

**[C-3] CRLF 인코딩 혼용**

`PlayerMovement.cs`, `ATBTurnController.cs` 등에서 `\r\r\n` (CRLF 두 번) 패턴이 다수 발견된다. 프로젝트 루트의 `FixGarbledText.ps1`, `FixEncoding.ps1`의 존재가 이를 증명한다.

수정 방향: `.gitattributes`에 `* text=auto eol=lf`, `.editorconfig`에 `charset = utf-8` 강제

### 15.2 🟡 Warning — 이번 마일스톤 내 개선 권장

**[W-1] 과도한 싱글톤 — 26개 이상**

`BattleManager`, `MapManager`, `PlayerMovement`, `UIManager`, `PopupManager`, `InventoryManager`, `SceneTransitionManager`, `BattleRewardManager`, `StageRuntimeContext`, `ExplorationFogManager`, `VigorManager`, `ExplorationStatusManager` 등.

씬 범위 싱글톤과 영구 싱글톤이 혼재해 테스트 격리가 불가능하다. 씬별 수명을 명확히 구분하고, 장기적으로 의존성 주입(Zenject 등) 도입을 검토해야 한다.

**[W-2] God Object 위험 — 거대 파일**

| 파일 | 줄 수 | 문제 |
|------|-------|------|
| `PlayerMovement.cs` | 1,255줄 | 경로 이동 + 소코반 퍼즐 + QTE 트리거 + 카메라 신호 혼재 |
| `BattleUnit.cs` | 1,909줄 (추정) | 유닛 데이터 + 애니메이션 + ATB + 스킬 쿨다운 + 상태이상 + 사망 처리 |
| `BattleManager.cs` | 1,313줄 | 오케스트레이터 + 이동/스킬/타겟팅/사망 처리 직접 포함 |
| `CampSkillPage.cs` | 1,090줄 | 스킬 슬롯 + 훈련 루트 + 패시브 해금 로직 혼합 |

**[W-3] 비동기 패턴 혼합**

Coroutine(`IEnumerator`), `async/await`, `UniTask`가 혼용된다. 스킬 흐름은 Coroutine, FSM 전환은 async void, 팝업 대기는 `async Task<bool>`, 리소스 로딩은 UniTask. 단일 비동기 표준(UniTask 권장)으로 통일이 필요하다.

**[W-4] 개발용 코드 프로덕션 혼재**

개발용 플래그와 TODO 주석이 프로덕션 코드에 노출되어 있다. `#if UNITY_EDITOR` 조건부 컴파일 또는 별도 Debug 씬으로 분리가 필요하다.

### 15.3 🔵 Info — 장기 개선 과제

**[I-1] 단위 테스트 전무**

`com.unity.test-framework` 1.1.33이 설치되어 있으나 실제 테스트 코드가 없다. `BattleSkillProcessor.GetFinalSkillDamage()`, `ATBTurnController` 턴 순서 로직, `InventoryManager.AddPartialItem()` 등 순수 계산 함수부터 단위 테스트 작성을 권장한다.

**[I-2] 07_Test 폴더 분리**

`.asmdef`로 테스트 전용 어셈블리를 만들고 빌드에서 제외하는 설정이 필요하다.

**[I-3] Addressables 핸들 관리 일원화**

`ResourceTracker` 클래스가 `PlayerDataManager`에 도입되었으나, `ExplorationPersistenceManager`의 `_activeAddressables` 목록과 이원화되어 있다. 단일 생명주기 추적 방식으로 통합해야 한다.

---

## 16. 종합 평가 및 액션 플랜

### 16.1 종합 평가

| 평가 항목 | 점수 | 근거 |
|----------|------|------|
| 아키텍처 구조 | ★★★★☆ | FSM, Command, Observer 패턴 적절 적용. 전투 서브시스템 7개 분리 명확 |
| 코드 가독성 | ★★★☆☆ | 한국어 주석 철저하나 거대 파일 다수. CRLF 인코딩 문제 |
| 데이터 설계 | ★★★★☆ | SkillAsset SO 계층 설계 우수. CSV 자동 임포터 파이프라인 존재 |
| 안정성/예외처리 | ★★☆☆☆ | async void 5곳, PlayerPrefs 저장 미완성, 인코딩 문제 |
| 성능 관리 | ★★★☆☆ | _activeUnits 레지스트리 도입했으나 FindObjectsOfType 잔존 |
| 테스트 가능성 | ★★☆☆☆ | 싱글톤 26개, 단위 테스트 전무 |
| 문서화 | ★★★★★ | 기술 문서 21개, 한국어 주석 철저, 구현 계획서 별도 관리 |
| 확장성 | ★★★★☆ | ISkillForStateResolver, ISelfCastSkill 등 인터페이스 분리 우수 |

### 16.2 우선순위별 액션 플랜

| 우선순위 | 작업 항목 | 예상 공수 |
|---------|----------|----------|
| 🔴 P0 (즉시) | `async void` 5곳 → `async UniTaskVoid` + try-catch 전환 | 2~3일 |
| 🔴 P0 (즉시) | `.gitattributes` CRLF 설정 + 인코딩 일괄 정리 (FixEncoding.ps1 활용 후 제거) | 0.5일 |
| 🔴 P0 (즉시) | 저장 시스템 일원화 (PlayerPrefs → persistentDataPath JSON, statModifiers 직렬화 문제 해결) | 3~5일 |
| 🟡 P1 | `FindObjectsOfType` 잔존 17곳 → `_activeUnits` 레지스트리 교체 완료 | 1~2일 |
| 🟡 P1 | `PlayerMovement.cs` 분리 (PathFollower, GridInteractionHandler 등으로) | 3~5일 |
| 🟡 P1 | `BattleStateMachine` FSM을 레거시 직접 상태 변경 방식과 완전 통합 | 2~3일 |
| 🟡 P1 | 개발용 플래그 `#if UNITY_EDITOR` 격리 | 1일 |
| 🟡 P2 | LuckySix `ShootingInsightPassive` TODO 3개 구현 완료 | 2~3일 |
| 🔵 P3 | 단위 테스트 작성 (데미지 계산, ATB 순서, 인벤토리 로직) | 5일~ |
| 🔵 P3 | `07_Test` 폴더 `.asmdef` 분리 및 빌드 제외 | 0.5일 |
| 🔵 P3 | Addressables 핸들 관리 ResourceTracker로 일원화 | 2일 |
| 🔵 P3 | 비동기 패턴 Coroutine → UniTask 단계적 통일 | 5일~ |

---

*Lemegeton Project Research Report — Generated 2026-02-27 by Claude Sonnet 4.6*
