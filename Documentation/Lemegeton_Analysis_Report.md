# LEMEGETON PROJECT — 프로젝트 구조 분석 보고서
**Project Architecture & Code Quality Report**

| 항목 | 내용 |
|------|------|
| 분석 일자 | 2026-02-20 |
| 엔진 | Unity (URP, 2D / Tilemap) |
| 언어 | C# (.NET) + JavaScript (Editor 도구) |
| 스크립트 총 수 | 227개 (.cs) |
| 씬 구성 | Title / Exploration / Battle / Test 外 |
| 문서 보관 | Documentation/ 내 .md 파일 17개 |

---

## 1. 프로젝트 개요

Lemegeton Project는 Unity 2D Tilemap 기반의 탐험-전투 하이브리드 RPG입니다. 플레이어는 타일맵 위에서 탐험하며 자원(활기)을 소모하다가, 적과 조우하면 별도의 전술적 턴제 전투 씬으로 전환되는 구조를 취하고 있습니다. 프로젝트명인 Lemegeton은 17세기 마법서에서 따온 이름으로, 어두운 판타지 세계관을 암시합니다.

### 1.1 핵심 기술 스택

| 분류 | 상세 |
|------|------|
| 렌더 파이프라인 | Universal Render Pipeline (URP) 14.0.12 |
| 맵 시스템 | Unity Tilemap + 2D Feature Pack |
| 에셋 관리 | Addressables 1.22.3 (비동기 로딩, 라벨 기반) |
| UI 텍스트 | TextMesh Pro 3.0.7 |
| 비동기 처리 | UniTask (Cysharp) + 기본 async/await / Coroutine 혼합 |
| 입력 처리 | New Input System 1.14.2 |
| AI 도우미 | Unity MCP (CoplayDev) 연동 |
| 버전 관리 | Git + GitHub (Visual Studio 2022 / JetBrains Rider) |
| 데이터 파이프라인 | CSV → ScriptableObject 자동 임포터 (Editor 도구) |

### 1.2 게임 플로우 요약

게임은 크게 4개의 씬으로 구성됩니다.

- **TitleScene**: 타이틀 화면, 신규/불러오기 분기
- **ExplorationScene**: Tilemap 기반 탐험, 활기(Vigor) 소모, 오브젝트 상호작용
- **BattleScene**: 헥스 그리드 ATB 턴제 전투
- **CampScene** (Exploration 내 Camp UI): 성장, 스킬 설정, 아이템 제작

---

## 2. 폴더 구조 및 역할

프로젝트의 Assets 폴더는 숫자 접두어로 레이어를 명확히 구분합니다. 이는 Unity에서 에셋 탐색 시 가독성을 높이고 관심사를 분리하는 효과적인 방법입니다.

| 폴더 | 역할 및 내용 |
|------|-------------|
| `00_Scenes/` | Title / Exploration / Battle 씬 + 테스트 씬 |
| `01_Scripts/` | 모든 C# 소스코드 (227개) — 도메인별 세분화 |
| `02_Prefabs/` | Common / Title / Exploration / Battle 프리팹 |
| `03_Data/` | ScriptableObject 데이터 에셋 (아이템, 스킬, 유닛, 스테이지) |
| `04_Art/` | 스프라이트, 애니메이션, 폰트 (Character/Enemy/UI/Effect 분류) |
| `07_Test/` | 개발 중 테스트용 리소스 및 씬 (출시 전 제거 예정) |
| `AddressableAssetsData/` | Addressables 그룹, 빌더, 스키마 설정 |
| `Editor/` | 커스텀 에디터 툴 (MapIDBaker, GatherableDataImporter 등) |
| `Resources/` | 런타임 동적 로딩용 최소 리소스 |
| `TextMesh Pro/` | TMP 폰트, 쉐이더, 스프라이트 에셋 |
| `Documentation/` | 마크다운 기술 문서 17개 (분석 보고서, 구현 계획서 등) |

### 2.1 스크립트 도메인 분포

