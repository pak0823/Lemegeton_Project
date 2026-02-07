# Lemegeton_Project 기술 상세 분석 보고서

**작성일시**: 2026-02-05
**참조**: Assets/01_Scripts 및 Editor 코드 분석
**버전**: Final Audit (5차 정밀 분석 취합)

---

## 1. 프로젝트 개요 및 아키텍처 (Architecture Overview)

**Lemegeton_Project**는 **타일맵(Tilemap)** 기술을 기반으로 탐험과 전투를 매끄럽게 연결하는 하이브리드 RPG입니다. Unity의 **Addressables** 시스템을 적극 활용하여 에셋 관리를 최적화하고 있으며, **구글 스프레드시트**를 통한 데이터 주도적(Data-Driven) 개발 환경을 구축하고 있습니다.

### 핵심 시스템 구조 (Core Systems)

1.  **Exploration (탐험)**: 활기(Vigor) 자원, 실시간 그리드 이동, QTE 및 퍼즐 기믹.
2.  **Battle (전투)**: FSM 기반 턴제, ATB/과로 시스템, 확률 기반 AI.
3.  **Management (운영)**: 캠프(Camp) UI - 성장, 스킬, 아이템 제작.
4.  **Foundation (기반)**: 전역 팝업, 씬 전환, 타이틀.
5.  **Data (데이터)**: 영속성(Persistence), 파이프라인(Importer), 정적 DB.

---

## 2. 모듈별 상세 분석 (Module Deep Dive)

### 2.1 탐험 및 상호작용 시스템 (Exploration & Interactables)

**위치**: `Assets/01_Scripts/Exploration`, `Assets/01_Scripts/Interactables`

단순 이동을 넘어 물리적 퍼즐 요소를 포함하며, 최근 **보상 시스템 통합 및 안정화(2026-02-07)** 작업을 완료했습니다.

- **Exploration System Components**:
  - **ExplorationMapLoader.cs**: 맵의 생성, 파괴 및 **전투 복귀 시 기존 맵 검색/복구**를 전담합니다.
  - **ExplorationEntitySpawner.cs**: 플레이어 및 상호작용 오브젝트(상자/함정)의 스폰 로직을 분리했습니다.
  - **ExplorationPersistenceManager.cs**: 스냅샷(Snapshot) 기반의 상태 저장/복구 및 어드레서블 메모리 관리를 담당합니다.
- **Interactables**:
  - **BoxInteract.cs**: `RewardTableSO`와 연동되어 아이템을 지급합니다. 최근 **획득 타이밍과 로그 출력 동기화(1.5초 지연)**가 적용되었습니다.
  - **RewardTableSO**: 확률 기반 보상 테이블로, 아이템 지급과 함께 '플레이버 텍스트'를 반환하는 통합 구조로 개선되었습니다.
- **VigorManager.cs**: 탐험 자원(활기) 관리. 원자적(Atomic) 연산을 보장하여 중복 차감을 방지합니다.
- **Puzzle System (퍼즐)**:
  - **PushObject.cs**: 타일맵 그리드(Offset Grid) 위에서 물체를 미는 '소코반(Sokoban)' 스타일 퍼즐을 구현합니다.

### 2.2 캠프 및 운영 시스템 (Camp & Management)

**위치**: `Assets/01_Scripts/UI/Camp`

- **InventoryManager.cs**: **[Update]** `DontDestroyOnLoad`가 적용된 전역 싱글톤으로 강화되었습니다. 씬 전환 시 파괴되지 않으며, 중복 생성 시 자기 자신을 파괴하여 유일성을 보장합니다.
- **CampUIManager.cs**: Status, Skill, Craft 탭을 총괄하는 허브.
- **CampCraftPage.cs**: 재료 소모 및 아이템 제작 로직 (인벤토리 연동).

### 2.3 전투 시스템 (Battle System)

**위치**: `Assets/01_Scripts/Battle`

#### A. 코어 로직

- **BattleManager.cs**: 전투 FSM 상태 관리.
- **BattleTurnManager.cs**: ATB 및 과로(Overwork) 리스크 관리.

#### B. 유닛 및 상태 (Units & Status)

- **StatusController.cs**: 스택형 상태이상(출혈/중독 등)과 비선형 데미지 공식 적용.
- **EnemyAI.cs**: `WeightedSO`를 활용한 가중치 기반 스킬 선택 AI.

### 2.4 데이터 아키텍처 (Data Architecture)

- **Persistence**: `PlayerDataManager` (Save/Load).
- **Pipeline**: `UniversalDataImporter` (CSV -> SO 변환).
- **Encoding**: **[New]** 모든 스크립트 파일(`.cs`)의 인코딩을 **UTF-8 with BOM**으로 통일하여 한글 주석 깨짐 및 컴파일 경고를 영구적으로 해결했습니다.

---

## 3. 현황 진단 및 한계점 (Current Status & Limitations)

최근 코드 감사 및 버그 수정(2026-02-07)을 통해 시스템 안정성이 대폭 향상되었습니다.

### 3.1 구현 완료 (Implemented)

- [x] 타일맵 기반 이동 및 충돌 처리
- [x] 전투 FSM 및 턴 매니지먼트
- [x] 데이터 파이프라인 (Data Importer)
- [x] 오브젝트 푸시 퍼즐 (Sokoban)
- [x] UI 프레임워크 (Popups, Menus)
- [x] **맵 시스템 리팩토링 및 영속성 수정 (Map Persistence Fix)**
- [x] **보상 시스템 아키텍처 개선 (RewardTableSO 통합)**
- [x] **인벤토리 싱글톤 생명주기 안정화**

### 3.2 미구현 또는 보완 필요 (Missing / To-Do)

- [ ] **오디오 시스템 (Audio System)**: `Assets` 리스트 및 `OptionsMenuUI` 분석 결과, 사운드 매니저나 볼륨 컨트롤 로직이 전무합니다. 사운드 구현이 시급합니다.
- [ ] **저장 시스템 확장**: `PushObject`의 `SaveState` 메서드가 주석 처리되어 있어 퍼즐 상태 저장이 완전하지 않을 수 있습니다.

---

## 4. 분석 결론

**Lemegeton_Project**는 **탐험(퍼즐/자원)**과 **전투(전략/AI)**가 잘 결합된 견고한 구조를 가지고 있습니다. 최근 **씬 전환 시 데이터 유실 문제**와 **보상 타이밍 비동기 문제**를 해결함으로써 상용화 수준의 안정성을 확보했습니다. 남은 과제인 **오디오 시스템** 구현만 완료된다면 프로젝트의 기술적 완성도는 매우 높아질 것입니다.
