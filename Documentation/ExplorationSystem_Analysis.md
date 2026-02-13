# 탐험 씬(ExplorationScene) 시스템 분석 보고서

본 문서는 `TitleScene`에서 `ExplorationScene`으로 전환될 때의 흐름, 맵 생성 및 배치 로직, 그리고 플레이어 이동 및 타일 판정 구조를 상세하게 분석한 내용입니다.

---

## 1. 씬 전환 및 초기화 (Title -> Exploration)

### 1.1 `TitleScene`에서의 시작

- **스크립트**: `TitleMenuUI.cs`
- **동작**: "새로하기" 버튼 클릭 시 `OnBtnStartGame()`이 호출됩니다.
- **로직**:
  1. `GameResetter.ResetAll(deleteSaves: true)`를 호출하여 기존 세이브 데이터를 초기화합니다.
  2. `SceneTransitionManager.Instance.FadeToScene("ExplorationScene")`를 호출하여 씬 전환을 시작합니다.

### 1.2 `ExplorationScene` 진입 및 맵 생성

- **매니저**: `MapManager.cs` (씬의 핵심 관리자)
- **맵 생성 흐름**:
  1. `MapManager.Start()`에서 `GenerateStageMap()`을 호출합니다.
  2. **프리팹 결정**:
     - `SceneTransitionManager`에 오버라이드된 맵(예: 전투 복귀 시)이 있다면 그것을 사용합니다.
     - 없다면 `StageDatabase`(ScriptableObject)에서 현재 스테이지(`currentStage`)에 맞는 `StageNormalMapData`를 조회하고, 그 안의 `normalMapPrefabs` 배열 중 하나를 랜덤으로 선택합니다.
     - **중요**: 맵은 런타임에 절차적 생성(Procedural Generation)되는 것이 아니라, **미리 제작된 프리팹(Prefab)**을 인스턴스화하는 방식입니다.
  3. **맵 로드 (`ExplorationMapLoader.cs`)**:
     - `mapLoader.LoadMap()`을 통해 선택된 프리팹을 `Grid` 오브젝트 하위에 생성(Instantiate)합니다.
     - 맵 오브젝트는 타일맵(Tilemap)들과 `PlayerStart` 지점, `MapObjectSpawner` 등을 포함하고 있습니다.

---

## 2. 맵 구조 및 오브젝트 배치 (Map Structure & Placement)

맵 프리팹이 생성된 후, `MapManager.ResetExplorationMap()` 메서드가 실행되며 본격적인 배치가 이루어집니다.

### 2.1 타일맵 식별 (Tilemap Layering)

맵 프리팹 내의 타일맵들은 **Tag**를 통해 역할이 구분됩니다.

- **Ground**: 바닥 타일 (이동 가능)
- **Obstacle**: 장애물 타일 (이동 불가)
- **Wall**: 벽 타일 (시각적 벽, 이동 불가)

### 2.2 플레이어 배치 (Player Placement)

- **담당**: `ExplorationEntitySpawner.cs`
- **로직**:
  1. 맵 프리팹 내부에서 `PlayerStart`라는 이름의 `Transform`을 찾습니다.
  2. 해당 위치(`spawn.position`)에 플레이어 프리팹을 생성합니다.
  3. 만약 `PlayerStart`가 없다면 `(0, 0, 0)` 좌표를 기본값으로 사용합니다.
  4. 생성 후 카메라인 `CameraFollow2D`가 플레이어를 추적하도록 설정합니다.

### 2.3 오브젝트 배치 (Object Placement)

- **담당**: `MapObjectSpawner.cs` (맵 프리팹에 부착된 컴포넌트)
- **배치 대상**: 함정(Trap), 상자(Chest), 문양(Pattern) 등
- **배치 알고리즘**:
  1. **후보지 선정 (`SpawnCandidate`)**:
     - `Ground` 태그가 달린 모든 타일맵의 좌표를 수집합니다.
     - **제외 조건**:
       - `Wall` 또는 `Obstacle` 타일이 있는 위치 제외.
       - `ExcludeSpawn` 태그가 달린 콜라이더(Collider2D)가 있는 영역 제외 (OverlapPoint로 검사).
  2. **무작위 배치**:
     - 수집된 유효한 바닥 좌표 리스트(`candidates`)에서 무작위 인덱스를 뽑아 오브젝트를 생성합니다.
     - 생성된 좌표는 리스트에서 제거하여 중복 생성을 방지합니다.

---

## 3. 이동 및 타일 판정 시스템 (Movement & Navigation)

플레이어의 이동은 `PlayerMovement.cs`에서 담당하며, Unity의 `Tilemap` 격자(Grid) 시스템을 기반으로 작동합니다.

### 3.1 이동 가능 여부 판정 (`IsWalkableCell`)

특정 타일 좌표(`Vector3Int cell`)로 이동 가능한지는 다음과 같이 판정합니다:

1. **바닥 존재 여부**: `floorTilemap` 또는 `floorMaps` 리스트에 해당 좌표에 타일이 존재해야 합니다.
2. **장애물 여부**: `obstacleMaps` 리스트에 해당 좌표에 타일이 없어야 합니다.
3. **물리적 충돌체**: `Physics2D` 검사를 통해 `impassableLayerMask` 레이어의 충돌체가 없어야 합니다.

### 3.2 높이 차이 판정 (`IsHeightDiffValid`)

- 타일맵에는 `tileAnchor.y` 값을 통해 높이(Height) 개념이 적용되어 있습니다.
- 이동하려는 타일과 현재 타일의 높이 차이를 계산하여, 일정 수치(약 0.55f ~ 0.6f) 이상 차이가 나면 이동 불가로 판정하거나, 점프(Jump) 동작을 수행합니다.