| 도메인 | 스크립트 수 | 비율 | 주요 역할 |
|--------|------------|------|----------|
| `Battle/` | 75개 | 33% | 전투 코어 / FSM / 스킬 / 유닛 / AI |
| `UI/` | 68개 | 30% | 전투 / 탐험 / 캠프 / 공통 / 팝업 UI |
| `Data/` | 27개 | 12% | ScriptableObject 정의 / DB / 효과 |
| `Interactables/` | 16개 | 7% | 함정 / 퍼즐 / 상자 / 포탈 / 인카운터 |
| `Exploration/` | 18개 | 8% | 맵로더 / 플레이어이동 / 안개 / 영속성 |
| `Core/` | 12개 | 5% | 씬전환 / 인벤토리 / 이벤트버스 / 카메라 |
| `Editor/ + Utils/` | 11개 | 5% | 에디터 도구 / 디버그 / 주소 |

---

## 3. 주요 시스템 분석

### 3.1 전투 시스템 (Battle System)

전투 시스템은 프로젝트에서 가장 복잡하고 완성도 높은 영역입니다. 헥스 오프셋 그리드 위에서 ATB(Active Time Battle) 방식으로 턴이 진행됩니다.

| 컴포넌트 | 책임 범위 |
|---------|----------|
| `BattleManager.cs` (1,313줄) | 최상위 오케스트레이터. 씬 전체 상태 관리 및 이벤트 라우팅 |
| `BattleStateMachine / States` | FSM으로 Idle → ActionSelect → Targeting → Resolving 등 상태 전환 관리 |
| `ATBTurnController.cs` | 유닛 AGI 기반 ATB 게이지 계산 및 턴 순서 결정 |
| `BattleTurnManager.cs` | 실제 턴 소비(이동/공격/휴식/진정), 과로(Overwork) 처리 |
| `BattleGridManager.cs` | 헥스 그리드 점유 여부 조회, 인접 이동 가능 셀 계산 |
| `BattleSkillProcessor.cs` | 스킬 흐름 실행 (Unit/Tile 타겟 분기), 데미지 계산 |
| `BattleWaveManager.cs` | 웨이브 스폰, 다음 웨이브 전환, 전체 클리어 감지 |
| `BattleInputHandler.cs` | 클릭/탭 입력 처리, 이동/스킬 미리보기 하이라이팅 |
| `CommandQueue.cs` | Command 패턴: Move/Skill 커맨드 큐잉 및 실행 |
| `EnemyAI / AnalysisEnemyAI / BossEnemyAI` | 적 AI 행동 트리 (기본/분석형/보스 3단계) |
| `StatusController / UnitStateController` | 상태이상(출혈/공포 등) 적용 및 턴 처리 |
| `PassiveAsset` (캐릭터별) | 각 캐릭터(기간트/노을/럭키식스/라스트보르그)의 패시브 로직 |

스킬 구조는 `SkillAsset(SO)`을 기반으로 `ParametricDamageSkill`, `ParametricSupportSkill`, `SelfStateSkill` 등의 템플릿 클래스로 나뉘며, `ISkillForStateResolver` 인터페이스를 통해 캐릭터 상태에 따른 스킬 변형이 가능합니다.

#### 전투 상태 흐름 (FSM)

```
Idle
 └─ HandleTurnReady(unit)
     └─ ActionSelect
         ├─ OnClickMove()  →  Moving  →  Resolving
         └─ OnClickAttack() → (SkillPanel) → Targeting
             ├─ ConfirmTarget()   →  Resolving  →  ActionSelect or Idle
             └─ ConfirmSkillOnTile() → Resolving
```

### 3.2 탐험 시스템 (Exploration System)

탐험 시스템은 최근 대규모 리팩토링을 거쳐 `MapManager` 하나에 집중되어 있던 책임이 4개의 서브시스템으로 분리되었습니다.

- **`ExplorationMapLoader`**: 맵 프리팹 Instantiate/Destroy, 전투 복귀 시 기존 맵 재사용
- **`ExplorationEntitySpawner`**: 플레이어 스폰, 상자/함정/인카운터 오브젝트 배치
- **`ExplorationPersistenceManager`**: Snapshot 기반 상태 저장/복구 및 Addressables 메모리 관리
- **`PathfindingSystem`**: Tilemap 기반 A* 경로 탐색

