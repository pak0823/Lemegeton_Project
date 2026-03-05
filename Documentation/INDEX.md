# Lemegeton Project — Documentation 인덱스

**프로젝트:** Lemegeton_Project (Unity 2D RPG)
**엔진:** Unity URP 14.0.12 / 2D Tilemap
**문서 최종 갱신:** 2026-03-05
**총 문서 수:** 14개

---

## 🚀 인수인계 필수 문서 (새 개발자 시작점)

> 처음 합류하는 개발자는 **이 순서대로** 읽는다.

| 순서 | 문서 파일                                                      | 내용                                                       |
| :--: | -------------------------------------------------------------- | ---------------------------------------------------------- |
|  1   | **[DevSetup_Guide.md](DevSetup_Guide.md)**                     | Unity 설치, 저장소 클론, Addressables 빌드 등 환경 세팅    |
|  2   | **[INDEX.md](INDEX.md)**                                       | 이 파일 — 전체 문서 구조 파악                              |
|  3   | **[Research.md](Research.md)**                                 | 프로젝트 전체 아키텍처 및 시스템 심층 분석 (메인 레퍼런스) |
|  4   | **[ContentCreation_Workflow.md](ContentCreation_Workflow.md)** | 캐릭터/스킬/맵/아이템 추가 방법                            |
|  5   | **[TeamConvention_Guide.md](TeamConvention_Guide.md)**         | Git, 커밋 규칙, 코드 스타일, 네이밍                        |

---

## 📁 파트별 상세 문서

### 🗡️ Part 1: 전투 시스템

> `Assets/01_Scripts/Battle/`

| 문서 파일                                                        | 내용                                                                                          |
| ---------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| **[Part1_BattleSystem_Detail.md](Part1_BattleSystem_Detail.md)** | 헥스 그리드, ATB, FSM, 7개 서브시스템, BattleUnit, 상태이상, 데미지 계산, AI, 스킬 계층, 보상 |

---

### 🗺️ Part 2: 탐험 시스템

> `Assets/01_Scripts/Exploration/`, `Assets/01_Scripts/Interactables/`

| 문서 파일                                                                  | 내용                                                                          |
| -------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| **[Part2_ExplorationSystem_Detail.md](Part2_ExplorationSystem_Detail.md)** | MapManager 4종, BFS 경로탐색, Vigor, Fog, Persistence, QTE, 상호작용 오브젝트 |

---

### ⚔️ Part 3: 캐릭터 / 스킬 / 인벤토리

> `Assets/01_Scripts/Data/`, `Assets/01_Scripts/Battle/Skills/`

| 문서 파일                                                                                  | 내용                                                        |
| ------------------------------------------------------------------------------------------ | ----------------------------------------------------------- |
| **[Part3_Character_Skill_Inventory_Detail.md](Part3_Character_Skill_Inventory_Detail.md)** | UnitData, 진형, 패시브 12종, 6대 스탯, 인벤토리, 제작, 저장 |
| [Lemegeton_InventorySystem_Analysis.md](Lemegeton_InventorySystem_Analysis.md)             | 인벤토리 & 제작 MVP/SOLID 아키텍처 분석                     |
| [Skill_CSV_Structure_Guide.md](Skill_CSV_Structure_Guide.md)                               | 스킬 CSV 데이터 구조 가이드                                 |

---

### 🏗️ Part 4: UI / 코어 인프라 / 데이터 파이프라인

> `Assets/01_Scripts/UI/`, `Assets/01_Scripts/Core/`, `Assets/Editor/`

| 문서 파일                                                                    | 내용                                                              |
| ---------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| **[Part4_UI_Infrastructure_Detail.md](Part4_UI_Infrastructure_Detail.md)**   | 캠프 UI, 팝업, GameEventBus, 싱글톤, 에디터 도구, 기술 부채       |
| [Lemegeton_Asset_Management_Report.md](Lemegeton_Asset_Management_Report.md) | Addressables 에셋 관리, 폴더 구조, 데이터 파이프라인, 인코딩 전략 |

---

## 🔧 시스템별 설정 & 제작 가이드

| 문서 파일                                    | 내용                                                                                                       |
| -------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| [SystemSetup_Guide.md](SystemSetup_Guide.md) | 탐험 상태이상 씬 배치, FloatingText, 드래그앤드롭 패턴, 데이터 임포터, 채집 오브젝트, 스테이지 데이터 설정 |

---

## 📚 참고 자료

| 문서 파일                  | 내용                                            |
| -------------------------- | ----------------------------------------------- |
| [Reusable.md](Reusable.md) | 프로젝트에서 추출 가능한 재사용 시스템 카탈로그 |

---

## 📋 현재 프로젝트 상태 요약 (2026-03-05)

### 기술 스택

| 분류        | 상세                           |
| ----------- | ------------------------------ |
| 엔진        | Unity URP 14.0.12 / 2D Tilemap |
| 에셋 관리   | Addressables 1.22.3            |
| UI 텍스트   | TextMesh Pro 3.0.7             |
| 비동기 처리 | UniTask (Cysharp) — 통일 완료  |
| 입력 처리   | New Input System 1.14.2        |
| 스크립트 수 | 251+ .cs 파일                  |
| 문서 수     | 14개 .md 파일                  |

### 파트별 완성도

| 파트                 | 완성도 | 비고                        |
| -------------------- | ------ | --------------------------- |
| 전투 시스템          | ★★★★☆  | LuckySix 패시브 일부 미완성 |
| 탐험 시스템          | ★★★★☆  | 리팩토링 완료, 안정화됨     |
| 캐릭터/스킬/인벤토리 | ★★★★☆  | 저장 시스템 안정화 완료     |
| UI/인프라            | ★★★☆☆  | 오디오 시스템 미구현 🔴     |

### 주요 미완성 항목

- 🔴 **오디오 시스템 전무** — AudioManager 미구현
- 🟡 `BattleUnit.cs`, `BattleManager.cs`, `CampSkillPage.cs` 추가 분리 필요
- 🟡 `LuckySixShootingInsightPassive` TODO 3개 미구현
- 🔵 단위 테스트 전무 (Unity Test Framework 설치됨)
- 🔵 싱글톤 26개+ 구조 개선 (장기)

---

_Documentation Index — Lemegeton Project — 2026-03-05_
