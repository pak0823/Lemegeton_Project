# Lemegeton 코드 품질 개선 세션 리포트

**작성일**: 2026년 2월 20일  
**작업 요약**: 프로젝트 전반에 걸친 아키텍처 리팩토링, 성능 최적화, 잠재적 버그 픽스 수행

## 1. 🔴 Phase 1: Critical (즉시 수정 사항)

### 1-1. 비동기 예외 처리 보완 (`async void` → `UniTaskVoid`)

- **문제점**: `BattleStateMachine`, `PlayerDataManager` 등에서 리턴 타입이 `async void`인 비동기 메서드 호출로 인해 C# 예외가 무음으로 삼켜져 디버깅이 불가능했습니다.
- **해결**: 대상 메서드들을 `async UniTaskVoid`로 전환하고, `try-catch` 블록을 명시하여 문제가 발생하더라도 Exception Log가 정상 출력되도록 수정 완료했습니다. `MapManager`, `ConfirmationPopup` 등 곳곳의 누락도 모두 덮어 씌웠습니다.

### 1-2. 코드 파일 인코딩 및 Git 설정

- **문제점**: OS 및 에디터 차이로 인한 CRLF 혼용 (`\r\r\n`), 이로 인해 한글 주석이 깨지는 문제가 있었습니다.
- **해결**: `.gitattributes`에 `*.cs eol=lf` 속성을 추가하고, `.editorconfig` (utf-8 설정) 등을 마련하여 인코딩 이슈를 원천 차단했습니다.

### 1-3. 저장 시스템 일원화

- **문제점**: `PassiveAsset`의 해금 정보, `InventoryManager`의 세이브가 `PlayerPrefs` 곳곳에 분산되어 관리가 불가능했습니다.
- **해결**: `SaveData.cs` 모델을 `PlayerDataManager.cs` 중심으로 합치고, `Application.persistentDataPath`에 `savedata.json` 파일 형태로 일원화 저장하도록 구조를 바꿨습니다.

---

## 2. 🟡 Phase 2 & 3: Warning (성능 및 관리 최적화)

### 2-1. `FindObjectsOfType` 성능 낭비 제거

- **문제점**: 턴 관리나 승리 루틴에서 매 프레임/주기적으로 `FindObjectsOfType<BattleUnit>()`이 호출되어 성능 저하가 컸습니다.
- **해결**: `BattleManager`에 `_activeUnits` 레지스트리를 구축하여 등록/해제 구조로 바꾸었으며, 선형 탐색 부하를 최소화했습니다.

### 2-2. 개발 테스트 플래그 제어

- **문제점**: 테스트를 위한 하드코딩된 값들(`simulateHasSaveData`, 긱스 종료 턴 체크 등)이 프로덕션(빌드)에 남을 여지가 있었습니다.
- **해결**: 개발용 플래그에 `#if UNITY_EDITOR` 전처리기를 씌워 개발자 빌드 외의 바이너리에는 탑재되지 않도록 격리했습니다.

### 2-3. 럭키식스(LuckySix) 패시브 작업 완료

- **해결**: TODO로 남겨져 있던 통찰(InsightUp) 버프 로직 구현. 5스택 달성 시 버프 부여, 최대 스택 초과 시 초기화 등 실제 상태이상이 걸리도록 연동을 완료했습니다.

---

## 3. 🔵 Phase 4: Info (장기 개선)

### 3-1. 테스트 코드 어셈블리 분리

- **문제점**: `07_Test` 아래의 디버거 및 치트 스크립트가 게임 코어 어셈블리에 섞여 있었습니다.
- **해결**: `Lemegeton.Test.asmdef`를 분리하고 `includePlatforms: Editor`로 한정시켜, 메인 클라이언트가 더 이상 테스트 스크립트에 종속되지 않습니다.

### 3-2. 어드레서블 메모리 중앙 관리 (`ResourceTracker`)

- **문제점**: `Addressables`를 통해 에셋을 로딩할 때 반환되는 `AsyncOperationHandle`이 소실되어 Release 되지 못하는 누수 징후가 있었습니다.
- **해결**: `ResourceTracker.cs` 유틸리티를 작성하여 모든 로딩된 핸들을 한 곳에서 저장하고, `OnDestroy` 등에 안전히 한꺼번에 해제(`ReleaseAll`)하도록 `PlayerDataManager`부터 연결을 완료했습니다.

---

## 💡 종합 결론

총 4개의 Phase에 걸친 코드 품질 개선을 무사히 마무리했습니다.
기존 하드코딩과 성능 저하 요소들을 적절한 디자인 패턴(`ResourceTracker`, `UniTask`, `레지스트리 관리`)으로 교체하여 유지 보수성은 대폭 증가했고, 추후 맵 분리(God Object 분할) 작업을 이어나가기 위한 기반이 단단하게 다져졌습니다.
