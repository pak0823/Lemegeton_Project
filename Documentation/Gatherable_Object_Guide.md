# 채집 오브젝트 (Gatherable Object) 가이드

이 문서는 게임 내 **채집 오브젝트(Gatherable Object)** 시스템의 설정, 사용법, 데이터 생성 방법을 설명합니다.

---

## 1. 시스템 개요 (System Overview)

채집 오브젝트는 플레이어가 탐험 중 상호작용하여 **확률적**으로 결과를 얻는 오브젝트입니다.

- **성공**: 아이템 보상 획득 (`RewardOutcome`)
- **실패**: 아무 일도 일어나지 않음 (`EmptyOutcome`)
- **함정**: 스탯 감소 등 페널티 발생 (`TrapOutcome`)

또한 상호작용 시 **활기(Vigor)**를 소모합니다.

---

## 2. 데이터 생성 방법 (Data Generation)

### 2.1. CSV Importer 사용 (권장)

기획 구글 스프레드시트(CSV) 데이터를 사용하여 대량의 데이터를 자동으로 생성 및 **업데이트**할 수 있습니다.

1. **Unity 메뉴** > `Tools` > `Gatherable Data Importer` 실행.
2. `CSV URL` 항목에 **웹에 게시된 구글 시트 CSV 링크**가 입력되어 있는지 확인합니다.
   - _기본값으로 설정된 링크를 사용하시면 됩니다._
3. `Target Path`를 확인합니다 (기본값: `Assets/03_Data/Interactions/Gatherasble`).
4. `Download & Import` 버튼 클릭.
5. **결과**:
   - `ID` 컬럼이 있는 행에 대해서만 `GatherableDataSO` 파일이 생성됩니다.
   - 이미 존재하는 `ID`의 파일은 **내용만 갱신(Update)**되므로, 씬에 배치된 오브젝트들의 연결이 끊어지지 않습니다.

> **참고**: 시트에서 `ID`가 없는 행은 무시됩니다. 데이터를 추가하려면 시트에 `ID`를 입력하고 다시 Import 하세요.
> **주의**: 보상 테이블(`RewardTableSO`)은 이름 매칭이 어렵기 때문에, 생성 후 `RewardOutcome` 항목을 열어 수동으로 연결해 주어야 할 수 있습니다.

### 2.2. 수동 생성

1. Project 창에서 우클릭 > `Create` > `Data` > `Definitions` > `Gatherable Object Data`.
2. 생성된 SO를 선택하고 Inspector에서 내용을 채웁니다.
   - `Is Vigor Cost`: 소모 활기.
   - `Outcomes`: 리스트에 +를 눌러 분기를 추가합니다.
   - 각 분기(`WeightedOutcome`)에 `Probability`(확률)와 `Result Text`(로그 텍스트)를 입력합니다.
   - `Outcome` 필드에는 `InteractionOutcomeSO`를 상속받은 에셋(`Trap`, `Reward` 등)을 연결합니다.

---

## 3. 씬 배치 (Scene Setup)

### 3.1. 프리팹 설정

`GatherableObject` 컴포넌트를 가진 프리팹을 만듭니다.

1. 빈 게임 오브젝트 생성 or 스프라이트 배치.
2. `BoxCollider2D` 추가 (Is Trigger 체크).
3. `GatherableObject` 스크립트 추가.
4. `Gatherable Data` 필드에 위에서 만든 `GatherableDataSO`를 할당합니다.
5. (선택) `Interacted Sprite`: 상호작용 후 바뀔 이미지를 지정합니다.

### 3.2. 테스트

1. 게임 실행.
2. 캐릭터를 오브젝트 근처로 이동.
3. 클릭하여 상호작용.
4. **결과 확인**:
   - `ExplorationLogUI`에 결과 텍스트가 뜨는지 확인.
   - 함정일 경우: `PlayerDataManager` 로그에 스탯 감소가 찍히는지 확인.
   - 성공일 경우: 보상 획득 로그 확인.

---

## 4. 스크립트 구조 (Scripts)

- **`GatherableObject.cs`**: 인게임 로직. 활기 체크 및 상호작용 처리.
- **`GatherableDataSO.cs`**: 데이터 컨테이너. 확률 로직(`PickOutcome`) 포함.
- **`InteractionOutcomeSO.cs`**: 결과 추상 클래스.
  - `TrapOutcomeSO`: 스탯 감소 (`PlayerDataManager.ApplyStatModifier` 호출).
  - `RewardOutcomeSO`: 아이템 지급 (`InventoryManager.GiveReward` 호출).
  - `EmptyOutcomeSO`: 텍스트만 출력.
- **`GatherableDataImporter.cs`** (Editor): 구글 시트 CSV 파싱 및 에셋 생성/갱신 툴.

---

## 5. 트러블 슈팅 (Troubleshooting)

### Q. '활기가 부족합니다' 메시지가 계속 빝니다.

- **원인**: `VigorManager`의 현재 활기가 `GatherableDataSO`의 `vigorCost`보다 낮습니다.
- **해결**: `VigorManager` 인스펙터에서 `Current Vigor`를 늘리거나, 데이터의 Cost를 낮추세요.

### Q. 함정에 걸렸는데 스탯이 안 깎입니다.

- **원인**: `TrapOutcomeSO`의 `Reduction Amount`가 0이거나, `Exclude Stat` 이름이 잘못되었습니다.
- **해결**: SO 파일을 확인하여 `Target Stat`이 "STR", "AGI" 등 정확한 코드인지 확인하세요.

### Q. 보상을 얻었는데 인벤토리에 없습니다.

- **원인**: `RewardOutcomeSO`에 `RewardTable`이 할당되지 않았습니다.
- **해결**: 임포터로 자동 생성된 경우 테이블 연결이 비어있을 수 있습니다. 해당 `Outcome` 에셋을 찾아 `RewardTable`을 연결해주세요.

### Q. Import를 했는데 파일이 안 생겨요.

- **원인**: 구글 시트에서 `ID` 컬럼이 비어있거나, 해당 `ID` 행의 데이터가 불완전할 수 있습니다.
- **해결**: 시트의 `ID` 컬럼(A열 근처)에 고유 번호(예: 1001)가 입력되어 있는지 확인하세요.
