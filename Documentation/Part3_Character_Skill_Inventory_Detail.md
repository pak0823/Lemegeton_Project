# Part 3: 캐릭터 / 스킬 / 인벤토리 시스템 — 상세 분석

**분류:** 파트3 — 캐릭터 · 스킬 · 인벤토리
**작성일:** 2026-03-04
**참조 파일:** `Assets/01_Scripts/Data/`, `Assets/01_Scripts/Battle/Skills/`, `Assets/01_Scripts/Core/`
**관련 문서:** Research.md §7~8, Lemegeton_InventorySystem_Analysis.md

---

## 목차

1. [데이터 레이어 구조](#1-데이터-레이어-구조)
2. [UnitData — 캐릭터 정의 ScriptableObject](#2-unitdata--캐릭터-정의-scriptableobject)
3. [런타임 상태 관리 (RuntimeUnitData)](#3-런타임-상태-관리-runtimeunitdata)
4. [진형(Formation) 시스템](#4-진형formation-시스템)
5. [패시브 시스템](#5-패시브-시스템)
6. [특성(Trait) 및 훈련(Training) 시스템](#6-특성trait-및-훈련training-시스템)
7. [6대 기본 스탯 체계](#7-6대-기본-스탯-체계)
8. [인벤토리 시스템](#8-인벤토리-시스템)
9. [아이템 데이터 (ItemData)](#9-아이템-데이터-itemdata)
10. [아이템 효과 계층 (ItemEffectSO)](#10-아이템-효과-계층-itemeffectso)
11. [제작 시스템 (Camp Craft)](#11-제작-시스템-camp-craft)
12. [드래그 앤 드롭](#12-드래그-앤-드롭)
13. [저장 시스템 (PlayerDataManager)](#13-저장-시스템-playerdatamanager)

---

## 1. 데이터 레이어 구조

`Data/` 폴더는 7개 하위 폴더로 세분화되어 있다.

```
Data/
├── Components/    데이터 컴포넌트
├── Databases/     각종 DB SO (StateStatModifierDB, TrainingDB, StatusDescriptionDB 등)
├── Definitions/   UnitData, ItemData, SkillAsset 등 SO 정의 (10개+)
├── Effects/       아이템/스킬 효과 SO (HealEffectSO, RestoreMPEffectSO 등)
├── Interaction/   상호작용 결과 데이터
├── Maps/          맵 데이터
└── Stages/        스테이지 DB (StageDatabase, StageNormalMapData)
```

### 주요 DB 3종

| DB                    | 역할                      |
| --------------------- | ------------------------- |
| `StateStatModifierDB` | 상태이상별 스탯 배율 정의 |
| `StatusDescriptionDB` | 상태이상 설명 텍스트      |
| `UnitStateVisualDB`   | 상태이상 시각 표현        |

---

## 2. UnitData — 캐릭터 정의 ScriptableObject

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
    SkillAsset[] skills;      // 보유 스킬
    PassiveAsset[] passives;  // 패시브 목록

    int currentBond;          // 유대 수치 (0~12)
    TraitAsset[] traits;      // 성격 특성 (2/6/12 유대에 해금)

    Sprite UnitIcon, UnitStandImage;
    GameObject battlePrefab;
}
```

---

## 3. 런타임 상태 관리 (RuntimeUnitData)

전투 중 변한 HP/MP/Rage를 저장하고, 씬 간 동기화한다.

```csharp
public class RuntimeUnitData
{
    float currentHP, currentMP, currentRage;
    bool isDead;
    Dictionary<string, int> statModifiers; // 영구 스탯 변화량
}
```

### 동기화 흐름

```
[전투 진입 전]
  PlayerDataManager.SyncToBattle()
  → RuntimeUnitData → BattleUnit 초기 HP/MP 설정

[전투 종료 후]
  PlayerDataManager.SyncFromBattle()
  → BattleUnit 최종 HP/MP/Rage → RuntimeUnitData 업데이트
```

> **직렬화 개선:** `RuntimeUnitData.statModifiers`의 Dictionary 직렬화 문제가
> `ISerializationCallbackReceiver` 기반 커스텀 래퍼 구조로 해결되었다.

---

## 4. 진형(Formation) 시스템

```csharp
// PlayerDataManager
UnitData[] formation; // 최대 19개 슬롯
```

- `FormationSlotUI`로 전투 진형을 편집
- `BattleManager.SpawnPlayerUnits()`에서 진형 데이터 읽어 `BattleMapManager.GetFormationSpawnPoint(i)` 위치에 전투 프리팹 소환
- `PlayerDataManager.formation` 배열에 유닛을 배치/제거

---

## 5. 패시브 시스템

### PassiveAsset 구조

`PassiveAsset`(abstract ScriptableObject)을 상속하는 캐릭터별 패시브들이 구현되어 있다. `Battle/Skills/Player/Passive/Character/` 하위에 캐릭터별로 분류되어 있다.

### 패시브 해금 관리

```csharp
// PlayerDataManager
HashSet<string> _unlockedPassiveIds;  // 해금된 패시브 ID 집합
// (PlayerPrefs에서 persistentDataPath JSON으로 마이그레이션 완료)
```

전투 진입 시 `BattleUnit.InitPassives(battleManager)`에서 해금된 패시브만 활성화한다.

### 구현된 캐릭터별 패시브

| 캐릭터                 | 패시브 파일                              | 효과                           |
| ---------------------- | ---------------------------------------- | ------------------------------ |
| 기간트(Gigant)         | `GigantCounterStackPassive`              | 대응(Action) 스택 관련 반격    |
| 기간트                 | `GigantEndTurnRegenPassive`              | 턴 종료 시 체력 재생           |
| 기간트                 | `GigantStrengthToBodyPassive`            | STR→BDY 스탯 전환              |
| 노을(NoEul)            | `NoEulBleedCountAgilityPassive`          | 출혈 스택 수에 비례한 AGI 상승 |
| 노을                   | `NoEulDoubleAttack`                      | 이중 공격 발동                 |
| 노을                   | `NoEulWeakOnLowestHpPassive`             | 체력 최저 대상에 취약 부여     |
| 럭키식스(LuckySix)     | `LuckySixShootingInsightPassive`         | 사격 통찰 (TODO 미완성)        |
| 럭키식스               | `LuckySixReactiveMoveAttackPassive`      | 이동 후 반응 공격              |
| 럭키식스               | `LuckySixReactiveAfterMoveAttackPassive` | 이동 후 공격 반응              |
| 라스트보르그(LastVorg) | `LastVorgRagePassive`                    | 분노 중첩 관련                 |
| 라스트보르그           | `LastVorgResearchPassive`                | 연구 중첩 관련                 |
| 라스트보르그           | `LastVorgToxicPassive`                   | 독성 관련                      |

---

## 6. 특성(Trait) 및 훈련(Training) 시스템

### TraitAsset

유대 수치 2/6/12에 해금되는 캐릭터 고유 특성.
`UnitData.traits: TraitAsset[]`에 배열로 저장.

### TrainingDB

```
스킬별 훈련 루트 (최대 3개):
├── 훈련 루트 1 — 스킬 코스트 변형
├── 훈련 루트 2 — 공격 범위 확장
└── 훈련 루트 3 — 사후 이동 추가
```

`SkillAsset.trainingRoutes: TrainingRouteInfo[]`에 저장되며, 활성화된 루트에 따라 `ParametricDamageSkill`의 `trainingUseAreaOverride`, `trainingUsePostMove` 파라미터가 적용된다.

---

## 7. 6대 기본 스탯 체계

| 스탯  | 전체 명칭        | 주요 역할                | 세부 수식                            |
| ----- | ---------------- | ------------------------ | ------------------------------------ |
| `STR` | 근력(Strength)   | 물리 공격력 기반         | MaxHP 계산에도 기여                  |
| `CLV` | 총명(Cleverness) | 마법 공격력 기반         |                                      |
| `AGI` | 민첩(Agility)    | ATB 충전 속도, 탈출 확률 | atbPerSecond = AGI × speedMultiplier |
| `BDY` | 신체(Body)       | 최대 HP 계산             | MaxHP = BDY×3 + STR                  |
| `MND` | 정신(Mind)       | 마법 방어                |                                      |
| `INS` | 통찰(Insight)    | 크리티컬/특수 판정       |                                      |

---

## 8. 인벤토리 시스템

### InventoryManager 구조

```csharp
public class InventoryManager : MonoBehaviour, IInventory
{
    int maxSlots = 12;
    int maxStack = 6;
    InventoryItem[] slots;               // 실제 슬롯 배열
    Dictionary<string, int> _itemCountCache; // O(1) 검색 캐시
}
```

`DontDestroyOnLoad` 적용된 전역 싱글톤. 씬 전환 시 파괴되지 않으며, 중복 생성 시 자기 자신을 파괴하여 유일성 보장.

### IInventory 인터페이스

```csharp
public interface IInventory {
    bool AddItem(string id, int count);
    bool ConsumeItem(string id, int count);
    int GetItemCount(string id);
}
```

확장성을 위해 `IInventory` 인터페이스로 추상화. 플레이어 인벤토리, 창고, 상점 모두 동일한 인터페이스로 처리 가능.

### 아이템 추가 로직

```
AddItem(id, count):
  1. 기존 같은 아이템의 여유 슬롯에 먼저 합치기 (스택)
  2. 여유 없으면 빈 슬롯 사용
  3. _itemCountCache 업데이트 (O(1) 조회 보장)
  4. OnInventoryChanged 이벤트로 UI 통지
```

### 과중량 연동

아이템 수 >= `overweightThreshold(10)` 시:
→ `ExplorationStatusManager.AddStatus(ExplorationStatusID.Overweight)`

---

## 9. 아이템 데이터 (ItemData)

```csharp
public class ItemData : ScriptableObject
{
    string itemID;       // Primary Key
    string itemName;
    string atlasAddress, spriteName;  // Addressables Atlas 참조
    ItemType itemType;   // Material / Consumable 등
    int maxStack;
    ItemEffectSO useContextEffect;    // 소비 효과 SO
}
```

아이콘은 Addressables Atlas를 통해 `"ItemAtlas[icon_potion]"` 형식으로 런타임 로드된다.

---

## 10. 아이템 효과 계층 (ItemEffectSO)

```
ItemEffectSO (abstract)
├── HealEffectSO        — HP 회복
└── RestoreMPEffectSO   — MP 회복
```

향후 추가 효과 타입 확장이 용이한 전략 패턴 구조.

---

## 11. 제작 시스템 (Camp Craft)

`CampCraftPage`에서 `CraftRecipe` SO를 기반으로 재료 아이템을 소비하고 결과물을 생성하는 제작 시스템.

```
CraftRecipe SO
├── requiredItems: ItemData[] + int[]   // 재료 목록
└── resultItem: ItemData                // 결과물

CampCraftPage
├── 재료 보유 여부 검사 (IInventory.GetItemCount)
├── 재료 소비 (IInventory.ConsumeItem)
├── 결과물 지급 (IInventory.AddItem)
└── CraftResultPopup 표시
```

---

## 12. 드래그 앤 드롭

`InventoryDragHandler`와 `TrashZone`으로 인벤토리 UI에서 드래그 앤 드롭 기능 구현.

- 슬롯 간 아이템 이동
- `TrashZone`에 드롭으로 아이템 버리기

---

## 13. 저장 시스템 (PlayerDataManager)

### 현재 구현 상태 (완료)

```csharp
public class PlayerDataManager : MonoBehaviour
{
    // 런타임 데이터
    Dictionary<UnitData, RuntimeUnitData> unitStates;
    HashSet<string> _unlockedPassiveIds;

    // Addressables 추적
    ResourceTracker _tracker;  // 핸들 누수 방지

    void SaveGame() → Application.persistentDataPath 기반 파일(savedata.json) 저장
    void LoadGame() → persistentDataPath에서 JSON 로드
}
```

### SaveData 클래스

```csharp
[Serializable]
public class SaveData
{
    public List<InventoryItem> inventory;
    public Dictionary<string, bool> unlockedPassives; // 패시브 해금 상태
    public List<RuntimeUnitSaveData> unitStates;      // statModifiers 포함
}
```

### 완료된 개선 사항

- [x] PlayerPrefs → `Application.persistentDataPath` 기반 파일 저장으로 일원화
- [x] `RuntimeUnitData.statModifiers` (Dictionary) 직렬화 문제 해결 (`ISerializationCallbackReceiver`)
- [x] 패시브 해금 상태도 `SaveData`에 통합

---

_Part 3 Character / Skill / Inventory System Detail — Lemegeton Project Documentation — 2026-03-04_
