# Part 4: UI / 코어 인프라 / 데이터 파이프라인 — 상세 분석

**분류:** 파트4 — UI · 코어 인프라 · 데이터 파이프라인
**작성일:** 2026-03-04
**참조 파일:** `Assets/01_Scripts/UI/`, `Assets/01_Scripts/Core/`, `Assets/Editor/`
**관련 문서:** Research.md §10~13, Lemegeton_Analysis_Report.md §3.4~3.5

---

## 목차

1. [UI 시스템 전체 구조](#1-ui-시스템-전체-구조)
2. [캠프 UI 시스템 (Camp UI)](#2-캠프-ui-시스템-camp-ui)
3. [전투 UI 시스템](#3-전투-ui-시스템)
4. [탐험 UI 시스템](#4-탐험-ui-시스템)
5. [팝업 / 모달 시스템](#5-팝업--모달-시스템)
6. [FloatingText 시스템](#6-floatingtext-시스템)
7. [코어 인프라 — 씬 전환 및 싱글톤 구조](#7-코어-인프라--씬-전환-및-싱글톤-구조)
8. [GameEventBus (타입 안전 이벤트 버스)](#8-gameeventbus-타입-안전-이벤트-버스)
9. [카메라 시스템](#9-카메라-시스템)
10. [ResourceTracker (Addressables 핸들 관리)](#10-resourcetracker-addressables-핸들-관리)
11. [에디터 도구 및 데이터 파이프라인](#11-에디터-도구-및-데이터-파이프라인)
12. [적용된 아키텍처 패턴 종합](#12-적용된-아키텍처-패턴-종합)
13. [현재 기술 부채 현황](#13-현재-기술-부채-현황)

---

## 1. UI 시스템 전체 구조

`UI/` 폴더는 9개의 하위 폴더로 세분화되어 있다.

```
UI/
├── Battle/      전투 UI (7개 파일)
├── Camp/        캠프 UI (22개 파일) — Status/Skill/Craft/Option 탭
├── Common/      공통 UI 컴포넌트 (12개 파일)
├── Core/        팝업 매니저, 모달 베이스 (6개 파일)
├── Exploration/ 탐험 UI (16개 파일)
├── Lobby/       로비 UI (빈 폴더)
├── Managers/    UI 매니저 (2개 파일)
├── Title/       타이틀 UI (1개 파일)
└── Utils/       UI 유틸리티 (3개 파일)
```

---

## 2. 캠프 UI 시스템 (Camp UI)

캠프 UI는 `ExplorationScene` 내에 모달로 내장되어 있다 (별도 씬 아님).
`Tab` 키로 열고 닫는다.

### 계층 구조

```
CampUIManager (ModalWindowBase 상속)
├── CampTabController       — 탭 전환 관리
├── CampHeaderController    — 상단 헤더 (캐릭터/아이템 선택)
├── CharacterHeaderController — 캐릭터 선택 UI
│
├── [Status 탭] CampStatusPage   — 캐릭터 스탯, 특성, 유대 정보
├── [Skill 탭]  CampSkillPage    — 스킬 슬롯 배치, 훈련 루트 선택, 패시브 해금
├── [Craft 탭]  CampCraftPage    — 아이템 제작
└── [Option 탭] CampOptionPage   — 게임 옵션
```

### CampSkillPage 상세 (1,090줄)

4종류 슬롯으로 구성:

| 슬롯 타입   | 클래스             | 역할             |
| ----------- | ------------------ | ---------------- |
| 스킬 슬롯   | `CampSkillSlot`    | 메인 스킬 배치   |
| 서브 슬롯   | `CampSubSkillSlot` | 서브 스킬 배치   |
| 패시브 슬롯 | `CampPassiveSlot`  | 패시브 표시/해금 |
| 훈련 슬롯   | `CampTrainingSlot` | 훈련 루트 선택   |

기타 UI 요소:

- `CampTraitSlot`: 특성 표시
- `CampBondHeart`: 유대 수치 하트 UI
- 드래그 앤 드롭으로 스킬 슬롯 간 이동 기능 구현

### FormationSlotUI

`FormationSlotUI`로 전투 진형을 편집. `PlayerDataManager.formation` 배열에 유닛을 배치/제거.

---

## 3. 전투 UI 시스템

`UI/Battle/` 폴더에 전투 중 표시되는 UI가 위치한다.

주요 UI 컴포넌트:

- 유닛 상태 패널 (HP/MP/ATB 게이지)
- 스킬 선택 패널
- 타겟 하이라이팅 시스템
- 웨이브 진행 표시
- 보상 표시 팝업 (`RewardPopupUI`)

---

## 4. 탐험 UI 시스템

`UI/Exploration/` 폴더에 탐험 중 표시되는 UI가 위치한다.

주요 UI 컴포넌트:

- 활기(Vigor) 게이지 UI
- 탐험 상태 아이콘 슬롯 (`ExplorationStatusSlot`)
- 미니맵 토글 (`MapToggleManager` 연동)
- 경로 마커 (`pathMarkerPrefab`, `goalMarkerPrefab`)

---

## 5. 팝업 / 모달 시스템

### PopupManager + ModalWindowBase

```
PopupManager
└── 씬 내 모달/팝업의 스택 관리

ModalWindowBase (부모 클래스)
├── CampUIManager
├── ConfirmationPopup
├── RewardPopupUI
└── (기타 팝업들)
```

팝업은 큐에 의해 순차 표시된다.

### 비동기 팝업 응답 지원

```csharp
// 비동기 팝업 예시
async UniTask<bool> ConfirmRetreatAsync()
{
    // 팝업을 열고 사용자 응답을 기다림
    bool result = await confirmPopup.ShowAsync();
    return result;
}
```

---

## 6. FloatingText 시스템

`FloatingTextManager`가 전투/탐험 중 화면에 부유하는 텍스트를 풀링하여 표시한다.

```
FloatingTextManager
├── Object Pooling — 텍스트 오브젝트 재사용 (GC 최소화)
└── FloatingTextDef SO
    ├── color     — 텍스트 색상
    ├── fontSize  — 폰트 크기
    └── animType  — 애니메이션 종류
```

사용 예시:

```csharp
FloatingTextManager.Show(worldPos, $"{damage}", FloatingTextType.Damage);
FloatingTextManager.Show(worldPos, "CRITICAL!", FloatingTextType.Critical);
```

---

## 7. 코어 인프라 — 씬 전환 및 싱글톤 구조

### Core 폴더 구조

```
Core/
├── Definitions/   SceneName enum 등 정의 (3개 파일)
└── System/        핵심 싱글톤 매니저 (11개 파일)
```

### 주요 싱글톤 매니저

| 매니저                     | DontDestroyOnLoad | 역할                             |
| -------------------------- | :---------------: | -------------------------------- |
| `SceneTransitionManager`   |        ✅         | 씬 페이드, 복귀 컨텍스트 저장    |
| `PlayerDataManager`        |        ✅         | 유닛 소유, 진형, HP/MP/Rage 관리 |
| `InventoryManager`         |        ✅         | 인벤토리 슬롯, 저장              |
| `StageRuntimeContext`      |        ✅         | 스테이지 런타임 컨텍스트         |
| `GameResetter`             |         -         | 게임 전체 초기화                 |
| `BattleManager`            |      씬 범위      | 전투 최상위 오케스트레이터       |
| `MapManager`               |      씬 범위      | 탐험 맵 오케스트레이터           |
| `VigorManager`             |      씬 범위      | 활기 자원 관리                   |
| `ExplorationFogManager`    |      씬 범위      | 안개 시스템                      |
| `ExplorationStatusManager` |      씬 범위      | 탐험 상태이상                    |
| `PopupManager`             |      씬 범위      | 팝업 스택 관리                   |

> ⚠️ **주의:** DontDestroyOnLoad 매니저와 씬 범위 매니저가 혼재(총 26개+). 씬 재진입 시 생명주기 관리 주의 필요. 장기적으로 DI 컨테이너(Zenject 등) 도입 검토.

### SceneTransitionManager 핵심 역할

페이드 전환(CanvasGroup 알파)과 로딩 프로그레스바를 제공.

전투 진입 시 저장:

- `explorationSnapshot` — 맵 오브젝트 상태
- `pendingReturnScene` — 복귀할 씬 이름
- `pendingReturnPosition` — 복귀 위치
- `savedVigor` — 활기 스냅샷
- `pendingResumeCells` — 전투 후 이어서 이동할 경로
- `pendingPlannedMoveVigorCost` — 계획된 이동 활기 비용
- `pendingRewards` — 전투 보상

### StageRuntimeContext

스테이지 진행 중 공유해야 하는 런타임 컨텍스트 데이터 보관 싱글톤.

---

## 8. GameEventBus (타입 안전 이벤트 버스)

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

### 현재 구현 범위

`UnitDamagedEvent` 등 일부 이벤트가 정의되어 있다. 대부분의 이벤트는 아직 매니저 간 직접 C# event로 처리하고 있으며, EventBus 방식으로의 점진적 마이그레이션이 권장된다.

---

## 9. 카메라 시스템

두 컴포넌트로 분리된 카메라 시스템:

| 컴포넌트              | 역할                                       |
| --------------------- | ------------------------------------------ |
| `CameraFollow2D`      | 기본 타겟 팔로우                           |
| `CameraDynamicOffset` | 플레이어 이동 방향에 따른 동적 오프셋 추가 |

---

## 10. ResourceTracker (Addressables 핸들 관리)

`PlayerDataManager`에 도입된 유틸 클래스. Addressables 핸들 누수를 방지한다.

```csharp
class ResourceTracker
{
    void Track(AsyncOperationHandle handle);
    void ReleaseAll();  // OnDestroy 시 호출
}
```

`ExplorationPersistenceManager` 내장을 제거하고 전역 `ResourceTracker`로 관리 일원화 완료.

---

## 11. 에디터 도구 및 데이터 파이프라인

`Editor/` 폴더 및 `Assets/Editor/` 폴더에 위치.

### CSV → ScriptableObject 자동 임포터

`GatherableDataImporter`가 CSV 파일을 읽어 `GatherableDataSO`를 자동 생성한다.

### UniversalDataImporter

범용 데이터 임포터. 다양한 형식의 외부 데이터를 ScriptableObject로 변환하는 파이프라인을 제공한다.

### MapDataAutoSetupTool

전용 에디터 윈도우를 통해 맵 데이터를 자동으로 설정한다.

### MapIDBaker

맵 프리팹에 고유 ID를 자동으로 부여하는 에디터 도구.

### AddressablesBuilder

에디터 메뉴에서 Addressables 빌드를 트리거하는 커스텀 빌더 스크립트.

### MissingScriptFinder

씬/프리팹에서 누락된 스크립트 컴포넌트를 검색하는 에디터 유틸리티.

---

## 12. 적용된 아키텍처 패턴 종합

| 패턴                  | 적용 위치                                                   | 평가                         |
| --------------------- | ----------------------------------------------------------- | ---------------------------- |
| Singleton             | BattleManager, PlayerDataManager 등 26개+                   | ⚠️ 과다 — 씬/영구 구분 필요  |
| FSM                   | BattleStateMachine / BattleBaseState / BattleConcreteStates | ✅ 적절                      |
| Command               | CommandQueue + ICommand / MoveCommand / SkillCommand        | ✅ 적절                      |
| Observer              | GameEventBus (정적) + 직접 C# 이벤트 혼용                   | ✅ 좋음 (EventBus 확장 여지) |
| Strategy              | SkillAsset 상속 계층 (Parametric* / Self* / Tactics\*)      | ✅ 적절                      |
| Template Method       | EnemySkill → EnemyBasicAttack / WebCastWebTrap              | ✅ 적절                      |
| Data-Driven SO        | SkillAsset / UnitData / RewardTableSO 등                    | ✅ 매우 좋음                 |
| Interface Segregation | IInventory / IGridProvider / IMapComponent 등               | ✅ 좋음                      |
| Repository / DB       | StateStatModifierDB / TrainingDB / StatusDescriptionDB      | ✅ 적절                      |
| Snapshot              | ExplorationPersistenceManager + ExplorationSnapshot         | ✅ 적절                      |
| Object Pool           | FloatingTextManager (텍스트 풀링)                           | ✅ 적절                      |
| Facade                | BattleUnit (UnitStats/UnitMover/UnitVisual 통합 제공)       | ✅ 적절                      |

---

## 13. 현재 기술 부채 현황

### 해결 완료 항목

| 항목                                                           | 상태    |
| -------------------------------------------------------------- | ------- |
| `async void` 5곳 → `async UniTaskVoid` + try-catch 전환        | ✅ 완료 |
| `.gitattributes` CRLF 설정 + 인코딩 일괄 정리                  | ✅ 완료 |
| 저장 시스템 일원화 (PlayerPrefs → persistentDataPath JSON)     | ✅ 완료 |
| `FindObjectsOfType` 잔존 17곳 → `_activeUnits` 레지스트리 교체 | ✅ 완료 |
| `PlayerMovement.cs` 책임 분리                                  | ✅ 완료 |
| FSM 레거시 방식과 완전 통합                                    | ✅ 완료 |
| 개발용 플래그 `#if UNITY_EDITOR` 격리                          | ✅ 완료 |
| `07_Test` 폴더 `.asmdef` 분리 및 빌드 제외                     | ✅ 완료 |
| Addressables 핸들 관리 ResourceTracker로 일원화                | ✅ 완료 |
| 비동기 패턴 Coroutine → UniTask 단계적 통일                    | ✅ 완료 |

### 미해결 / 진행 중 항목

| 항목                                                                         | 우선순위 | 예상 공수 |
| ---------------------------------------------------------------------------- | -------- | --------- |
| `LuckySixShootingInsightPassive` TODO 3개 구현                               | 🟡 P2    | 2~3일     |
| `BattleUnit.cs` 추가 분리 (1,909줄 — UnitStats/UnitMover/UnitVisual 진행 중) | 🟡 P2    | 3~5일     |
| `BattleManager.cs` 오케스트레이터 역할 축소 (1,313줄)                        | 🟡 P2    | 3~5일     |
| `CampSkillPage.cs` 분리 (1,090줄)                                            | 🟡 P2    | 2~3일     |
| 단위 테스트 작성 (데미지 계산, ATB 순서)                                     | 🔵 P3    | 5일~      |
| 과도한 싱글톤 구조 개선 (26개+)                                              | 🔵 P3    | 장기      |
| 오디오 시스템 부재 — AudioManager 미구현                                     | 🔴 P1    | 3~5일     |

> ⚠️ **중요:** 오디오(Sound) 시스템이 현재 전무하다. `OptionsMenuUI`에 볼륨 컨트롤 슬롯이 있으나 연결된 AudioManager가 없다. 게임 완성도를 위해 조속한 구현이 필요하다.

---

## 종합 평가 (2026-03-04 기준)

| 평가 항목       | 점수  | 현황                                                        |
| --------------- | ----- | ----------------------------------------------------------- |
| 아키텍처 구조   | ★★★★☆ | FSM, Command, Observer 패턴 적절 적용. 서브시스템 분리 명확 |
| 코드 가독성     | ★★★★☆ | 한국어 주석 철저, 리팩토링으로 God Object 축소              |
| 데이터 설계     | ★★★★☆ | SO 기반 데이터 주도 설계 우수, CSV 파이프라인 자동화        |
| 안정성/예외처리 | ★★★★☆ | async void 해결, 저장 시스템 안정화 완료                    |
| 성능 관리       | ★★★★☆ | FindObjectsOfType 완전 제거, 레지스트리/캐시 일원화 완료    |
| 테스트 가능성   | ★★☆☆☆ | 싱글톤 26개+, 단위 테스트 전무                              |
| 문서화          | ★★★★★ | 기술 문서 26개, 한국어 주석 철저, 구현 계획서 별도 관리     |
| 확장성          | ★★★★☆ | 인터페이스 분리, Strategy 패턴, 스킬 SO 시스템 설계 우수    |

---

_Part 4 UI / Core Infrastructure / Data Pipeline Detail — Lemegeton Project Documentation — 2026-03-04_
