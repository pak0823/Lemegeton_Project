# Lemegeton_Project 프로젝트 분석 보고서

## 1. 프로젝트 개요 (Project Overview)

- **프로젝트 명**: Lemegeton_Project
- **Unity 버전**: 2022.3.62f1
- **장르**: 2D 퍼즐/어드벤처 (타일맵 기반)
- **주요 특징**: Hexagonal/Grid 타일 시스템, 퍼즐 박스 상호작용, 랜덤 맵 생성

## 2. 폴더 및 파일 구조 (Directory Structure)

주요 로직은 `Assets/Resources/Scripts` 내에 위치하며, 다음과 같은 구조를 가집니다:

- **Common/**: 공용 유틸리티 및 Enum 정의 (`Enum.cs`, `Shared.cs`, `TileCounter.cs`)
- **Data/**: 게임 데이터 관리 (`StageDataBase.cs`, `DebuffData.cs`)
- **Manager/**: 게임 시스템 관리자
  - `MapManager.cs`: 맵 생성 및 플레이어 스폰 관리
  - `PuzzleManager.cs`: 퍼즐 로직 처리 (박스 이동 등)
  - `UIManager.cs`: UI 흐름 관리
  - `ObjectGaugeManager.cs`, `MapToggleManager.cs`
- **Object/**: 게임 내 상호작용 객체
  - `PuzzleBox.cs`, `BoxInteract.cs`: 퍼즐 박스 로직
  - `BarrierController.cs`, `TrapBehavior.cs`: 장애물 및 함정
  - `MapObjectSpawner.cs`: 오브젝트 스폰
- **Player/**: 플레이어 관련 로직
  - `PlayerMovement.cs`: A\* 알고리즘 기반 이동 및 상호작용
  - `PlayerDebuffController.cs`
- **UI/**: 사용자 인터페이스 (`UI_Lobby.cs`, `RegionButtonManager.cs` 등)
- **Assets/StoneUI**: UI 관련 리소스(텍스처, 프리팹)가 포함된 것으로 추정되는 디렉토리

## 3. 주요 스크립트 및 로직 분석 (Key Components Analysis)

### 3.1. MapManager (`Manager/MapManager.cs`)

- **역할**: 게임의 맵을 생성하고 초기화합니다.
- **주요 기능**:
  - `GenerateRandomMap()`: 사전 정의된 `mapPrefabs` 중 하나를 랜덤으로 선택하여 인스턴스화합니다.
  - **Tilemap 탐색**: 생성된 맵에서 "Floor"와 "Wall" 이름을 가진 Tilemap을 자동으로 찾아 참조합니다.
  - **Player Spawn**: `PlayerStart` 위치를 찾아 플레이어를 생성하고, 타일맵 정보를 `PlayerMovement`에 전달합니다.
  - **Grid 관리**: `MapToggleManager`와 연동하여 맵과 그리드를 관리합니다.

### 3.2. PlayerMovement (`Player/PlayerMovement.cs`)

- **역할**: 플레이어의 입력 처리 및 이동 로직을 담당합니다.
- **주요 기능**:
  - **A\* Pathfinding**: `FindPath()` 메서드를 통해 목표 지점까지의 최단 경로를 계산하여 이동합니다. `WorldToCell`을 사용하여 월드 좌표를 그리드 좌표로 변환합니다.
  - **Hex/Grid 이동**: 6방향(Hex) 또는 4방향 이동을 고려한 `GetNeighbors()` 로직이 포함되어 있습니다.
  - **상호작용 (Push)**: `PerformPush()` 코루틴을 통해 `PuzzleBox`를 미는 동작을 부드럽게(Lerp) 처리합니다.
  - **충돌 감지**: `IsWalkableCell()` 및 `HasImpassableObject()`를 통해 이동 가능 여부를 판단합니다.

### 3.3. StageDatabase (`Data/StageDataBase.cs`)

- **역할**: ScriptableObject로 구현된 스테이지 데이터 컨테이너입니다.
- **기능**: `StageQuizMapData` 배열을 보유하여 여러 스테이지의 정보를 관리합니다.

## 4. 게임 플레이 메커니즘 (Gameplay Mechanics)

1. **타일 기반 이동**: 플레이어는 연속적인 공간이 아닌 타일(Cell) 단위로 이동하며, 이동 시 A\* 알고리즘을 통해 경로를 탐색합니다.
2. **퍼즐 상호작용**: '박스 밀기'와 같은 퍼즐 요소가 있으며, 이는 `PuzzleManager`와 `PlayerMovement` 간의 연동으로 처리됩니다.
3. **랜덤 맵**: 매 게임 시작 시마다 프리팹 목록에서 맵을 랜덤하게 선택하여 다양성을 제공합니다.
4. **장애물 및 함정**: `Barrier`와 `Trap` 오브젝트가 존재하여 플레이어의 이동을 제한하거나 도전 요소를 제공합니다.

## 5. 결론 (Conclusion)

이 프로젝트는 Unity의 Tilemap 시스템을 적극 활용한 2D 퍼즐 게임으로, 체계적인 매니저 클래스(`MapManager`, `PuzzleManager`)와 객체 지향적인 오브젝트 설계가 돋보입니다. 특히 A\* 알고리즘을 직접 구현하여 타일 기반의 정교한 이동을 제어하고 있습니다.
