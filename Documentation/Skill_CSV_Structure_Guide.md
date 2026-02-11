# Skill CSV Structure Guide (Updated)

이 문서는 `SkillDataImporter`가 사용하는 CSV 데이터의 구조와 작성 규칙을 설명합니다.  
최신 변경 사항(Override Description, Range 기반 접근 로직, 특수 스킬 매핑)이 반영되었습니다.

---

## 1. CSV 열 구조 (Columns)

CSV는 총 **13개 열**로 구성됩니다. 1행은 헤더이며, 데이터는 2행부터 시작합니다.

| Index  |     Column Header     | 필수 | 설명                                                             |
| :----: | :-------------------: | :--: | ---------------------------------------------------------------- |
| **0**  |         `ID`          |  V   | 스킬 고유 ID (정수). 훈련 행은 `Active ID + 01~02` 형식 권장.    |
| **1**  |        `Type`         |  V   | 행의 종류. `Active` (스킬 정의) 또는 `Training` (훈련 정의).     |
| **2**  |      `ParentID`       |  V   | `Active` 행은 `0`. `Training` 행은 부모 `Active` 스킬의 `ID`.    |
| **3**  |        `Name`         |  V   | 스킬 이름 (에셋 파일명). 훈련 행은 `스킬명 훈련` 형식 사용 권장. |
| **4**  |      `CostType`       |  -   | 비용 타입 (`MP`, `Rage`).                                        |
| **5**  |      `CostValue`      |  -   | 비용 수치 (정수).                                                |
| **6**  |     `TargetType`      |  -   | 타겟 유형 (`Self`, `Single`, `Tile`, `All`).                     |
| **7**  |        `Range`        |  -   | **사거리 및 접근(Gap Close) 판정용**. (상세 설명 참조)           |
| **8**  |     `FormulaType`     |  V   | **스킬의 기능 정의** (핵심).                                     |
| **9**  |       `Value1`        |  -   | 기능별 주요 수치 (float).                                        |
| **10** |      `ValueStr`       |  -   | 기능별 문자열 옵션 (enum 이름 등).                               |
| **11** |     `Description`     |  -   | 스킬 또는 훈련의 기본 설명.                                      |
| **12** | `OverrideDescription` |  -   | **(New)** 훈련 적용 시 변경될 스킬 설명. (`Training` 행 전용)    |

---

## 2. 주요 로직 상세

### A. Range (사거리)와 접근(Gap Close)

`Range` 값은 단순 사거리를 넘어 **애니메이션 및 이동 로직**을 결정합니다.

|  Range 값  |        판정         | 설명                                                          |
| :--------: | :-----------------: | ------------------------------------------------------------- |
| **0 ~ 1**  |  **Melee (근접)**   | 스킬 사용 시 대상에게 **점프 접근(Gap Close)** 후 공격합니다. |
| **2 이상** | **Ranged (원거리)** | 제자리에서 발사합니다. **접근하지 않습니다.**                 |

### B. Training Description (훈련 설명)

- **Description (11열)**: 훈련 선택 UI에 표시될 설명입니다. (예: "사거리가 증가합니다.")
- **OverrideDescription (12열)**: 이 훈련을 찍었을 때 **스킬 툴팁**에 표시될 설명입니다. (예: "사거리 <color=green>7</color>로 증가")

---

## 3. FormulaType & ValueStr 매핑 가이드

`FormulaType`과 `ValueStr`의 조합으로 다양한 스킬 효과를 구현합니다.

### 특수 스킬 (Active)

스킬 자체의 클래스 타입을 결정합니다.

| 스킬 종류       | 클래스                    | FormulaType      | ValueStr     | Value1       |
| --------------- | ------------------------- | ---------------- | ------------ | ------------ |
| **연막탄**      | `SmokeBombSkill`          | `Field_Create`   | `Smoke`      | 지속 턴      |
| **맹수 영역**   | `SelfBeastDomainSkill`    | `Field_Create`   | `BeastField` | 지속 턴      |
| **경계 태세**   | `SelfVigilanceSkill`      | `Status_Buff`    | `Guard`      | 지속 턴      |
| **쇄국 태세**   | `SelfIsolationTimedSkill` | `Status_Buff`    | `Isolation`  | 방어 지속 턴 |
| **도발/어그로** | `HostilitySpikeSkill`     | `Aggro_Up`       | (무관)       | 적의 배수    |
| **상태 정화**   | `SelfStateCleanseSkill`   | `Status_Cleanse` | (무관)       | (없음)       |

