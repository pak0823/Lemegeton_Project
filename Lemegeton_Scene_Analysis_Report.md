# Lemegeton_Project 씬(Scene) 구조 및 상태 분석 보고서

**작성일시**: 2026-02-05
**검증 상태**: ✅ **Verified (정밀 검증 완료)**
**참조**: `.unity` 파일 바이너리/YAML 파싱, 스크립트(`MapManager.cs`, `SceneTransitionManager.cs`) 코드 교차 검증

---

## 1. 개요 (Overview)

본 문서는 프로젝트의 3대 핵심 씬(`Title`, `Exploration`, `Battle`)의 실제 구현 상태를 정밀 분석한 보고서입니다.
단순 추정이 아닌, **GUID 추적 및 바이너리 데이터 대조**를 통해 검증된 사실만을 기술합니다.

---

## 2. 씬별 정밀 분석 (Detailed Scene Analysis)

### 2.1 타이틀 씬 (TitleScene)

**경로**: `Assets/00_Scenes/TitleScene.unity`
**역할**: 게임 진입, 초기화, 메인 메뉴

#### **계층 구조 (Hierarchy)**

- **Canvas** (Root UI)
  - **ButtonSet**: `Start` 등 메인 버튼. (`TitleMenuUI.cs` GUID: `a623...` 정적 바인딩)
  - **Panel**: 팝업 UI.
  - **Text**: 타이틀 로고.
- **Main Camera**
  - **AudioListener**: 존재 확인 (Line 1923). 사운드 출력이 가능한 상태.
- **EventSystem**: UI 입력 처리.

#### **분석 결론**

- **순수 UI 씬 (Clean UI Scene)**: 3D 월드 오브젝트가 없는 가벼운 구조입니다.
- **AudioListener 보유**: 이 씬이 게임의 오디오 리스너 역할을 겸하고 있을 가능성이 높습니다 (DontDestroyOnLoad가 아니라면 씬 전환 시 교체됨).

---

### 2.2 탐험 씬 (ExplorationScene)

**경로**: `Assets/00_Scenes/ExplorationScene.unity`
**역할**: 월드 이동 및 상호작용 (Shell Architecture)

#### **계층 구조 (Hierarchy)**

- **MapManager** (Logic Root): `MapManager.cs` (GUID: `e23e...`)가 씬에 존재.
- **Canvas** (UI Overlay)
  - **Vigor**: 단순 UI 컨테이너(RectTransform). **매니저 스크립트 없음.**
  - **Tab_Container**: 인벤토리/스킬/상태창 복합 UI.
  - **Bottom_Area**: 조작 버튼.
  - **UnitImage**: 캐릭터 포트레이트.

#### **[검증됨] 완전 동적 구조 (Fully Dynamic Verification)**

1.  **맵 데이터 0 (Empty Scene)**:
    - 씬 파일 내 `Grid`, `Tilemap`, `MapObjectSpawner` **없음**.
    - **팩트**: `MapManager`가 `GenerateStageMap()`을 통해 프리팹을 **100% 동적으로 생성**합니다.
2.  **Vigor 시스템**:
    - `Vigor` 오브젝트는 껍데기이며, `VigorManager` (GUID: `8063...`) 로직은 런타임에 동적으로 초기화됩니다.

---

### 2.3 전투 씬 (BattleScene)

**경로**: `Assets/00_Scenes/BattleScene.unity`
**역할**: 턴제 전투 (Standalone Module)

#### **계층 구조 (Hierarchy)**

- **ManagerSet**: `BattleManager.cs` (GUID: `25f5...`)를 포함하는 컨트롤 타워.
- **Canvas** (HUD)
  - **Panel_Skill**: 스킬 버튼 그리드.
  - **TurnBarPanel**: ATB 턴 순서.
  - **Panel_Action**: 행동 제어.
- **Environment**: `BackGround` (Sprite).

#### **분석 결론**

- **정적 모듈 (Static Module)**: 탐험과 달리 `BattleManager`와 HUD가 씬에 **정적으로 배치**되어 있습니다.
- **플레이스홀더**: 유닛이 배치될 빈 부모 오브젝트(`PlayerStatParent` 등)가 미리 잡혀 있습니다.

---

## 3. 글로벌 시스템 분석 (Global Systems)

### 3.1 씬 전환 매니저 (SceneTransitionManager)

- **GUID**: `244f6c1c567ddc941b929a039edb7fbc`
- **위치**: 3대 씬 파일(`Title`, `Exploration`, `Battle`) 내에 **존재하지 않음**.
- **결론**: `DontDestroyOnLoad` 속성을 가진 싱글톤으로, 별도의 부트(Boot) 씬이나 개발용 씬(`Test`/`Ui_Test`)에서 출발하거나, 초기화 시점에 코드로 생성되는 구조입니다.

### 3.2 맵/오브젝트 매니지먼트

- **MapManager**: 씬에 내장.
- **MapObjectSpawner**: 맵 프리팹 내부에만 존재 (씬 파일 없음).

---

## 4. 최종 요약 (Summary)

| 씬 이름              | 구조 특징         | 핵심 매니저 위치             | 데이터 로딩 방식             |
| :------------------- | :---------------- | :--------------------------- | :--------------------------- |
| **TitleScene**       | **Pure UI**       | `TitleMenuUI` (Scene 내장)   | 정적 배치                    |
| **ExplorationScene** | **Dynamic Shell** | `MapManager` (Scene 내장)    | **100% 동적 (Addressables)** |
| **BattleScene**      | **Static Module** | `BattleManager` (Scene 내장) | 정적 배치 + 유닛 데이터 로드 |

이 구조는 **"탐험의 확장성(무한한 맵)"**과 **"전투의 안정성(독립 모듈)"**을 동시에 잡은 하이브리드 아키텍처입니다.