`VigorManager`는 탐험 자원(활기)을 관리하며, 원자적 연산을 보장하여 이동 중 중복 차감을 방지합니다.

> ⚠️ `PlayerMovement.cs`는 2,525줄로, 그리드 이동 + 경로 추적 + 박스 밀기(소코반) + QTE 트리거를 모두 포함하는 대형 파일입니다. 분리가 필요합니다.

### 3.3 캠프 시스템 (Camp System)

`CampUIManager`를 허브로 Status / Skill / Craft 탭을 분리 관리합니다.

- **`CampStatusPage`**: 캐릭터 선택 및 스탯 표시
- **`CampSkillPage`** (1,090줄): 스킬 슬롯 배치, 훈련 루트 선택, 패시브 해금
- **`CampCraftPage`**: `CraftRecipe SO` 기반 재료 소모 및 아이템 제작
- **드래그 앤 드롭**: `InventoryDragHandler` + `TrashZone`으로 인벤토리 슬롯 간 이동 구현

### 3.4 코어 인프라 (Core Infrastructure)

| 컴포넌트 | 역할 |
|---------|------|
| `SceneTransitionManager` | DontDestroyOnLoad 싱글톤. 씬 페이드, 복귀 컨텍스트(씬명/좌표/스냅샷/보상) 저장 |
| `PlayerDataManager` | DontDestroyOnLoad 싱글톤. 유닛 소유 목록, 진형(Formation), 런타임 HP/MP/Rage 상태 관리 |
| `InventoryManager` | DontDestroyOnLoad 싱글톤. 인벤토리 슬롯 관리 및 저장(PlayerPrefs JSON) |
| `GameEventBus` | 정적 이벤트 버스. `Subscribe<T>/Publish<T>` 제네릭 방식으로 시스템 간 결합도 감소 |
| `StageRuntimeContext` | 현재 스테이지 번호, 리셋 여부 등 런타임 컨텍스트 |
| `GameResetter` | 게임 전체 초기화 진입점 |

### 3.5 데이터 레이어 (Data Layer)

ScriptableObject(SO)를 중심으로 데이터 주도적 설계를 구현하고 있습니다.

- **`UnitData`**: 유닛 스탯, 스킬 배열, 전투/탐험 프리팹 참조
- **`ItemData`**: 아이템 종류(도구/재료/효과), 아이콘, 드롭 확률
- **`SkillAsset / PassiveAsset`**: 스킬/패시브 행동 데이터 + 실행 로직 통합
- **`StageDatabase → StageNormalMapData`**: 스테이지별 맵 프리팹 배열
- **`RewardTableSO`**: 확률 기반 보상 테이블
- **`StateStatModifierDB / StatusDescriptionDB / UnitStateVisualDB`**: 상태이상 관련 DB 3종

---

## 4. 적용된 아키텍처 패턴

| 패턴 | 적용 위치 | 평가 |
|------|----------|------|
| Singleton (싱글톤) | BattleManager, PlayerDataManager 外 총 26개 | ⚠️ 과다 |
| FSM (유한 상태 기계) | BattleStateMachine / BattleBaseState / BattleConcreteStates | ✅ 적절 |
| Command Pattern | CommandQueue + ICommand / MoveCommand / SkillCommand | ✅ 적절 |
| Observer (이벤트) | GameEventBus (정적) + BattleManager 이벤트 다수 | ✅ 좋음 |
| Strategy (전략) | SkillAsset 상속 계층 (Parametric* / Self* / Tactics*) | ✅ 적절 |
| Template Method | EnemySkill → EnemyBasicAttack / WebCastWebTrap | ✅ 적절 |
| Data-Driven SO | SkillAsset / UnitData / RewardTableSO 등 | ✅ 매우 좋음 |
| Interface Segregation | IGridProvider / IFieldController / IMapComponent 등 | ✅ 좋음 |
| Repository / DB | StateStatModifierDB / TrainingDB / StatusDescriptionDB | ✅ 적절 |
| Snapshot Pattern | ExplorationPersistenceManager + ExplorationSnapshot | ✅ 적절 |

