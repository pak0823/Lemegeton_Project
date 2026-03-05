# 콘텐츠 제작 워크플로우 가이드 (Content Creation Workflow)

**분류:** 인수인계 — 콘텐츠 제작 워크플로우
**작성일:** 2026-03-05
**대상:** 새 캐릭터, 스킬, 맵, 아이템을 추가하는 개발자

---

## 목차

1. [새 캐릭터(유닛) 추가](#1-새-캐릭터유닛-추가)
2. [새 스킬 추가](#2-새-스킬-추가)
3. [새 패시브 추가](#3-새-패시브-추가)
4. [새 적(Enemy) 추가](#4-새-적enemy-추가)
5. [새 맵 추가](#5-새-맵-추가)
6. [새 아이템 추가](#6-새-아이템-추가)
7. [새 상태이상 추가](#7-새-상태이상-추가)
8. [새 상호작용 오브젝트 추가](#8-새-상호작용-오브젝트-추가)
9. [Addressables 에셋 등록 방법](#9-addressables-에셋-등록-방법)

---

## 1. 새 캐릭터(유닛) 추가

### 전체 순서

```
1. UnitData SO 생성
2. 배틀 프리팹 생성
3. 애니메이터 설정
4. 스킬 슬롯 연결
5. Addressables 등록
6. PlayerDataManager에 추가
```

### 단계별 상세

#### Step 1 — UnitData ScriptableObject 생성

```
Project 창 → Assets/03_Data/Unit/Player/ (또는 Enemy/) 폴더
→ 우클릭 → Create → Lemegeton → UnitData
→ 파일명: [UnitName]Data.asset
```

인스펙터에서 설정:

- `unitID`: 고유 정수 ID (기존 ID와 중복 금지)
- `DisplayName`: 화면 표시 이름 (한글 가능)
- 6대 스탯: `baseSTR`, `baseCLV`, `baseAGI`, `baseBDY`, `baseMND`, `baseINS`
- `team`: `Player` 또는 `Enemy`
- `isBoss`: 보스 여부
- `baseHostility`: 적 타겟팅 가중치 (적 전용)

#### Step 2 — 배틀 프리팹 생성

```
Assets/02_Prefabs/Battle/ 폴더
→ 기존 캐릭터 프리팹 복사 후 이름 변경
→ BattleUnit 컴포넌트 확인
→ UnitData 필드에 Step 1에서 만든 SO 연결
```

**프리팹 필수 컴포넌트:**

- `BattleUnit` — 핵심 유닛 컨트롤러
- `UnitStats` — 스탯 관리 컴포넌트
- `UnitMover` — 이동 컴포넌트
- `UnitVisual` — 애니메이션/VFX 컴포넌트
- `StatusController` — 상태이상 관리
- `UnitStateController` — 특수 상태 관리

#### Step 3 — 애니메이터 설정

```
Assets/04_Art/Character/[UnitName]/ 폴더 생성
→ 스프라이트 시트 임포트 (.png)
→ 애니메이션 클립 생성 (Idle, Walk, Attack, Hurt, Die)
→ Animator Controller 생성 후 클립 연결
→ 프리팹의 Animator에 Controller 할당
```

UnitData의 `skillAnimBindings`에 스킬 ID → 애니메이션 이름 매핑을 추가한다.

#### Step 4 — 스킬 슬롯 연결

UnitData 인스펙터:

- `skills[]`: 사용 가능한 스킬 에셋 배열
- `passives[]`: 패시브 에셋 배열
- `traits[]`: 특성 에셋 배열 (2/6/12 유대 순서)

#### Step 5 — Addressables 등록

[9번 섹션 참고](#9-addressables-에셋-등록-방법)

배틀 프리팹을 Addressables 그룹에 추가하고 라벨을 설정한다.

#### Step 6 — PlayerDataManager에 추가

```csharp
// PlayerDataManager.cs
// 초기 보유 유닛 목록에 추가
// LoadStartingUnitsByLabel() 에서 로딩하거나
// ownedUnits 리스트에 직접 UnitData를 참조 추가
```

---

## 2. 새 스킬 추가

### 스킬 타입 선택 기준

| 원하는 기능                                   | 사용할 템플릿              |
| --------------------------------------------- | -------------------------- |
| 단순/범위 데미지, 상태이상 부여, 투사체, 넉백 | `ParametricDamageSkill`    |
| 회복 (HP/MP)                                  | `ParametricHealSkill`      |
| 소생, 버프 지원                               | `ParametricSupportSkill`   |
| 방향 기반 공격                                | `ParametricDirectionSkill` |
| 자신에게 상태 부여 (매복, 경계 등)            | `SelfStateSkill` 계열      |
| 아군 위치 교체                                | `AllyRetreatSwapSkill`     |
| 조건부 복합 스킬                              | `StateConditionalMulti`    |

### 단계별 상세 (ParametricDamageSkill 예시)

#### Step 1 — 스킬 에셋 생성

```
Assets/03_Data/Skills/Player/ (또는 Enemy/) 폴더
→ 우클릭 → Create → Lemegeton → Skills → ParametricDamageSkill
→ 파일명: [SkillName]Skill.asset
```

#### Step 2 — 핵심 파라미터 설정

| 필드              | 설명                | 예시                       |
| ----------------- | ------------------- | -------------------------- |
| `id`              | 고유 스킬 ID 문자열 | `"gigant_slash"`           |
| `displayName`     | 표시 이름           | `"대검 베기"`              |
| `school`          | 피해 속성           | `Physical` / `Magical`     |
| `power`           | 기본 배수           | `1.2f`                     |
| `costResource`    | 비용 자원           | `MP` / `Rage`              |
| `cost`            | 비용량              | `20`                       |
| `cooldownTurns`   | 재사용 대기 턴      | `2`                        |
| `targetMode`      | 타겟 방식           | `Unit` / `Tile`            |
| `targetAlignment` | 타겟 진영           | `Enemy` / `Ally` / `Self`  |
| `AreaPreset`      | 범위 프리셋         | `Single` / `Line` / `Cone` |
| `animKind`        | 애니메이션 종류     | `Melee` / `Ranged`         |

#### Step 3 — 고급 파라미터 (선택)

| 필드                     | 설명                                            |
| ------------------------ | ----------------------------------------------- |
| `applyStatusOnHit`       | 적중 시 부여할 상태이상 (StatusEffectInfo 배열) |
| `changeTileTo`           | 적중 타일 변경 (장판 생성)                      |
| `projectilePrefab`       | 투사체 프리팹                                   |
| `UseFrontlineBonus`      | 전방 유닛 추가 피해 여부                        |
| `conditionalMultipliers` | 특정 상태이상 대상 추가 배율                    |
| `trainingRoutes`         | 훈련 루트 변형 정보 (최대 3개)                  |

#### Step 4 — 캐릭터에 스킬 연결

해당 캐릭터의 `UnitData` → `skills[]` 배열에 추가.

---

## 3. 새 패시브 추가

### 패시브 스크립트 생성

```
Assets/01_Scripts/Battle/Skills/Player/Passive/Character/[CharacterName]/
→ [CharacterName][PassiveName]Passive.cs 생성
```

```csharp
// 패시브 스크립트 기본 구조
[CreateAssetMenu(menuName = "Lemegeton/Passives/[CharacterName]/[PassiveName]")]
public class [CharacterName][PassiveName]Passive : PassiveAsset
{
    public override void OnInit(BattleUnit owner, BattleManager bm)
    {
        // 초기화 (이벤트 구독 등)
    }

    public override void OnDispose()
    {
        // 정리 (이벤트 구독 해제)
    }
}
```

### 패시브 에셋 생성

```
Assets/03_Data/Skills/Player/ 폴더
→ 우클릭 → Create → Lemegeton → Passives → [PassiveName]
```

### 해금 조건 설정

`PassiveAsset`에 `unlockBond` 필드로 해금 유대 수치 설정 (0 = 기본 해금, 2/6/12 = 유대 해금).

---

## 4. 새 적(Enemy) 추가

### 단순 적 추가 (기존 AI 사용)

1. **UnitData SO 생성** (`Assets/03_Data/Unit/Enemy/`)
   - `team = Enemy`
   - `isBoss = Normal`
   - `baseHostility` 설정 (타겟 우선순위 가중치)

2. **EnemySkill 사용** — `Assets/03_Data/Skills/Enemy/`에서 적 전용 스킬 에셋 생성

3. **배틀 프리팹 생성** — `EnemyAI` 컴포넌트 추가 (기본 가중치 랜덤 AI)

4. **WaveSet SO에 등록** — `Assets/03_Data/StageDB/`의 스테이지 데이터에 적 추가

### 보스 적 추가

1. `isBoss = Boss` 설정
2. 프리팹에 `BossEnemyAI` 컴포넌트 추가
3. `BossEnemyAI`의 패턴 배열(웨이브/체력 조건별)에 스킬 등록

### 거미 모델(사전 시전) 추가

`EnemyCastState` 컴포넌트 추가 → `PendingCast` 구조체에 투사체/덫 프리팹 설정.

---

## 5. 새 맵 추가

### 전체 순서

```
1. 맵 프리팹 제작 (Tilemap 구성)
2. MapIDBaker 실행 (고유 ID 자동 부여)
3. Addressables 등록
4. StageDatabase에 맵 등록
5. 포탈 연결 (MapConnectionData 설정)
```

#### Step 1 — 맵 프리팹 제작

```
Assets/02_Prefabs/Exploration/ 폴더
→ 기존 맵 프리팹 참고하여 새 프리팹 생성
→ Tilemap 레이어 구성:
   • FloorMap      — 바닥 타일
   • ObstacleMap   — 장애물 타일
   • WallMap       — 벽 타일
```

**필수 컴포넌트/설정:**

- Grid 컴포넌트 (Cell Size, Cell Layout 통일)
- `tileAnchor` 값: 같은 층은 동일값, 다른 층은 0.55 이상 차이

#### Step 2 — MapIDBaker로 ID 부여

```
Unity 메뉴 → Tools → Lemegeton → Bake Map IDs
```

자동으로 각 맵 프리팹에 고유 PersistID가 부여된다.

#### Step 3 — Addressables 등록

[9번 섹션 참고](#9-addressables-에셋-등록-방법)

라벨 `ExplorationMap` 또는 해당 스테이지 라벨 추가.

#### Step 4 — StageDatabase 등록

```
Assets/03_Data/StageDB/StageDatabase.asset
→ Stage 배열에 새 스테이지 추가
→ StageNormalMapData에 맵 프리팹 배열 설정
→ MapDataAutoSetupTool로 일괄 자동 설정 가능
```

```
Unity 메뉴 → Tools → Lemegeton → Map Data Auto Setup
→ 대상 StageDatabase 선택 → Setup 실행
```

#### Step 5 — 포탈 연결

씬 내 `PortalController` 컴포넌트:

- `targetMapAddress`: 연결할 맵의 Addressables 주소
- `targetSpawnPointIndex`: 도착 스폰 포인트 인덱스
- `MapConnectionData` SO를 통해 연결 정보를 중앙 관리

---

## 6. 새 아이템 추가

#### Step 1 — ItemData SO 생성

```
Assets/03_Data/Item/ 폴더
→ 우클릭 → Create → Lemegeton → ItemData
```

| 필드               | 설명                                              |
| ------------------ | ------------------------------------------------- |
| `itemID`           | 고유 문자열 ID (`"mat_herb"`, `"cons_potion"` 등) |
| `itemName`         | 표시 이름                                         |
| `itemType`         | `Material` / `Consumable`                         |
| `maxStack`         | 슬롯당 최대 스택 수                               |
| `atlasAddress`     | Addressables Atlas 주소                           |
| `spriteName`       | Atlas 내 스프라이트 이름                          |
| `useContextEffect` | 소비 시 효과 SO (선택)                            |

#### Step 2 — 아이콘 등록

```
아이콘 .png를 Addressables Atlas에 추가
→ atlasAddress: "ItemAtlas"
→ spriteName: "icon_[이름]"
```

#### Step 3 — 효과 SO 생성 (소비형만)

```
Assets/01_Scripts/Data/Effects/ 참고
→ HealEffectSO 또는 RestoreMPEffectSO 인스턴스 생성
→ ItemData.useContextEffect에 연결
```

#### Step 4 — 보상 테이블에 등록

드롭 아이템으로 사용하려면 `RewardTableSO`에 추가:

```
Assets/03_Data/StageDB/[StageName]RewardTable.asset
→ itemPool 배열에 ItemData + 확률 가중치 추가
```

---

## 7. 새 상태이상 추가

### StatusId 범위 규칙

```
1~20   — 스킬 중첩 상태 (캐릭터별)
21~40  — 수치 보정 상태 (Defense, Resistance 등)
50~55  — 지속 피해 (Bleeding, Poisoning, Ignition)
→ 새 상태이상은 현재 미사용 ID 범위에서 추가 필요
```

### 수치 상태 추가 (StatusController)

1. `StatusId` enum에 새 값 추가 (`Assets/01_Scripts/Battle/Units/StatusController.cs`)
2. `StateStatModifierDB`에 피해 배율 추가
3. `StatusDescriptionDB`에 설명 텍스트 추가
4. `UnitStateVisualDB`에 시각 표현 추가

### 특수 상태 추가 (UnitStateController)

1. `UnitStateId` enum에 새 값 추가
2. `UnitStateController`에 처리 로직 추가 (턴 시작 시 효과 등)

---

## 8. 새 상호작용 오브젝트 추가

### 기본 구조

```csharp
// IInteractable 구현 필수
public class MyInteractable : MonoBehaviour, IInteractable, IExplorationPersistable
{
    [SerializeField] private string persistID;
    public string PersistID => persistID;

    public void Interact(PlayerMovement player) { ... }
    public void SaveState(ExplorationSnapshot snapshot) { ... }
    public void RestoreState(ExplorationSnapshot snapshot) { ... }
}
```

### 파일 위치

```
Assets/01_Scripts/Interactables/Props/ (소품형)
Assets/01_Scripts/Interactables/Traps/ (함정형)
Assets/01_Scripts/Interactables/Puzzles/ (퍼즐형)
```

### 영속성 등록

`ExplorationEntitySpawner`의 스폰 배열에 프리팹을 추가하고, `MapIDBaker`로 ID를 부여한다.

---

## 9. Addressables 에셋 등록 방법

### 기본 등록 절차

1. **Window → Asset Management → Addressables → Groups** 열기
2. 등록할 에셋을 Addressables Groups 창으로 드래그
3. **Address 이름** 설정 (예: `"Prefabs/Battle/GigantPrefab"`)
4. **라벨 설정** (중요):

| 라벨             | 등록할 에셋            |
| ---------------- | ---------------------- |
| `PlayerUnit`     | 플레이어 캐릭터 프리팹 |
| `EnemyUnit`      | 적 캐릭터 프리팹       |
| `ExplorationMap` | 탐험 맵 프리팹         |
| `ItemAtlas`      | 아이템 아이콘 아틀라스 |
| `SkillAsset`     | 스킬 에셋 SO           |

5. **빌드 실행:**
   ```
   Tools → Addressables → Build Addressables
   ```

### 주의 사항

- 에셋을 삭제하거나 이동하면 Address가 깨진다 → 반드시 Addressables Groups 창에서 이동/삭제
- 빌드하지 않으면 런타임에서 `InvalidKeyException` 발생
- 원격 배포(CDN)가 아닌 로컬 빌드 방식 사용 중이므로 빌드 후 `StreamingAssets/` 업데이트 여부 확인

---

_Content Creation Workflow — Lemegeton Project Documentation — 2026-03-05_
