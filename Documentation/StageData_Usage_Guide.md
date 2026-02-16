# 스테이지 데이터 시스템 사용 가이드 (Stage Data System Usage Guide)

본 가이드는 리팩토링된 스테이지 시스템에서 **맵 데이터(`StageNormalMapData`)를 생성하고 설정하는 방법**을 설명합니다.

## 1. 스테이지 데이터 생성 및 설정

### 1) 데이터 파일 생성

Project 창에서 우클릭 -> `Create` -> `Data` -> `Stage` -> `NormalMap`을 선택하여 새로운 스테이지 데이터 파일을 생성합니다.

### 2) 기본 정보 설정 (`Basic Info`)

- **Stage Number**: (레거시 호환용) 정수형 스테이지 번호를 입력합니다. (예: 1)
- **Stage Id**: **[중요]** 스테이지를 식별하는 고유한 문자열 ID를 입력합니다. (예: `Chapter1_Stage1`, `1-1`)
  - 이 ID는 포탈 이동 및 웨이브 로드 시 최우선으로 사용됩니다.
- **Normal Map Prefabs**: 탐험 씬에서 사용될 맵 프리팹들을 등록합니다.

### 3) 전투 웨이브 설정 (`Waves`)

기존의 고정된 필드(`Trap`, `Puzzle`) 대신, **`Context Waves` 리스트**를 사용하여 상황별 전투를 유연하게 등록합니다.

1.  `Context Waves` 리스트의 `+` 버튼을 눌러 항목을 추가합니다.
2.  **Context Type**: 전투가 발생하는 상황을 선택합니다.
    - `TrapEncounter`: 함정 게이지가 가득 차서 전투에 진입하는 경우.
    - `AfterPuzzle`: 퍼즐을 풀고 나서 전투에 진입하는 경우.
3.  **Wave Set**: 해당 상황에서 로드될 `WaveSet` 데이터(적 등장 구성)를 연결합니다.

> **참고**: 더 이상 `Legacy` 항목의 `Trap Encounter Wave`, `Post Puzzle Wave` 필드는 사용되지 않으며, 코드에서 삭제되었습니다.

---

## 2. 포탈(Portal) 설정

탐험 씬에서 전투 씬으로 넘어가는 포탈 오브젝트의 설정 방법입니다.

1.  씬에 배치된 **Portal** 오브젝트를 선택합니다.
2.  `PortalController` 컴포넌트 설정을 확인합니다.
3.  **Current Stage Data**: 위에서 생성한 `StageNormalMapData` 에셋을 드래그하여 연결합니다.
    - 포탈을 타면 연결된 데이터의 `Stage Id`가 시스템에 등록됩니다.
4.  **Battle Context When Used**: 이 포탈을 통해 전투 씬으로 갔을 때 어떤 상황(`Context Type`)으로 간주할지 선택합니다.
    - 예: 퍼즐 방 뒤에 있는 포탈이라면 `AfterPuzzle`로 설정.

---

## 3. 작동 원리 (참고)

1.  플레이어가 **포탈**을 이용하면, 연결된 `StageNormalMapData`의 `Stage Id`와 설정된 `Context Path`가 `StageRuntimeContext`에 저장됩니다.
2.  **전투 씬**(`BattleScene`)이 로드됩니다.
3.  `BattleWaveManager`는 저장된 `Stage Id`를 이용해 **`StageDatabase`**에서 일치하는 스테이지 데이터를 찾습니다.
4.  찾은 스테이지 데이터의 `Context Waves` 리스트에서, 현재 상황(`Context`)에 맞는 `WaveSet`을 찾아 전투를 시작합니다.