---

## 5. 발견된 문제점 및 개선 제안

### 5.1 즉시 개선이 필요한 항목 (Critical 🔴)

---

#### [CRITICAL-1] async void 패턴 — 예외가 무음으로 삼켜짐

**영향 파일**
- `BattleStateMachine.ChangeState()`
- `BattleManager.OnClickEscape()`
- `PlayerDataManager.LoadStartingUnitsByLabel()`
- `PlayerDataManager.AddUnitByAddress()`
- `MapObjectSpawner.Spawn()`

**문제**: `async void`는 Unity에서 `catch`할 수 없는 예외를 발생시켜 씬이 무응답 상태가 될 수 있습니다.

**해결책**
```csharp
// ❌ 위험
public async void LoadStartingUnitsByLabel() { ... }

// ✅ 안전 (UniTask 사용 시)
public async UniTaskVoid LoadStartingUnitsByLabel()
{
    try { ... }
    catch (Exception e) { Debug.LogException(e); }
}
```

---

#### [CRITICAL-2] 글자 깨짐 (Encoding) — PlayerMovement.cs 外

**증거**: 루트에 `FixGarbledText.ps1` 스크립트 존재 + 코드 내 `\r\r\n` (CRLF 혼용) 다수 감지

**문제**: 팀 내 에디터 설정 불일치로 인한 파일 인코딩 문제. `PlayerMovement.cs` 등에서 한글 주석/Header가 깨진 채로 Push됨.

**해결책**
```
# .gitattributes 에 추가
* text=auto eol=lf
*.cs text eol=lf
```
추가로 `.editorconfig`에 `charset = utf-8` 강제 설정 후 `FixGarbledText.ps1` 제거.

---

#### [CRITICAL-3] 저장 시스템 미완성 — 분산된 PlayerPrefs 의존

**문제 지점**
- 인벤토리 저장: `PlayerDataManager` → `PlayerPrefs.SetString("SaveSlot_1", json)`
- 패시브 해금: `PassiveAsset.cs` → `PlayerPrefs.GetInt($"Passive_{key}")` (SaveData와 완전 분리)
- `RuntimeUnitData.statModifiers`가 `Dictionary<string, int>` → JSON 직렬화 시 KeyValuePair 손실 위험

**해결책**: `Application.persistentDataPath` 기반 JSON 파일 저장으로 일원화. 패시브 해금 상태도 `SaveData` 클래스에 통합.

```csharp
// 권장 구조
[Serializable]
public class SaveData
{
    public List<InventoryItem> inventory;
    public Dictionary<string, bool> unlockedPassives; // PassiveAsset 통합
    public List<RuntimeUnitSaveData> unitStates;      // statModifiers 포함
}
```

---

### 5.2 개선이 권장되는 항목 (Warning 🟡)

---

#### [WARN-1] 과도한 싱글톤 — 26개 (DontDestroyOnLoad 10개)

**현황**: `BattleManager`, `MapManager`, `PlayerMovement`, `UIManager`, `PopupManager`, `InventoryManager`, `SceneTransitionManager`, `BattleRewardManager`, `StageRuntimeContext`, `ExplorationFogManager` 등

**문제**: 씬별로 수명이 다른 매니저가 혼재 → 테스트 격리 불가능, 씬 재진입 시 중복 생성 위험

**해결책**: 영구 싱글톤(게임 전체 수명)과 씬 범위 싱글톤을 구분. 장기적으로 Zenject 같은 DI 컨테이너 도입 검토.

---

#### [WARN-2] FindObjectsOfType 남용 — 35곳

**현황**: `BattleManager.HandleVictory()`, `OnClickEscape()` 등에서 `FindObjectsOfType<BattleUnit>()` 직접 호출. 주석으로 `// [Optimization] Use registry`가 18곳 표시되어 있으나 일부 미완료.

**문제**: 전투 씬에서 유닛이 많아질수록 매 프레임 선형 탐색으로 성능 저하.

