# Lemegeton — Reusable Systems Catalog

**작성일:** 2026-02-28
**기준 문서:** Research.md (2026-02-27 기준)
**목적:** 다른 Unity 2D 프로젝트에 이식 가능한 시스템, 스크립트, 에디터 도구 목록

> **이식 난이도 기준**
>
> - ⭐ 파일 복사 후 네임스페이스 변경만으로 사용 가능
> - ⭐⭐ 의존하는 소수 파일을 함께 복사하면 사용 가능
> - ⭐⭐⭐ 일부 로직 수정이 필요하거나 게임 도메인 결합도가 있음

---

## 목차

1. [코어 인프라](#1-코어-인프라)
2. [UI 공통 시스템](#2-ui-공통-시스템)
3. [인벤토리 및 아이템 시스템](#3-인벤토리-및-아이템-시스템)
4. [카메라 시스템](#4-카메라-시스템)
5. [탐험 / 이동 시스템](#5-탐험--이동-시스템)
6. [전투 상태이상 시스템](#6-전투-상태이상-시스템)
7. [에디터 도구](#7-에디터-도구)
8. [화면 설정 시스템](#8-화면-설정-시스템)
9. [이식 제외 항목](#9-이식-제외-항목)

---

## 1. 코어 인프라

### 1-A. GameEventBus — 타입 안전 이벤트 버스

| 항목            | 내용                                            |
| --------------- | ----------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Core/System/GameEventBus.cs` |
| **의존성**      | 없음 (완전 독립, Unity API 미사용)              |
| **이식 난이도** | ⭐                                              |

**기능 요약**

타입 기반의 경량 이벤트 버스. 퍼블리셔와 구독자를 직접 참조 없이 `Type`으로 연결한다.
구독 중 해제를 안전하게 처리하기 위해 역순 반복(`for (int i = list.Count - 1; ...)`)을 사용한다.

**사용 패턴**

```csharp
// 이벤트 구조체 정의
public struct UnitDamagedEvent { public BattleUnit Target; public int Amount; }

// 구독
GameEventBus.Subscribe<UnitDamagedEvent>(OnUnitDamaged);

// 발행
GameEventBus.Publish(new UnitDamagedEvent { Target = unit, Amount = 50 });

// 해제
GameEventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
```

**이식 방법**

1. `GameEventBus.cs` 파일을 복사한다.
2. 상단 `namespace`를 새 프로젝트 네임스페이스로 변경한다.
3. 이벤트 구조체(예: `UnitDamagedEvent`)는 별도 파일로 분리하거나 도메인 폴더에 정의한다.

---

### 1-B. SceneTransitionManager — 씬 전환 + 페이드 + 로딩 진행바

| 항목            | 내용                                                      |
| --------------- | --------------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Core/System/SceneTransitionManager.cs` |
| **의존성**      | UniTask, Addressables, `CanvasGroup` (UI)                 |
| **이식 난이도** | ⭐⭐                                                      |

**기능 요약**

`DontDestroyOnLoad` 싱글톤. 씬 전환 시 화면 페이드(CanvasGroup 알파 보간), 로딩 진행바, 씬 간 데이터 전달(pendingData 패턴)을 제공한다.

**이식 방법**

1. `SceneTransitionManager.cs`를 복사한다.
2. 프로젝트 고유 필드(`explorationSnapshot`, `pendingReturnPosition` 등)를 제거하고 필요한 페이로드 구조체만 남긴다.
3. 페이드 Canvas 프리팹을 새 프로젝트에 맞게 교체한다.

**주의사항**

- `pendingReturnScene`, `pendingRewards` 등 Lemegeton 고유 필드는 이식 시 제거 또는 범용화가 필요하다.

---

### 1-C. ResourceTracker — Addressables 핸들 누수 방지

| 항목            | 내용                                               |
| --------------- | -------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Core/System/ResourceTracker.cs` |
| **의존성**      | Addressables 패키지                                |
| **이식 난이도** | ⭐                                                 |

**기능 요약**

`Addressables.LoadAssetAsync` 결과 핸들을 내부 리스트로 추적하고, `ReleaseAll()`로 일괄 해제한다.
`OnDestroy` 훅에서 호출하면 핸들 누수를 방지할 수 있다.

**이식 방법**

1. `ResourceTracker.cs`를 복사한다.
2. Addressables를 사용하는 모든 매니저 클래스에 필드로 포함시키고 `OnDestroy`에서 `ReleaseAll()`을 호출한다.

---

## 2. UI 공통 시스템

### 2-A. PopupManager + ModalWindowBase — 모달 스택 관리

| 항목            | 내용                                           |
| --------------- | ---------------------------------------------- |
| **파일**        | `Assets/01_Scripts/UI/Common/PopupManager.cs`  |
|                 | `Assets/01_Scripts/UI/Core/ModalWindowBase.cs` |
|                 | `Assets/01_Scripts/UI/Core/UiModalManager.cs`  |
| **의존성**      | UniTask                                        |
| **이식 난이도** | ⭐⭐                                           |

**기능 요약**

`ModalWindowBase`를 상속하는 팝업들을 `PopupManager`가 스택으로 관리한다.
`async UniTask<bool>` 반환을 통한 비동기 팝업 응답(예: "퇴각 확인 여부")을 지원한다.

**내장 팝업 종류**

| 파일                                                 | 용도             |
| ---------------------------------------------------- | ---------------- |
| `Assets/01_Scripts/UI/Common/ConfirmationPopup.cs`   | Yes/No 확인 팝업 |
| `Assets/01_Scripts/UI/Common/RewardPopupUI.cs`       | 보상 표시 팝업   |
| `Assets/01_Scripts/UI/Common/DescriptionDialogUI.cs` | 텍스트 설명 팝업 |

**이식 방법**

1. `PopupManager.cs`, `ModalWindowBase.cs`, `UiModalManager.cs` 세 파일을 복사한다.
2. 필요한 팝업(예: `ConfirmationPopup.cs`)을 추가로 복사한다.
3. Scene에 `PopupManager` GameObject를 배치하고 Panel/Canvas 참조를 연결한다.

---

### 2-B. FloatingText 시스템 — 화면 부유 텍스트 (풀링)

| 항목            | 내용                                             |
| --------------- | ------------------------------------------------ |
| **파일**        | `Assets/01_Scripts/UI/Common/FloatingText.cs`    |
|                 | `Assets/01_Scripts/UI/Common/FloatingTextDef.cs` |
| **의존성**      | TextMesh Pro                                     |
| **이식 난이도** | ⭐⭐                                             |

**기능 요약**

피해량, 치유량, 상태 텍스트 등을 화면 위에 부유·소멸시키는 시스템.
`FloatingTextDef` ScriptableObject로 텍스트 색상, 크기, 애니메이션 커브를 에디터에서 설정한다.
오브젝트 풀링으로 GC 부하를 최소화한다.

**이식 방법**

1. `FloatingText.cs`, `FloatingTextDef.cs`를 복사한다.
2. `FloatingTextDef` SO 에셋을 새 프로젝트에서 생성하고 스타일을 정의한다.
3. World Space Canvas에 부유 텍스트 프리팹을 등록한다.

---

### 2-C. UIArrowNavigator — 키보드 방향키 UI 탐색

| 항목            | 내용                                                   |
| --------------- | ------------------------------------------------------ |
| **파일**        | `Assets/01_Scripts/UI/Common/UIArrowNavigator.cs`      |
|                 | `Assets/01_Scripts/UI/Common/UIArrowNavButtonRelay.cs` |
| **의존성**      | Unity New Input System                                 |
| **이식 난이도** | ⭐                                                     |

**기능 요약**

방향키/패드로 UI 버튼 포커스를 순환 탐색하는 컴포넌트.
`UIArrowNavButtonRelay`를 각 버튼에 부착해 탐색 대상으로 등록한다.

---

## 3. 인벤토리 및 아이템 시스템

### 3-A. InventoryManager — 슬롯 기반 인벤토리

| 항목            | 내용                                                |
| --------------- | --------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Core/System/InventoryManager.cs` |
|                 | `Assets/01_Scripts/Core/System/IInventory.cs`       |
| **의존성**      | `ItemData` ScriptableObject                         |
| **이식 난이도** | ⭐⭐                                                |

**기능 요약**

- 슬롯 수: `maxSlots = 12` (변경 가능)
- 스택: `maxStack = 6` (아이템별 오버라이드 가능)
- 아이템 추가 시 기존 스택에 먼저 합치고, 부족하면 빈 슬롯 사용
- `Dictionary<string, int> _itemCountCache` 로 O(1) 수량 조회
- `OnInventoryChanged` 이벤트로 UI 자동 갱신

**같이 복사해야 할 파일**

| 파일                                                       | 용도                  |
| ---------------------------------------------------------- | --------------------- |
| `Assets/01_Scripts/Data/ItemData.cs`                       | 아이템 SO 기반 클래스 |
| `Assets/01_Scripts/Data/ItemEffectSO.cs` 계층              | 소비 효과 전략 패턴   |
| `Assets/01_Scripts/UI/Exploration/InventoryDragHandler.cs` | 드래그앤드롭 UI       |

**이식 방법**

1. 위 파일들을 복사한다.
2. `ItemData`의 `atlasAddress`, `spriteName` 필드를 프로젝트 아이콘 로딩 방식에 맞게 변경한다.
3. 인벤토리 슬롯 UI를 새 프로젝트에 맞게 새로 제작한다.

---

### 3-B. ItemEffectSO 계층 — 아이템 사용 효과 전략 패턴

| 항목            | 내용                                               |
| --------------- | -------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Data/` (ItemEffectSO 관련 파일) |
| **의존성**      | `ItemData.cs`                                      |
| **이식 난이도** | ⭐                                                 |

**기능 요약**

`ItemEffectSO(abstract)` → `HealEffectSO`, `RestoreMPEffectSO` 구조로, 새 효과 추가 시 ScriptableObject 하위 클래스만 작성하면 된다. 전략 패턴(Strategy Pattern)의 교과서적 구현이다.

**확장 방법**

```csharp
// 새 효과 추가 예시
[CreateAssetMenu(menuName = "Item/Effect/Revive")]
public class ReviveEffectSO : ItemEffectSO
{
    public override void Apply(BattleUnit target) { /* 부활 로직 */ }
}
```

---

## 4. 카메라 시스템

### 4-A. CameraFollow2D + CameraDynamicOffset

| 항목            | 내용                                                   |
| --------------- | ------------------------------------------------------ |
| **파일**        | `Assets/01_Scripts/Core/System/CameraFollow2D.cs`      |
|                 | `Assets/01_Scripts/Core/System/CameraDynamicOffset.cs` |
| **의존성**      | 없음 (순수 Unity Transform 조작)                       |
| **이식 난이도** | ⭐                                                     |

**기능 요약**

- `CameraFollow2D`: 대상 Transform을 부드럽게 추적 (Lerp 기반)
- `CameraDynamicOffset`: 플레이어 이동 방향에 따라 카메라 오프셋을 동적으로 추가해 시야를 확보

**이식 방법**

1. 두 파일을 복사해 Camera GameObject에 부착한다.
2. `CameraFollow2D.target`에 Player Transform을 연결한다.
3. Offset 강도와 Lerp 속도는 Inspector에서 조정한다.

---

## 5. 탐험 / 이동 시스템

### 5-A. PathfindingSystem — 헥스 오프셋 BFS 경로탐색

| 항목            | 내용                                                        |
| --------------- | ----------------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Exploration/System/PathfindingSystem.cs` |
| **의존성**      | Unity Tilemap (`UnityEngine.Tilemaps`)                      |
| **이식 난이도** | ⭐⭐⭐                                                      |

**기능 요약**

헥스 오프셋 좌표계 특화 BFS 경로탐색 시스템. 다음 기능을 포함한다.

| 기능                     | 설명                                                      |
| ------------------------ | --------------------------------------------------------- |
| **다층 Floor 맵**        | `List<Tilemap> floorMaps`로 복수의 바닥 레이어를 관리     |
| **4단계 이동 가능 판정** | 동적 장애물 → 바닥 타일 → 벽/장애물 → Physics2D 순서 검사 |
| **높이 차이 이동 제한**  | `tileAnchor.y` 차이 0.55f 이상 시 이동 불가               |
| **TileAnchor 좌표 보정** | 다층 맵에서 클릭 좌표를 정확한 그리드 셀로 변환           |
| **인접 셀 경로 탐색**    | `FindPathToAdjacentCell()` — 오브젝트 인접 상호작용용     |

**이식 방법**

1. `PathfindingSystem.cs`를 복사한다.
2. Tilemap 구조(바닥/장애물/벽 레이어)가 동일한 프로젝트에서는 거의 수정 없이 사용 가능하다.
3. 정사각 그리드 프로젝트에서는 6방향 오프셋 배열(`evenNeighbours`, `oddNeighbours`)을 4방향 또는 8방향으로 교체한다.

**주의사항**

- `Physics2D.OverlapBox`를 사용하는 4단계 판정은 LayerMask 설정이 필수다.
- `occupiedCells: HashSet<Vector3Int>`에 동적 장애물을 등록/해제하는 별도 로직이 필요하다.

---

### 5-B. ExplorationQTEManager — Quick Time Event 시스템

| 항목            | 내용                                                     |
| --------------- | -------------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Exploration/ExplorationQTEManager.cs` |
| **의존성**      | Unity New Input System, UniTask                          |
| **이식 난이도** | ⭐⭐                                                     |

**기능 요약**

탐험 이벤트 중 QTE를 처리한다. 성공/실패에 따른 결과 분기와 UI 연동이 포함되어 있다.

**이식 방법**

1. `ExplorationQTEManager.cs`를 복사한다.
2. QTE 트리거 조건(Lemegeton의 경우 특정 인터랙션 오브젝트)을 새 프로젝트 이벤트에 맞게 연결한다.

---

## 6. 전투 상태이상 시스템

> **참고:** 이 시스템은 전투 도메인 결합도가 높으나, 구조 자체는 턴제 RPG 어디서든 재사용 가능하다.

### 6-A. StatusController — 스택 기반 수치 상태이상

| 항목            | 내용                                                 |
| --------------- | ---------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Battle/Units/StatusController.cs` |
| **의존성**      | `BattleUnit.cs` (현재 결합됨, 분리 필요)             |
| **이식 난이도** | ⭐⭐⭐                                               |

**기능 요약**

`StatusId` 기반 스택 상태이상(출혈, 중독, 방어 등)을 관리한다.
`DebuffTuning.Mult` 배열로 스택별 효과 감쇠를 정의한다.

```
StatusId 범위 규칙:
  1~20   — 스킬 중첩 (캐릭터 고유)
  21~40  — 피해 보정 (공통)
  50~55  — 지속 피해 (DOT)
```

**이식 방법**

1. `StatusController.cs`와 `StatusId` enum을 복사한다.
2. `BattleUnit` 의존성을 인터페이스(`IStatusTarget`)로 추상화해 결합도를 낮춘다.
3. 새 프로젝트의 유닛 기반 클래스에 `StatusController` 컴포넌트를 추가한다.

---

### 6-B. UnitStateController — 불리언 특수 상태

| 항목            | 내용                                                    |
| --------------- | ------------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Battle/Units/UnitStateController.cs` |
| **의존성**      | `BattleUnit.cs` (현재 결합됨)                           |
| **이식 난이도** | ⭐⭐⭐                                                  |

**기능 요약**

`UnitStateId`(수면, 공포, 매복, 경계, 고립)와 `UnitStateBuffId`(연막 은신, 야수 영역) 같은 ON/OFF 성격의 특수 상태를 관리한다.

---

## 7. 에디터 도구

> 아래 스크립트들은 `Assets/01_Scripts/Editor/` 또는 `Assets/Editor/` 경로에 위치하며, 빌드에 포함되지 않는다.

### 7-A. MissingScriptFinder — 누락 스크립트 검색기

| 항목            | 내용                                              |
| --------------- | ------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Editor/MissingScriptFinder.cs` |
| **의존성**      | 없음                                              |
| **이식 난이도** | ⭐                                                |

**기능 요약**

씬 및 프리팹 전체에서 `Missing Script` 컴포넌트를 검색·목록화하는 에디터 윈도우. 프로젝트 규모가 커질수록 유용하다.

**이식 방법:** 파일만 복사하면 바로 사용 가능하다.

---

### 7-B. GatherableDataImporter — CSV → ScriptableObject 자동 임포터

| 항목            | 내용                                                 |
| --------------- | ---------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Editor/GatherableDataImporter.cs` |
| **의존성**      | `GatherableDataSO` (쌍으로 이식 필요)                |
| **이식 난이도** | ⭐⭐                                                 |

**기능 요약**

CSV 파일을 읽어 ScriptableObject를 자동 생성하는 에디터 임포터. 대량 게임 데이터 관리에 유용한 파이프라인이다.

**이식 방법**

1. `GatherableDataImporter.cs`를 복사한다.
2. CSV 컬럼 파싱 로직과 대상 ScriptableObject 타입을 새 프로젝트에 맞게 수정한다.
3. 동일한 패턴으로 다른 데이터 타입용 임포터를 파생시킬 수 있다.

---

### 7-C. GoogleSheetImporter — 구글 시트 데이터 임포터

| 항목            | 내용                                    |
| --------------- | --------------------------------------- |
| **파일**        | `Assets/Editor/GoogleSheetImporter.cs`  |
| **의존성**      | Google Sheets API 또는 TSV 다운로드 URL |
| **이식 난이도** | ⭐⭐                                    |

**기능 요약**

구글 시트에서 데이터를 가져와 ScriptableObject로 변환하는 에디터 파이프라인.

---

### 7-D. MapIDBaker — 맵 프리팹 ID 자동 부여

| 항목            | 내용                                     |
| --------------- | ---------------------------------------- |
| **파일**        | `Assets/01_Scripts/Editor/MapIDBaker.cs` |
| **의존성**      | 없음                                     |
| **이식 난이도** | ⭐⭐                                     |

**기능 요약**

맵 프리팹에 고유 ID를 자동으로 부여하는 에디터 도구. Addressables 기반 맵 로더를 사용하는 프로젝트에 적합하다.

---

### 7-E. ParametricDamageSkillEditor — 커스텀 인스펙터

| 항목            | 내용                                           |
| --------------- | ---------------------------------------------- |
| **파일**        | `Assets/Editor/ParametricDamageSkillEditor.cs` |
| **의존성**      | `ParametricDamageSkill.cs`                     |
| **이식 난이도** | ⭐⭐⭐                                         |

**기능 요약**

`ParametricDamageSkill` SO에 대한 커스텀 인스펙터로, 복잡한 파라미터를 시각적으로 편집할 수 있게 한다.

---

## 8. 화면 설정 시스템

### 8-A. ScreenSettingsManager + GraphicSettingsUI

| 항목            | 내용                                                     |
| --------------- | -------------------------------------------------------- |
| **파일**        | `Assets/01_Scripts/Core/System/ScreenSettingsManager.cs` |
|                 | `Assets/01_Scripts/UI/Common/GraphicSettingsUI.cs`       |
| **의존성**      | PlayerPrefs (저장), Unity Resolution API                 |
| **이식 난이도** | ⭐                                                       |

**기능 요약**

해상도(960×540 / 1280×720 / 1920×1080 / 2560×1440)와 화면 모드(전체화면/창 모드)를 드롭다운 및 토글 UI로 변경하고 PlayerPrefs에 저장한다.

**이식 방법**

1. 두 파일을 복사한다.
2. `GraphicSettingsUI`를 원하는 옵션 패널에 부착한다.
3. `ScreenSettingsManager.ApplySettings()`를 게임 시작 시 호출한다.

---

## 9. 이식 제외 항목

아래 시스템들은 Lemegeton 고유 도메인과 강하게 결합되어 다른 프로젝트로 이식하기 어렵다.

| 시스템                    | 제외 이유                                                       |
| ------------------------- | --------------------------------------------------------------- |
| **BattleManager**         | 헥스 그리드, ATB, FSM이 상호 의존. 게임 구조 자체가 전제됨      |
| **ATBTurnController**     | BattleUnit, BattleManager와 깊게 결합                           |
| **BattleSkillProcessor**  | Lemegeton 전용 데미지 계산 공식(STR/CLV/AGI 스탯 체계)에 종속   |
| **PlayerMovement**        | 헥스 오프셋 좌표, VigorManager, QTE 등 탐험 시스템 전체에 의존  |
| **MapManager 서브시스템** | Addressables 맵 구조, ExplorationSnapshot 등 프로젝트 고유 구조 |
| **CampUI 시스템**         | 스킬/진형/패시브 등 Lemegeton 게임 데이터 구조에 완전히 종속    |
| **PlayerDataManager**     | 저장 구조, 진형 배열, RuntimeUnitData가 Lemegeton 고유          |

---

## 부록 — 이식 우선순위 요약

| 우선순위 | 시스템                         | 범용성                     |
| -------- | ------------------------------ | -------------------------- |
| 🔴 즉시  | GameEventBus                   | 모든 장르                  |
| 🔴 즉시  | FloatingText 시스템            | 모든 장르                  |
| 🔴 즉시  | CameraFollow2D + DynamicOffset | 2D 게임 전반               |
| 🔴 즉시  | ScreenSettingsManager          | 모든 장르                  |
| 🔴 즉시  | MissingScriptFinder            | 에디터 공통                |
| 🟡 단기  | PopupManager + ModalWindowBase | UI 복잡한 게임             |
| 🟡 단기  | InventoryManager               | RPG/어드벤처               |
| 🟡 단기  | ResourceTracker                | Addressables 사용 프로젝트 |
| 🟡 단기  | GatherableDataImporter         | 데이터 주도 게임           |
| 🔵 장기  | PathfindingSystem              | 타일맵 기반 게임           |
| 🔵 장기  | StatusController               | 턴제 RPG                   |
| 🔵 장기  | ExplorationQTEManager          | QTE 필요 게임              |

---

_Lemegeton Project — Reusable Systems Catalog — 2026-02-28_