### 3.3 경로 탐색 (Pathfinding)

- **알고리즘**: 너비 우선 탐색(BFS) 변형을 사용합니다 (`FindPath` 메서드).
- **작동 방식**:
  1. 시작 타일에서 인접한 6방향(Hexagon/Isometric Grid 특성 반영)을 검사합니다.
  2. `IsWalkableCell`과 `IsHeightDiffValid`를 통과한 타일만 큐(Queue)에 넣습니다.
  3. 목표 지점까지의 최단 경로를 찾아 `List<Vector3Int>` 형태로 반환합니다.

### 3.4 이동 실행

- 코루틴 `Co_MoveAlongPath`를 통해 경로 리스트를 순차적으로 이동합니다.
- 각 타일 간 이동 시 `Vector3.Lerp`로 부드럽게 이동하며, 높이 차이가 있을 경우 `AnimationCurve`를 사용해 점프 효과를 연출합니다.
- 타일 도착 시 함정(`TrapBehavior`)이나 몬스터(`EncounterMonster`)가 있는지 확인하고 이벤트를 발생시킵니다.

## 4. 요약 플로우 (Summary Flow)

1. **[Start]** `TitleScene` -> "새로하기" 클릭
2. **[SceneLoad]** `ExplorationScene` 로드
3. **[MapGen]** `MapManager`: `StageDatabase`에서 프리팹 선택 -> `MapLoader`로 인스턴스화
4. **[Scan]** 생성된 맵에서 `Ground`, `Obstacle`, `Wall` 타일맵 식별
5. **[Spawn]**
   - `EntitySpawner`: `PlayerStart` 위치에 플레이어 생성
   - `MapObjectSpawner`: 빈 바닥(`Ground`)을 찾아 상자/함정 랜덤 배치
6. **[Play]** `PlayerMovement`: 타일 클릭 -> BFS 경로 탐색 -> 이동 실행

---

## 5. 개선 제안 (Improvement Proposals)

현재 분석 내용을 바탕으로, 유지보수성과 확장성을 높이기 위한 구조적 개선 제안을 정리합니다.

### 5.1 `PlayerMovement` 클래스의 단일 책임 원칙 (SRP) 위반 해결

- **문제점**: 현재 `PlayerMovement` 클래스는 **약 3500줄**에 달하며, 이동 로직뿐만 아니라 `Input` 처리, `UI` 표시(힌트, 경로), `상호작용`, `함정/몬스터 체크`, `애니메이션` 등을 모두 포함하고 있습니다. 이는 코드 수정 시 사이드 이펙트를 발생시키기 쉽고 디버깅을 어렵게 만듭니다.
- **개선 방안**: 기능을 분리하여 전담 클래스(Controller/Manager)로 위임합니다.
  - **`PlayerInputController`**: 키보드/마우스 입력을 감지하고 `PlayerMovement`에 명령만 전달.
  - **`InteractionController`**: 상자, 포탈, NPC와의 상호작용 및 UI 힌트 표시 로직 전담.
  - **`Pathfinder`**: A\* 또는 BFS 알고리즘을 별도 유틸리티 클래스나 싱글톤으로 분리하여 재사용성 증대.
  - **`PlayerMovement`**: 순수하게 이동(좌표 변경, 물리 연산, 애니메이션 동기화)에만 집중.
- **기대 효과**:
  - 각 기능별 코드가 간결해져 버그 추적이 용이해짐.
  - 입력 방식(PC/모바일 등) 변경 시 `InputController`만 교체하면 됨.

### 5.2 매직 스트링(Magic String) 및 태그 의존성 제거

- **문제점**: `MapManager`나 `PlayerMovement`에서 `"Ground"`, `"Obstacle"`, `"Wall"`과 같은 문자열 리터럴(Magic String)을 직접 사용하고 있습니다. 오타가 발생하면 찾기 힘들고, 태그 이름 변경 시 모든 코드를 수정해야 합니다.
- **개선 방안**:
  - **`MapConfig` (ScriptableObject)**: 맵 생성에 필요한 태그, 레이어 이름, 타일맵 정렬 순서(Sorting Order) 등을 관리하는 설정 파일을 만듭니다.
  - **`LayerMask` 활용**: 코드에서 문자열로 레이어를 찾는 대신, 인스펙터에서 `LayerMask`를 할당받아 비트 연산으로 처리합니다 (성능 향상).
  - **상수 클래스 (`Constants.cs`)**: 모든 태그와 레이어 이름을 `public const string`으로 관리합니다.
- **기대 효과**:
  - 문자열 오타로 인한 런타임 에러 방지.
  - 설정 파일 하나만 수정하면 프로젝트 전체에 반영되므로 유지보수 용이.

### 5.3 맵 데이터 관리의 구조화 (Structured Map Data)

- **문제점**: 현재 `MapManager`는 맵 프리팹을 인스턴스화한 뒤, `GetComponentsInChildren<Tilemap>()`과 태그 비교를 통해 바닥/벽을 식별합니다. 맵 구조가 복잡해지거나 계층이 바뀌면 로직이 깨질 수 있습니다.
- **개선 방안**:
  - **`MapData` 컴포넌트**: 맵 프리팹의 루트에 `MapData`라는 스크립트를 부착하고, `Inspector`에서 미리 `FloorTilemap`, `WallTilemap`, `NormalObjectSpawnRoots` 등을 연결해둡니다.
  - `MapManager`는 런타임에 태그를 검색하는 대신, `MapData` 컴포넌트에 접근하여 즉시 필요한 참조를 가져옵니다.
- **기대 효과**:
  - 런타임 검색 비용(Search Cost) 제거로 씬 로딩 속도 향상.
  - 맵 제작자가 인스펙터에서 직접 연결하므로 구조가 명확해짐.