**해결책**: 이미 구현된 `_activeUnits HashSet`을 일관되게 사용하고 `FindObjectsOfType` 완전 제거.

```csharp
// ❌ 현재 (일부 잔존)
var units = FindObjectsOfType<BattleUnit>();

// ✅ 개선 (레지스트리 사용)
var units = _activeUnits.Where(u => u != null && !u.IsDead).ToList();
```

---

#### [WARN-3] 거대 클래스 (God Object 위험)

| 파일 | 줄 수 | 포함 책임 |
|------|-------|----------|
| `PlayerMovement.cs` | 2,525줄 | 그리드 이동 + 경로추적 + 소코반 퍼즐 + QTE 트리거 + 카메라 신호 |
| `BattleUnit.cs` | 1,909줄 | 유닛 데이터 + 애니메이션 + ATB + 스킬 쿨다운 + 상태이상 + 사망 처리 |
| `BattleManager.cs` | 1,313줄 | 오케스트레이터 + 이동/스킬/타겟팅/사망 처리 직접 포함 |
| `CampSkillPage.cs` | 1,090줄 | 스킬 슬롯 + 훈련 루트 + 패시브 해금 로직 혼합 |

**해결책**: `PlayerMovement`를 `PathFollower`, `GridInteractionHandler`, `QTEHandler`로 분리. `BattleUnit`의 애니메이션 로직은 `UnitVisual`(이미 존재)로 위임 강화.

---

#### [WARN-4] 임시/하드코딩 코드가 프로덕션에 혼재

| 위치 | 내용 |
|------|------|
| `TitleMenuUI.cs:55` | `[SerializeField] private bool simulateHasSaveData = false` — 개발용 플래그 노출 |
| `GigantEndTurnRegenPassive.cs:40` | "해금 안 됐을 때 테스트용으로 강제로 진행도 반환" |
| `LuckySixShootingInsightPassive.cs` | TODO 주석 3개 — 실제 스탯 연동 미구현 |
| `ExplorationStatusSlot.cs:69` | "임시 방편: UnitData의 Base 스탯을 기반으로 대략적인 MaxHP 계산" |
| `SceneTransitionManager.cs:357` | "임시 테스트용 — 훈련씬을 거치기 위해 임시 추가" |

**해결책**: 개발용 플래그는 `#if UNITY_EDITOR` 조건부 컴파일 또는 별도 Debug 씬으로 분리.

---

#### [WARN-5] 비동기 패턴 혼합 — Coroutine + async/await + UniTask

**현황**: 스킬 흐름은 `IEnumerator Coroutine`, FSM 전환은 `async void`, 팝업 대기는 `async Task<bool>`, UniTask도 혼용.

**문제**: 세 가지 비동기 패턴 혼합은 실행 순서 예측을 어렵게 하고 취소(Cancellation) 처리 누락을 유발.

**해결책**: UniTask를 단일 비동기 표준으로 채택. `IEnumerator` Coroutine을 점진적으로 `UniTask.Delay` / `await UniTask.WaitUntil`로 대체.

---

### 5.3 참고 사항 (Info 🔵)

#### [INFO-1] 문서화 수준 양호하나 최신화 필요

`Documentation/` 폴더에 17개 마크다운 기술 문서 보관 — 국내 인디 프로젝트 중 이례적으로 체계적. 다만 일부 문서가 최신 코드 상태와 일치하지 않을 수 있음. 주요 시스템 변경 시 문서도 함께 커밋하는 팀 컨벤션 수립을 권장.

#### [INFO-2] 07_Test 폴더 — 출시 전 정리 필요

- 테스트 씬: `Test.unity`, `FogTest.unity`, `Ui_Test.unity`
- 디버그 스크립트: `QTESystemTester`, `TilemapDebugger`, `AddressableLoaderTest`, `AddressableSpawnerTest`, `TileCounter`, `ItemIconLoader`

별도 테스트 전용 Assembly Definition(`.asmdef`)으로 분리하고 빌드 시 제외 설정 권장.

#### [INFO-3] 단위 테스트 미비

