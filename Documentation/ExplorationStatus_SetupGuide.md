# [탐험 상태이상 시스템 설정 가이드]

스크립트 작성은 완료되었으나, Unity 에디터에서 **오브젝트 배치 및 연결 작업**이 필요합니다. 아래 순서대로 설정을 진행해주세요.

## 1. 매니저 설치 (ExplorationStatusManager)

1. **Hierarchy** 창에서 빈 게임 오브젝트를 생성하고 이름을 `ExplorationSystem` (또는 원하는 이름)으로 변경합니다.
2. 생성한 오브젝트에 `ExplorationStatusManager` 컴포넌트를 추가합니다.
   - _팁: `ExplorationStatusManager`는 Singleton이므로 씬에 단 하나만 존재해야 합니다._

## 2. 데이터 생성 (ScriptableObject)

1. **Project** 창의 `Assets/Resources/Data` (또는 원하는 폴더)로 이동합니다.
2. 우클릭 -> `Create` -> `Lemegeton` -> `Exploration` -> `Status Data`를 선택하여 데이터 파일을 생성합니다. (이름: `ExplorationStatusData`)
3. 생성된 `ExplorationStatusData`를 선택하고 Inspector에서 `Status List`의 `+` 버튼을 눌러 항목을 추가합니다.
4. **Overweight (과중) 세팅**:
   - **ID**: `Overweight`
   - **Display Name**: 과중
   - **Description**: "소지품이 너무 많아 움직이기 힘듭니다.\n활기 소모량이 2배 증가합니다."
   - **Icon**: (준비된 디버프 아이콘 스프라이트 할당)
   - **Is Debuff**: 체크 (True)

## 3. UI 설정 (ExplorationStatusUI)

1. **Hierarchy** 창의 Canvas 하위에 상태 아이콘들이 나열될 부모 오브젝트(`Panel_StatusIcons` 등)를 생성합니다.
   - `Horizontal Layout Group`을 추가하면 아이콘들이 자동으로 정렬됩니다.
2. 해당 오브젝트(또는 UI 관리자 오브젝트)에 `ExplorationStatusUI` 컴포넌트를 추가합니다.
3. 컴포넌트 속성을 연결합니다:
   - **Data DB**: 2번에서 만든 `ExplorationStatusData` 데이터를 드래그하여 할당.
   - **Icon Root**: 아이콘이 생성될 부모 Transform (방금 만든 `Panel_StatusIcons`).
   - **Icon Prefab**: 아이콘 프리팹 (Image 컴포넌트가 있는 프리팹)을 할당. 없다면 빈 이미지를 프리팹화하여 사용.

## 4. 최종 확인

1. 게임을 실행(`Play`)합니다.
2. 아이템을 11개 이상 획득하여 인벤토리를 채웁니다.
3. **결과 확인**:
   - 설정한 위치에 '과중' 아이콘이 생성되는지 확인.
   - 이동 시 활기가 2배(기본 2 -> 4)로 소모되는지 확인.
