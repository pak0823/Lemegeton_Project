# Lemegeton_Project 에셋 관리 및 데이터 구조 분석 보고서

**작성일시**: 2026-02-05
**버전**: 4.0 (최종 전수 감사 반영)
**참조**: Assets 루트 전체 및 Editor/Test 폴더 상세 분석

---

## 1. 개요 (Overview)

본 문서는 **Lemegeton_Technical_Report.md**에서 다룬 기술 아키텍처가 실제 **Unity 프로젝트 폴더 및 에셋 구조**에 어떻게 반영되어 관리되고 있는지 분석한 최종 정밀 보고서입니다. **Addressables 전면 도입**, **개발 툴(Editor)**, 그리고 **테스트 자산 격리 전략**까지 포괄적으로 다룹니다.

---

## 2. 폴더 구조 및 네이밍 규칙 (Directory Structure)

프로젝트는 `Assets` 루트 하위에 숫자를 붙여 폴더를 정렬하는 **Numbered Folder** 방식을 사용하며, 레거시와 테스트 자산을 철저히 분리하고 있습니다.

| 폴더명                    | 상태         | 상세 분석                                                                                     |
| :------------------------ | :----------- | :-------------------------------------------------------------------------------------------- |
| **00_Scenes**             | 사용         | `Title`, `Exploration`, `Battle` 3대 핵심 씬 및 테스트 씬 관리                                |
| **01_Scripts**            | 사용         | C# 소스 코드 (모듈별 분류)                                                                    |
| **02_Prefabs**            | 사용         | 게임 오브젝트 프리팹 (Scene/System 분류)                                                      |
| **03_Data**               | 사용         | ScriptableObject 데이터 및 VisualDB                                                           |
| **04_Art**                | 사용         | 그래픽 리소스 (`Character`, `Ui`, `Effect` 등)                                                |
| **05_Audio**              | **비어있음** | 사운드 시스템 미구현 상태 (기술 리포트와 일치)                                                |
| **06_Plugins**            | **비어있음** | 외부 네이티브 플러그인 의존성 없음 (순수 C# 환경)                                             |
| **07_Test**               | **격리**     | 실험용 맵(`TestMap`), 임시 스프라이트 등이 격리되어 메인 리소스 오염 방지                     |
| **AddressableAssetsData** | 사용         | 리소스 로딩 파이프라인의 핵심                                                                 |
| **Editor**                | 사용         | 커스텀 인스펙터(`ParametricDamageSkillEditor`) 및 데이터 임포터(`UniversalDataImporter`) 포함 |
| **Resources**             | **비어있음** | **Addressables 100% 전환 완료** (Legacy Load 미사용)                                          |

---

## 3. 프리팹 관리 (02_Prefabs)

프리팹은 시스템(System)과 씬(Scene) 단위로 명확히 모듈화되어 있습니다.

### 3.1 Battle & Exploration

- **Battle**: `BattleManager`와 유닛(`Enemy_spider` 등)이 모여 있으며, `EnemyStat` 같은 기본형 프리팹을 상속받아 파생형을 만드는 구조입니다.
- **Exploration**: `Box`, `Potal` 등 기믹과 `InteractionHintUI` 등 월드 UI가 포함됩니다.

### 3.2 Common (공통 시스템)

- **UI Framework**: `PopUpManager`, `ModalManager`, `FloatingTextManager` 등 전역 싱글톤 매니저들이 집중 관리됩니다. 이는 씬 전환 시에도 파괴되지 않거나(DontDestroy), 모든 씬에서 공통적으로 쓰이는 인프라입니다.

---

## 4. 데이터 및 리소스 파이프라인 (Data & Pipeline)

### 4.1 Addressables (어드레서블)

**위치**: `Assets/AddressableAssetsData`

- **전략**: `Resources` 폴더를 비우고 모든 동적 에셋을 Addressables Group으로 관리합니다.
- **그룹핑**: `Assets_UnitPrefabs` (유닛), `Data_Items` (아이템) 등으로 세분화되어 있어 메모리 로드/언로드 효율이 높습니다.

### 4.2 VisualDB (시각 데이터 분리)

**위치**: `Assets/03_Data/VisualDB`

- **구조**: 로직(`Data`)과 리소스(`Art`)의 결합을 끊는 중간 매핑 테이블(`BuffIcon` 등)이 존재합니다. 이는 기획자와 아티스트의 작업 의존성을 낮추는 고도화된 설계입니다.

### 4.3 Editor Tools (개발 도구)

**위치**: `Assets/Editor`

- **Data Import**: `UniversalDataImporter.cs`를 통해 구글 시트 데이터를 SO로 자동 변환합니다.
- **Custom Inspector**: `ParametricDamageSkillEditor.cs` 등으로 복잡한 스킬 데이터를 인스펙터에서 쉽게 편집하도록 지원합니다.

---

## 5. 테스트 전략 (Test Isolation)

**위치**: `Assets/07_Test`

- **격리**: `01-1 Map`, `TestMap` 등 실험적인 맵 데이터와 `ResolutionTester` 등 테스트 스크립트가 이 폴더에 모여 있습니다.
- **이점**: 빌드 시 이 폴더를 제외하기 쉬운 구조이며, 메인 프로젝트 폴더(`01_Scripts` 등)를 더럽히지 않고 실험을 진행할 수 있습니다.

---

## 6. 결론 및 제언

### 6.1 아키텍처 강점

1.  **완전한 Addressables 전환**: 레거시(Resources)를 완전히 배제하여 모바일 최적화 및 DLC 확장에 대비되어 있습니다.
2.  **테스트 격리(Isolation)**: `07_Test` 폴더 운용으로 프로젝트의 청결도(Clean Project)가 높게 유지됩니다.
3.  **데이터 파이프라인**: `Editor` 툴을 통한 자동화가 구축되어 있어 데이터 밸런싱 효율이 높습니다.

### 6.2 보완 제언

- **오디오 분류 체계 수립**: `05_Audio`가 비어있으므로, 사운드 구현 시 `BGM/SFX` 폴더링과 Addressables Group 계획을 미리 수립해야 합니다.
- **UI 프리팹 관리**: 전역 UI(`Common`)와 씬 UI(`Exploration` 등)가 나뉘어 있는데, `02_Prefabs/UI` 폴더를 신설하여 UI 관련 리소스를 일원화하면 관리 효율이 더 높아질 것입니다.