`com.unity.test-framework 1.1.33`이 설치되어 있으나 실제 테스트 코드 없음. 전투 데미지 계산, ATB 턴 순서, 보상 확률 등은 단위 테스트에 적합한 영역. `BattleSkillProcessor.GetFinalSkillDamage()` 등 순수 계산 메서드부터 테스트 작성을 권장.

#### [INFO-4] Addressables 핸들 관리 검토 필요

`PlayerDataManager.LoadStartingUnitsByLabel()`에서 `async void + Addressables.LoadAssetsAsync` 혼용. Addressables Release 처리가 `ExplorationPersistenceManager`에만 국한되어 다른 곳에서 누락 가능성이 있음. 중앙에서 핸들을 추적하는 `ResourceTracker` 클래스 도입을 권장.

---

## 6. 종합 평가

| 평가 항목 | 점수 | 근거 |
|----------|------|------|
| 아키텍처 구조 | ★★★★☆ | FSM, Command, Observer 패턴 적절 적용. 도메인 분리 명확 |
| 코드 가독성 | ★★★☆☆ | 주석 풍부하나 거대 파일(2,500줄) 존재. 한/영 혼용 명명 |
| 데이터 설계 | ★★★★☆ | SO 기반 데이터 주도 설계 우수. CSV 파이프라인 자동화 |
| 안정성 / 예외처리 | ★★☆☆☆ | async void 5곳, PlayerPrefs 저장, 인코딩 문제 존재 |
| 성능 관리 | ★★★☆☆ | 레지스트리 도입했으나 FindObjectsOfType 잔존 35곳 |
| 테스트 가능성 | ★★☆☆☆ | 싱글톤 26개, 단위 테스트 전무, 테스트 프레임워크만 설치 |
| 문서화 | ★★★★★ | 기술 문서 17개, 한국어 주석 철저, 구현 계획서 별도 관리 |
| 확장성 | ★★★★☆ | 인터페이스 분리, Strategy 패턴, 스킬 SO 시스템 설계 우수 |

Lemegeton Project는 개인 또는 소규모 인디 팀 기준으로 매우 체계적인 구조를 갖추고 있습니다. 특히 전투 시스템의 FSM 설계, 데이터 주도 스킬 시스템, 탐험 시스템의 서브시스템 분리 리팩토링은 높이 평가됩니다. 다만 `async void`로 인한 예외 처리 위험과 저장 시스템 미완성은 출시 전 반드시 해결해야 할 과제입니다.

---

## 7. 우선순위별 Action Plan

| 우선순위 | 작업 항목 | 예상 공수 | 담당 영역 |
|---------|----------|----------|----------|
| 🔴 P0 (즉시) | `async void` → `UniTaskVoid` / `async UniTask` 전환 (5곳) | 2~3일 | Battle / Core |
| 🔴 P0 (즉시) | `.gitattributes` CRLF 설정 + 인코딩 일괄 정리 | 0.5일 | 전체 |
| 🔴 P0 (즉시) | 저장 시스템 일원화 (PlayerPrefs → File JSON) | 3~5일 | Core / Data |
| 🟡 P1 (이번 마일스톤) | `FindObjectsOfType` 잔존 17곳 → `_activeUnits` 레지스트리 교체 | 1~2일 | Battle |
| 🟡 P1 (이번 마일스톤) | `PlayerMovement.cs` 분리 (PathFollower, InteractionHandler) | 3~5일 | Exploration |
| 🟡 P1 (이번 마일스톤) | 개발용 플래그 `#if UNITY_EDITOR`로 이동 | 1일 | UI / Exploration |
| 🟡 P2 (다음 마일스톤) | LuckySix 패시브 TODO 구현 완료 | 2~3일 | Battle/Skills |
| 🔵 P3 (장기) | 단위 테스트 작성 (데미지 계산, ATB 순서) | 5일~ | Battle |
| 🔵 P3 (장기) | `07_Test` 폴더 `.asmdef` 분리 및 빌드 제외 | 0.5일 | Editor |
| 🔵 P3 (장기) | Addressables Release 중앙화 (ResourceTracker) | 2일 | Core |

---

*Lemegeton Project Architecture Report — Generated 2026-02-20 by Claude*
