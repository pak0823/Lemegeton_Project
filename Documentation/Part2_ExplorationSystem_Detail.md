# Part 2: 탐험 시스템 (Exploration System) — 상세 분석

**분류:** 파트2 — 탐험 시스템
**작성일:** 2026-03-04
**참조 파일:** `Assets/01_Scripts/Exploration/`, `Assets/01_Scripts/Interactables/`
**관련 문서:** Research.md §6, ExplorationSystem_Analysis.md

---

## 목차

1. [탐험 아키텍처 전체 구조](#1-탐험-아키텍처-전체-구조)
2. [MapManager 서브시스템 4종](#2-mapmanager-서브시스템-4종)
3. [PlayerMovement 및 분리 컴포넌트](#3-playermovement-및-분리-컴포넌트)
4. [PathfindingSystem 상세 구조](#4-pathfindingsystem-상세-구조)
5. [활기(Vigor) 시스템](#5-활기vigor-시스템)
6. [안개(Fog) 시스템](#6-안개fog-시스템)
7. [탐험 영속성 (Persistence)](#7-탐험-영속성-persistence)
8. [QTE 시스템](#8-qte-시스템)
9. [탐험 상태 (ExplorationStatus)](#9-탐험-상태-explorationstatus)
10. [상호작용 오브젝트 분류](#10-상호작용-오브젝트-분류)
11. [맵 전환 시스템](#11-맵-전환-시스템)
12. [씬 전환 및 데이터 지속성](#12-씬-전환-및-데이터-지속성)

---

## 1. 탐험 아키텍처 전체 구조

`Exploration/` 폴더는 탐험씬의 모든 시스템을 담당한다.

```
Exploration/
├── Components/    탐험 컴포넌트
├── Core/          ExplorationMapLoader, ExplorationEntitySpawner 등
├── Data/          탐험 데이터
├── Interactables/ 탐험 전용 상호작용
├── System/        ExplorationFogManager, PathfindingSystem 등 16개+
├── MapManager.cs                  맵 오케스트레이터 (Singleton)
├── MapTransitionManager.cs        맵 간 전환 처리
├── MapToggleManager.cs            미니맵 토글 UI 연동
├── PlayerMovement.cs              플레이어 이동 핵심 (~23KB)
├── PlayerInputController.cs       입력 처리 분리
├── PlayerInteractionHandler.cs    상호작용 판정
├── PlayerPushHandler.cs           소코반 밀기
├── ExplorationInteractionController.cs  통합 상호작용 컨트롤러 (신규)
├── ExplorationQTEManager.cs       QTE 관리
├── ExplorationStatusManager.cs    탐험 상태 관리
└── VigorManager.cs                활기 자원 관리
```

---

## 2. MapManager 서브시스템 4종

`MapManager`가 탐험씬의 최상위 오케스트레이터로, 4개의 서브시스템을 통해 맵을 관리한다.

```
MapManager (Singleton)
├── ExplorationMapLoader      — Addressables 비동기 맵 프리팹 로드
├── ExplorationEntitySpawner  — 적, 상자, 함정 등 오브젝트 스폰
├── ExplorationPersistenceManager — 씬 복귀 시 스냅샷 복원
└── PathfindingSystem         — BFS 기반 경로 탐색 (헥스 오프셋 그리드)
```

### ExplorationMapLoader

- Addressables 비동기(`async UniTask`) 맵 프리팹 Instantiate/Destroy
- 전투 복귀 시 기존 맵 재사용 (재생성 방지)

### ExplorationEntitySpawner

- 플레이어 스폰 위치 설정
- 상자, 함정, 인카운터 오브젝트 배치
- Addressables를 통한 비동기 스폰 지원

### ExplorationPersistenceManager

- `ExplorationSnapshot` 데이터 기반 맵 오브젝트 상태 복원
- Addressables 메모리 관리 (ResourceTracker와 통합)
- `IExplorationPersistable` 구현 오브젝트의 PersistID 기반 상태 복구

**StageDatabase와 관계:**
`MapManager`는 `StageDatabase`와 `currentStage`로 현재 스테이지를 추적하며, `MapConnectionData`로 맵 간 포탈 연결 정보를 관리한다.

---

## 3. PlayerMovement 및 분리 컴포넌트

### 리팩토링 현황 (완료)

기존 2,525줄 God Object에서 5개 컴포넌트로 책임이 분리되었다.

| 파일                                  | 크기   | 역할                                   |
| ------------------------------------- | ------ | -------------------------------------- |
| `PlayerMovement.cs`                   | ~23KB  | 핵심 그리드 이동, 경로 추적, 이동 잠금 |
| `PlayerInputController.cs`            | 소~중  | New Input System 연동, 입력 처리 전담  |
| `PlayerInteractionHandler.cs`         | ~4.5KB | 오브젝트 상호작용 판정 전담            |
| `PlayerPushHandler.cs`                | ~13KB  | 소코반 스타일 상자 밀기 전담           |
| `ExplorationInteractionController.cs` | ~13KB  | 통합 상호작용 컨트롤러 (신규 추가)     |

### 2단계 이동 UI

```
1단계: 타일 클릭 → 목표 셀 선택 + 경로 미리보기 (pathMarker 표시)
2단계: 확정 클릭 → 실제 이동 수행

selectedTargetCell: 현재 선택된 목표 셀
isMovingByPath: 이동 중 여부 플래그
```

### 이동 잠금 시스템

| 잠금 방식 | 사용 변수           | 용도                              |
| --------- | ------------------- | --------------------------------- |
| 시간 기반 | `movementLockUntil` | 특정 시간까지 이동 금지           |
| 토큰 기반 | `_hardLockTokens`   | 다중 시스템이 잠금 토큰 발행/해제 |

### OnTileStepped 이벤트

이동 중 각 셀 진입 시 `OnTileStepped` 정적 이벤트를 발송한다. 포탈, 함정, 인카운터 등이 이를 구독하여 자동으로 감지한다.

---

## 4. PathfindingSystem 상세 구조

### 탐색 알고리즘 — 순수 BFS

```csharp
var queue = new Queue<Vector3Int>();
var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
// 목표 도달 후 cameFrom을 역추적해 경로 재구성
```

A\* 아님, 순수 BFS로 최단 홉(hop) 수 경로를 반환한다.

### 다층 Floor 맵 시스템

`floorMaps: List<Tilemap>`으로 여러 높이의 바닥 레이어를 동시 관리.

- 우선순위: **리스트 뒤쪽(인덱스 높음) + TilemapRenderer.sortingOrder 높음** → 시각적으로 위에 있는 타일 선택

### IsWalkableCell — 4단계 통과 판정

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
```

### IsHeightDiffValid — 층간 이동 제한

```csharp
float diff = Mathf.Abs(toMap.tileAnchor.y - fromMap.tileAnchor.y);
return diff < 0.55f;  // 0.55f 이상 차이나면 이동 불가
```

`tileAnchor` 수치 조정만으로 층 이동 제한을 코드 변경 없이 제어 가능하다.

### TileAnchor 좌표 보정

```csharp
Vector3 anchorOffset = grid.LocalToWorld(grid.CellToLocalInterpolated(map.tileAnchor))
                     - grid.LocalToWorld(grid.CellToLocalInterpolated(Vector3.zero));
Vector3 correctedPos = worldPos - anchorOffset;
```

### 헥스 6방향 오프셋 (짝수/홀수 행 분기)

```
짝수 행(y%2==0): NW(-1,1), NE(0,1), W(-1,0), E(1,0), SW(-1,-1), SE(0,-1)
홀수 행(y%2==1): NW(0,1),  NE(1,1), W(-1,0), E(1,0), SW(0,-1),  SE(1,-1)
```

### 물리 거리 필터 (BFS 오작동 방지)

BFS 탐색 중 실제 월드 거리가 `2.0f`를 초과하면 건너뜀. 다층 맵에서 다른 층의 타일이 잘못 인접 셀로 포함되는 경우를 방지하는 안전장치.

### FindPathToAdjacentCell

상자(`BoxInteract`), 퍼즐 박스 등 인접 상호작용용 특수 메서드. 목표 셀 주변 6개 인접 셀 각각에 `FindPath`를 시도하고 최단 경로를 반환한다.

---

## 5. 활기(Vigor) 시스템

```
VigorManager (Singleton)
├── maxVigor = 30 (기본값)
├── costMovePerTile = 2     (타일 이동 1칸)
├── costInspectBox = 1      (상자 검사)
├── costTriggerTrap = 5     (함정 발동)
└── costPushBoxPerTile = 3  (상자 밀기 1칸)

원자적(Atomic) 연산:
└── 이동 중 중복 차감 방지 보장

과중량(Overweight) 처리:
└── InventoryManager 아이템 수 >= overweightThreshold(10) 시
    ExplorationStatusManager.AddStatus(ExplorationStatusID.Overweight) 추가
    → 이동 비용 증가 효과 (UI에 상태 아이콘 표시)

Vigor 고갈 시: onExplorationFailed UnityEvent 발동 → TitleScene 이동
```

---

## 6. 안개(Fog) 시스템

`ExplorationFogManager`가 탐험 중 방문하지 않은 타일에 안개를 덮는다.

- 플레이어 이동 시 주변 타일의 안개를 걷어냄
- 방문 기록은 `ExplorationSnapshot`에 저장(영속성 보장)
- 씬 복귀 시 방문했던 타일은 안개가 걷어진 상태로 복원

---

## 7. 탐험 영속성 (Persistence)

### ExplorationSnapshot 구조

씬 전환 시 `SceneTransitionManager`에 저장된다.

```
ExplorationSnapshot
├── persistableStates: Dictionary<string, bool>  // 오브젝트 개방/소멸 여부
└── visitedTiles: HashSet<Vector3Int>             // 방문한 타일 좌표
```

### IExplorationPersistable 인터페이스

구현 오브젝트 (상자, 함정, 조우 등):

- 고유 `PersistID`를 가짐
- `SaveState()`, `RestoreState()` 메서드로 직렬화/역직렬화
- Addressables를 통한 비동기 스폰도 지원

### 복원 흐름

```
[전투 진입 시]
  SceneTransitionManager.SaveSnapshot()
  → ExplorationSnapshot 저장 (persistableStates + visitedTiles)

[전투 복귀 시]
  ExplorationPersistenceManager.RestoreSnapshot(snapshot)
  → 오브젝트별 PersistID로 상태 복원
  → 방문 타일 안개 상태 복원
```

---

## 8. QTE 시스템

`ExplorationQTEManager`가 탐험 중 특정 이벤트에서 QTE를 처리한다.

- `BaseQTEController`를 상속하는 `SimpleQTEController`가 UI와 연동
- 성공/실패에 따른 보상 차등 지급 구현

---

## 9. 탐험 상태 (ExplorationStatus)

`ExplorationStatusID` enum으로 탐험 상태를 정의하고, `ExplorationStatusManager`가 중첩(스택) 기반으로 관리한다.

| 상태            | 효과                    |
| --------------- | ----------------------- |
| `Overweight`    | 과중량 — 이동 비용 증가 |
| (기타 상태이상) | UI 상태 아이콘 표시     |

---

## 10. 상호작용 오브젝트 분류

`Interactables/` 폴더 구조:

```
Interactables/
├── Core/       IInteractable.cs 등 기반 인터페이스
├── Encounters/ 몬스터 조우 (EncounterMonster)
├── Props/      BoxInteract, PortalController, BarrierController 등
├── Puzzles/    PushObject, PuzzleBox, BoxGoal
├── Spawners/   오브젝트 스포너
└── Traps/      TrapBehavior, WebTrapController
```

### 오브젝트 카테고리 상세

| 카테고리 | 주요 클래스                                      | 기능                                       |
| -------- | ------------------------------------------------ | ------------------------------------------ |
| 채집     | `GatherableObject`                               | 자원 채집, 채집 가능 횟수 관리             |
| 상자     | `BoxInteract`                                    | 아이템 획득, RewardTableSO 연동, 팝업 연출 |
| 퍼즐     | `PushObject`, `PuzzleBox`, `BoxGoal`             | 소코반 스타일 상자 밀기 퍼즐               |
| 함정     | `TrapBehavior`, `WebTrapController`              | 피해/상태이상 부여                         |
| 몬스터   | `EncounterMonster`                               | 접촉 시 전투 진입 트리거                   |
| 포탈     | `PortalController`, `ExitHiddenPortalController` | 맵 이동                                    |
| 배리어   | `BarrierController`                              | 조건부 통로 차단                           |

### IInteractable 인터페이스

`IInteractable.cs`에 정의된 공통 인터페이스. 모든 상호작용 오브젝트가 구현한다.

---

## 11. 맵 전환 시스템

`MapTransitionManager.cs`가 맵 간 전환을 담당한다.

- 포탈 통과 시 다음 맵으로 전환
- `MapConnectionData`로 포탈 연결 정보 및 스폰 위치 관리
- `MapToggleManager.cs`가 미니맵 UI 표시/숨김 처리

---

## 12. 씬 전환 및 데이터 지속성

`SceneTransitionManager` (DontDestroyOnLoad)가 씬 간 모든 데이터를 관리한다.

### 전투 진입 시 저장 데이터

| 필드                          | 내용                       |
| ----------------------------- | -------------------------- |
| `explorationSnapshot`         | 맵 오브젝트 상태 스냅샷    |
| `pendingReturnScene`          | 복귀할 씬 이름             |
| `pendingReturnPosition`       | 복귀 위치 좌표             |
| `savedVigor`                  | 활기 스냅샷                |
| `pendingResumeCells`          | 전투 후 이어서 이동할 경로 |
| `pendingPlannedMoveVigorCost` | 계획된 이동 활기 비용      |
| `pendingRewards`              | 전투 보상                  |

### 전투 복귀 시 처리 순서

```
1. pendingRewards 보상 지급
2. PlayerDataManager.SyncFromBattle() — 유닛 HP/MP/Rage 동기화
3. RestoreSnapshot() — 맵 오브젝트 상태 복원
4. 안개 상태 복원
5. 활기(Vigor) 복원
```

---

_Part 2 Exploration System Detail — Lemegeton Project Documentation — 2026-03-04_