### 훈련 효과 (Training)

스킬의 기능을 강화하거나 변경합니다.

| 효과 분류        | FormulaType     | ValueStr (대소문자 주의)                      | Value1 (수치)   | 비고                              |
| ---------------- | --------------- | --------------------------------------------- | --------------- | --------------------------------- |
| **출혈 부여**    | `Status_Bleed`  | `Bleed` (권장)                                | 중첩 수 (Stack) | -                                 |
| **발화 부여**    | `Status_Burn`   | `Ignition` (권장)                             | 중첩 수         | -                                 |
| **공포 부여**    | `Status_Debuff` | `Fear`                                        | (없음)          | -                                 |
| **제압 부여**    | `Status_Debuff` | `Suppress`                                    | 추가 제압량     | **[Fix]** 이제 정상 작동함        |
| **범위 변경**    | `Modify_Range`  | 범위 프리셋 이름<br>(`LineDiagU3`, `Ring` 등) | (없음)          | ValueStr에 `AreaPreset` 이름 입력 |
| **이동 추가**    | `Post_Move`     | (무관)                                        | 이동 칸 수      | 사용 후 추가 이동                 |
| **비용 감소**    | `Cost_Reduce`   | (무관)                                        | 변경될 비용     | -                                 |
| **환급(처치시)** | `Resource_Gain` | `Kill`                                        | (없음)          | 처치 시 소모값 환급               |
| **스탯 버프**    | `Buff_Stat`     | `STR`                                         | 버프량?         | 구현에 따라 다름                  |
| **스탯 디버프**  | `Debuff_Stat`   | `AGI`                                         | (없음)          | 민첩 감소 등                      |

---

## 4. CSV 작성 예시 (Copy & Paste)

아래 표는 실제 CSV 포맷(`Name`까지만 표시, 뒷부분은 이어서 작성)의 예시입니다.

### **[예시: 리볼버 사격 (Revolver Shot)]**

- **기본**: 물리 데미지, 사거리 5 (원거리)
- **훈련 1**: `Modify_Range` (사거리를 세로 3칸으로 변경)
- **훈련 2**: `Status_Debuff` (제압 부여)
- **훈련 3**: `Post_Move` (사용 후 1칸 이동)

|   ID   |   Type   | ParentID | Name         | CostType | CostValue | TargetType | Range | FormulaType   | Value1 | ValueStr          | Description              | OverrideDescription                            |
| :----: | :------: | :------: | ------------ | -------- | --------- | ---------- | ----- | ------------- | ------ | ----------------- | ------------------------ | ---------------------------------------------- |
| 101100 |  Active  |    0     | RevolverShot | MP       | 10        | Single     | **5** | Damage_Phys   | 1.0    |                   | 리볼버 사격              |                                                |
| 101101 | Training |  101100  | 범위 확대    |          |           |            |       | Modify_Range  | 0      | **LineVertical3** | 범위가 세로 3칸으로 변경 | 범위가 <color=yellow>세로 3칸</color>으로 변경 |
| 101102 | Training |  101100  | 제압 사격    |          |           |            |       | Status_Debuff | **2**  | **Suppress**      | 적중 시 제압 수치 2 감소 | 적중 시 <color=red>제압 2</color> 감소         |
| 101103 | Training |  101100  | 치고 빠지기  |          |           |            |       | Post_Move     | **1**  |                   | 사격 후 1칸 이동 가능    | 사격 후 <color=blue>1칸 이동</color> 가능      |

_(위 표의 `TargetType`, `Range` 등 빈 칸은 Training 행에서 상속받지 않거나 필요 없는 값입니다. 쉼표 개수는 유지해야 합니다.)_
