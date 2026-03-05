# 시스템 설정 가이드 (System Setup Guide)

**분류:** 인수인계 — 개별 시스템 씬 설정 및 사용법
**작성일:** 2026-03-05
**통합된 원본 파일:** ExplorationStatus_SetupGuide, FloatingText_Usage_Guide, DragDrop_Implementation_Report, UniversalDataImporter_Manual, Gatherable_Object_Guide, StageData_Usage_Guide

> **참고:** 새 콘텐츠 추가 순서(캐릭터/스킬/맵/아이템)는 `ContentCreation_Workflow.md`를 참고한다.
> 이 문서는 기존에 만들어진 **시스템을 씬에 배치하고 설정하는 방법**을 다룬다.

---

## 목차

1. [탐험 상태이상 시스템 씬 설정](#1-탐험-상태이상-시스템-씬-설정)
2. [FloatingText 시스템 사용법](#2-floatingtext-시스템-사용법)
3. [드래그 앤 드롭 패턴 (UI 확장 가이드)](#3-드래그-앤-드롭-패턴-ui-확장-가이드)
4. [UniversalDataImporter 사용법](#4-universaldataimporter-사용법)
5. [채집 오브젝트(GatherableObject) 설정](#5-채집-오브젝트gatherableobject-설정)
6. [스테이지 데이터 설정 가이드](#6-스테이지-데이터-설정-가이드)

---

## 1. 탐험 상태이상 시스템 씬 설정

### 1-1. ExplorationStatusManager 배치

1. **Hierarchy** → 빈 게임 오브젝트 생성 → 이름: `ExplorationSystem`
2. `ExplorationStatusManager` 컴포넌트 추가
   - Singleton이므로 씬에 **단 하나**만 존재해야 한다

### 1-2. 상태 데이터 SO 생성

```
Project → Assets/03_Data/Interactions/
→ 우클릭 → Create → Lemegeton → Exploration → Status Data
→ 파일명: ExplorationStatusData.asset
```

인스펙터 `Status List`에 항목 추가:

**Overweight (과중량) 설정 예시:**

| 필드         | 값                                                                     |
| ------------ | ---------------------------------------------------------------------- |
| ID           | `Overweight`                                                           |
| Display Name | 과중                                                                   |
| Description  | `소지품이 너무 많아 움직이기 힘듭니다.\n활기 소모량이 2배 증가합니다.` |
| Is Debuff    | ✅ True                                                                |
| Icon         | 디버프 아이콘 스프라이트 할당                                          |

### 1-3. ExplorationStatusUI 설정

1. Canvas 하위에 `Panel_StatusIcons` 빈 오브젝트 생성
   - `Horizontal Layout Group` 추가 → 아이콘 자동 정렬
2. `ExplorationStatusUI` 컴포넌트 추가 후 연결:
   - **Data DB** → `ExplorationStatusData.asset` 할당
   - **Icon Root** → `Panel_StatusIcons` Transform
   - **Icon Prefab** → Image 컴포넌트가 있는 프리팹

### 1-4. 테스트

1. 플레이 실행
2. 아이템 11개 이상 획득
3. 확인 항목:
   - `Panel_StatusIcons`에 '과중' 아이콘 표시 여부
   - 이동 시 활기가 2×(기본 2 → 4) 소모되는지

---

## 2. FloatingText 시스템 사용법

### 2-1. 스타일 설정 (Inspector)

`FloatingTextManager` 프리팹 인스펙터 → **Style Configs** 리스트:

| 필드                  | 설명                                                                        |
| --------------------- | --------------------------------------------------------------------------- |
| Style                 | `FloatingTextStyle` Enum 값 선택 (Damage / Heal / Critical / VigorLoss ...) |
| Color                 | 텍스트 색상                                                                 |
| Scale Multiplier      | 크기 배율 (기본 `1.0f`, 크리티컬 강조 시 `1.5f`)                            |
| Move Speed Multiplier | 떠오르는 속도 배율                                                          |
| Scale Curve           | 등장 시 'Punch' 효과용 Animation Curve (선택)                               |

### 2-2. 스크립트에서 호출

```csharp
// 데미지
FloatingTextManager.Instance.Spawn(position, "100", FloatingTextStyle.Damage);

// 치유
FloatingTextManager.Instance.Spawn(position, "50", FloatingTextStyle.Heal);

// 치명타
FloatingTextManager.Instance.Spawn(position, "CRITICAL!", FloatingTextStyle.Critical);

// 활기 소모
FloatingTextManager.Instance.Spawn(position, "-5", FloatingTextStyle.VigorLoss);
```

> ⚠️ 스타일 Config가 등록되지 않은 Enum 값으로 호출하면 **흰색 기본값**으로 출력된다.

### 2-3. 새 스타일 추가

1. `FloatingTextDef.cs`의 `FloatingTextStyle` Enum에 새 값 추가 (예: `ExpGain`)
2. `FloatingTextManager` 인스펙터 → Style Configs에 항목 추가 및 설정
3. 코드에서 `FloatingTextStyle.ExpGain`으로 호출

---

## 3. 드래그 앤 드롭 패턴 (UI 확장 가이드)

인벤토리 UI와 캠프 UI 모두에서 **Ghost Image(반투명 복제)** 방식으로 드래그 앤 드롭이 구현되어 있다.

### 3-1. 핵심 구조

```
InventoryUI / CampUIManager
├── dragGhostImage: Image    — 마우스를 따라다니는 반투명 이미지
├── StartDrag(Sprite)        — 고스트 이미지 활성화 + SetAsLastSibling()
├── UpdateDrag(Vector2)      — 고스트 이미지 위치 이동
└── EndDrag()                — 고스트 이미지 숨김

InventoryDragHandler / FormationSlotUI
├── OnBeginDrag  → InventoryUI.StartDrag() + canvasGroup.alpha = 0.5f
├── OnDrag       → InventoryUI.UpdateDrag()
└── OnEndDrag    → InventoryUI.EndDrag() + alpha 복구
```

### 3-2. 새 UI에 드래그 앤 드롭 추가 시

1. `InventoryUI.StartDrag/UpdateDrag/EndDrag` 패턴 참고하여 구현
2. 반드시 `dragGhostImage.raycastTarget = false` 설정 → 고스트 이미지가 Drop 이벤트를 막지 않도록

---

## 4. UniversalDataImporter 사용법

구글 스프레드시트 데이터를 `SkillAsset`, `PassiveAsset`, `TraitAsset`으로 자동 동기화하는 에디터 도구.

### 4-1. 사전 준비

1. 구글 시트 → `파일 → 공유 → 웹에 게시` → 특정 시트 탭 선택 → `.csv` 형식 → URL 복사

2. `Assets/Editor/UniversalDataImporter.cs` 열기:

```csharp
private const string SKILL_SHEET_URL   = "여기에_스킬_CSV_링크";
private const string PASSIVE_SHEET_URL = "여기에_패시브_CSV_링크";
private const string TRAIT_SHEET_URL   = "여기에_TRAIT_CSV_링크";
```

### 4-2. 사용 방법

```
Unity 메뉴 → Tools → Data → Sync Skills / Sync Passives / Sync Traits
```

### 4-3. 데이터 규칙

- **A열(첫 번째 열)**은 반드시 고유한 **ID** 값
- 기존 에셋에 같은 `id` 존재 → 해당 에셋만 **갱신(Update)**
- 없으면 신규 에셋 파일 생성 (`Skill_{ID}.asset` 등)
- 신규 스킬 에셋은 기본적으로 `ParametricDamageSkill` 타입으로 생성

> ⚠️ 대량 덮어쓰기 전에 반드시 프로젝트 백업 권장

---

## 5. 채집 오브젝트(GatherableObject) 설정

플레이어가 탐험 중 상호작용하여 **확률적**으로 결과를 얻는 오브젝트.

| 결과 | 클래스                           |
| ---- | -------------------------------- |
| 성공 | `RewardOutcomeSO` — 아이템 보상  |
| 실패 | `EmptyOutcomeSO` — 텍스트만 출력 |
| 함정 | `TrapOutcomeSO` — 스탯 감소      |

### 5-1. GatherableDataSO 생성 방법

**방법 A — CSV Importer (권장, 대량)**

```
Unity 메뉴 → Tools → Gatherable Data Importer
→ CSV URL 확인 (구글 시트 웹 게시 CSV 링크)
→ Target Path 확인 (기본: Assets/03_Data/Interactions/Gatherables)
→ Download & Import 클릭
```

같은 ID의 기존 에셋은 **내용만 갱신** (씬 연결 유지).

**방법 B — 수동 생성**

```
Project → 우클릭 → Create → Data → Definitions → Gatherable Object Data
```

인스펙터 필드:

- `Is Vigor Cost`: 소모 활기
- `Outcomes` 리스트: `WeightedOutcome` 추가 (Probability + Result Text + Outcome SO)

### 5-2. 씬 배치

1. 빈 오브젝트 생성 + 스프라이트 배치
2. `BoxCollider2D` 추가 (Is Trigger ✅)
3. `GatherableObject` 스크립트 추가
4. `Gatherable Data` 필드에 `GatherableDataSO` 할당
5. (선택) `Interacted Sprite`: 상호작용 후 교체될 이미지

### 5-3. 주요 트러블슈팅

| 증상                          | 원인                                     | 해결                                         |
| ----------------------------- | ---------------------------------------- | -------------------------------------------- |
| '활기 부족' 메시지 계속       | `vigorCost`가 현재 활기보다 높음         | `VigorManager` 인스펙터에서 활기 초기값 조정 |
| 함정에 걸렸는데 스탯 안 깎임  | `TrapOutcomeSO.TargetStat`이 잘못됨      | `"STR"`, `"AGI"` 등 정확한 코드 확인         |
| 보상 획득했는데 인벤토리 없음 | `RewardOutcomeSO`에 `RewardTable` 미연결 | 해당 Outcome 에셋에서 RewardTable 수동 연결  |
| Import 해도 파일 안 생김      | A열 ID가 비어있음                        | 시트 A열에 고유 ID 입력 확인                 |

---

## 6. 스테이지 데이터 설정 가이드

### 6-1. StageNormalMapData 생성

```
Project → 우클릭 → Create → Data → Stage → NormalMap
```

인스펙터 설정:

| 필드               | 설명                                                      |
| ------------------ | --------------------------------------------------------- |
| Stage Number       | 레거시 호환용 정수 (예: `1`)                              |
| **Stage Id**       | 고유 문자열 ID ⭐ 최우선 식별자 (예: `"Chapter1_Stage1"`) |
| Normal Map Prefabs | 탐험씬에서 사용할 맵 프리팹 배열                          |

### 6-2. 전투 웨이브 설정 (Context Waves)

```
Context Waves 리스트 → + 클릭 → 항목 추가
├── Context Type: TrapEncounter (함정 게이지 가득 찼을 때)
│                AfterPuzzle   (퍼즐 클리어 후 전투)
└── Wave Set: 해당 상황의 WaveSet 에셋 연결
```

> ⚠️ 레거시 필드 `Trap Encounter Wave`, `Post Puzzle Wave`는 **삭제됨**. 사용하지 않는다.

### 6-3. 포탈 연결

씬 내 `PortalController` 컴포넌트:

| 필드                     | 설정값                                                |
| ------------------------ | ----------------------------------------------------- |
| Current Stage Data       | 위에서 만든 `StageNormalMapData` 할당                 |
| Battle Context When Used | 이 포탈의 전투 상황 (`TrapEncounter` / `AfterPuzzle`) |

### 6-4. 작동 흐름

```
플레이어 포탈 진입
  → StageNormalMapData.StageId + ContextType → StageRuntimeContext 저장
  → BattleScene 로드
  → BattleWaveManager → StageDatabase에서 StageId 검색
  → Context Waves에서 현재 ContextType에 맞는 WaveSet 선택 → 전투 시작
```

---

_System Setup Guide — Lemegeton Project Documentation — 2026-03-05_
