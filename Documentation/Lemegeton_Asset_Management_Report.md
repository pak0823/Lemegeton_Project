# 에셋 관리 및 데이터 파이프라인 보고서

**작성일:** 2026-03-05 (최신화)
**원본:** 2026-02-05 v4.0 기준 — 현재 상태 반영하여 전면 갱신
**참조:** `Assets/` 루트 전체, `AddressableAssetsData/`, `Assets/Editor/`

---

## 1. Assets 폴더 구조

프로젝트는 `Assets` 루트 하위에 번호를 붙여 폴더를 정렬하는 **Numbered Folder** 방식을 사용한다.

| 폴더명                  | 상태            | 상세                                                                            |
| ----------------------- | --------------- | ------------------------------------------------------------------------------- |
| `00_Scenes`             | ✅ 사용         | TitleScene, ExplorationScene, BattleScene 3대 핵심 씬 + 07_Test 테스트 씬       |
| `01_Scripts`            | ✅ 사용         | C# 소스 코드 (251+ 파일, 도메인별 분류)                                         |
| `02_Prefabs`            | ✅ 사용         | 게임 오브젝트 프리팹 (Battle / Exploration / Common 분류)                       |
| `03_Data`               | ✅ 사용         | ScriptableObject 데이터 (Unit, Skills, Item, Stages, VisualDB 등 7개 하위 폴더) |
| `04_Art`                | ✅ 사용         | 그래픽 리소스 (Character, UI, Effect 등)                                        |
| `05_Audio`              | ⚠️ **비어있음** | **오디오 시스템 미구현** — 가장 시급한 미완성 항목                              |
| `06_Plugins`            | ✅ 비어있음     | 외부 네이티브 플러그인 없음 (순수 C# 환경)                                      |
| `07_Test`               | 🔒 **격리**     | `Lemegeton.Test.asmdef` 적용 → 릴리즈 빌드 자동 제외                            |
| `AddressableAssetsData` | ✅ 사용         | Addressables 그룹 정의 — 리소스 로딩 파이프라인 핵심                            |
| `Editor`                | ✅ 사용         | 커스텀 인스펙터, 데이터 임포터, 에디터 유틸리티                                 |
| `Resources`             | ✅ **비어있음** | Addressables 100% 전환 완료 → `Resources.Load()` 미사용                         |

---

## 2. Addressables 에셋 관리

### 전략

`Resources/` 폴더를 완전히 비우고 모든 동적 에셋을 Addressables Group으로 관리한다.

- 메모리 로드/언로드 제어 가능
- 번들 단위 DLC/패치 확장 준비
- `ResourceTracker`로 핸들 누수 방지 (PlayerDataManager 통합 완료)

### 주요 Addressables 그룹

| 그룹                 | 포함 에셋                         |
| -------------------- | --------------------------------- |
| `Assets_UnitPrefabs` | 플레이어/적 전투 프리팹           |
| `Data_Items`         | 아이템 ItemData SO                |
| `ExplorationMap`     | 탐험 맵 프리팹                    |
| `ItemAtlas`          | 아이템 아이콘 스프라이트 아틀라스 |
| `SkillAssets`        | 스킬 에셋 SO                      |

### Addressables 빌드

```
Unity 메뉴 → Tools → Addressables → Build Addressables
```

> ⚠️ 에셋 이동/삭제는 반드시 **Addressables Groups 창**에서 수행.
> 직접 파일 이동 시 Address 링크가 끊어짐.

---

## 3. 프리팹 관리 (02_Prefabs)

### Battle

- `BattleManager` 오케스트레이터 프리팹
- 유닛 프리팹 (`Enemy_Spider` 등) — `BattleUnit` 컴포넌트 + `UnitStats` / `UnitMover` / `UnitVisual` 분리 완료

### Exploration

- `Box`, `Portal`, `Trap` 등 인터랙티블 기믹 프리팹
- `InteractionHintUI` 등 월드 UI 프리팹
- 맵 프리팹 (`Map_Stage1_01` 등) — Addressables로 비동기 로드

### Common (공통 시스템)

전역 싱글톤 UI 프리팹:

- `PopupManager` — 팝업 스택 관리
- `FloatingTextManager` — 부유 텍스트 풀링
- `SceneTransitionManager` — 씬 전환 (DontDestroyOnLoad)

---

## 4. 데이터 파이프라인

### 4-1. ScriptableObject 데이터 계층

```
Assets/03_Data/
├── Definitions/    UnitData, ItemData, SkillAsset (에셋 정의 원천)
├── Unit/           캐릭터별 UnitData 에셋 (161개+)
├── Item/           아이템 ItemData 에셋 (50개+)
├── Skills/         스킬 에셋 SO
├── StageDB/        StageNormalMapData, WaveSet, StageDatabase (16개+)
├── Interactions/   GatherableDataSO, InteractionOutcomeSO 등 (13개+)
├── VisualDB/       UnitStateVisualDB, StatusDescriptionDB 등 (8개+)
├── Databases/      StateStatModifierDB, TrainingDB 등
└── Trap/           TrapBehavior 관련 데이터
```

### 4-2. VisualDB — 로직/리소스 분리

`Assets/03_Data/VisualDB`에 시각 데이터 매핑 테이블을 별도 관리.
코드 로직(`Data`)과 그래픽 리소스(`Art`)의 결합을 끊어 아티스트·기획자·개발자 간 의존성을 낮춘다.

### 4-3. 에디터 자동화 도구 (`Assets/Editor/`)

| 도구                          | 역할                                                  |
| ----------------------------- | ----------------------------------------------------- |
| `UniversalDataImporter`       | 구글 시트 CSV → SO 자동 변환 (스킬/패시브/특성)       |
| `GatherableDataImporter`      | 채집 오브젝트 CSV → `GatherableDataSO` 자동 생성/갱신 |
| `MapDataAutoSetupTool`        | 맵 데이터 일괄 자동 설정 에디터 윈도우                |
| `MapIDBaker`                  | 맵 프리팹에 고유 PersistID 자동 부여                  |
| `AddressablesBuilder`         | 메뉴에서 Addressables 빌드 트리거                     |
| `MissingScriptFinder`         | 씬/프리팹 누락 스크립트 탐색                          |
| `ParametricDamageSkillEditor` | 스킬 에셋 커스텀 인스펙터                             |

---

## 5. 인코딩 및 줄바꿈 관리

`.editorconfig` 및 `.gitattributes`에 의해 **자동** 적용된다.

| 대상     | 인코딩           | 줄바꿈 |
| -------- | ---------------- | ------ |
| `*.cs`   | UTF-8 (BOM 없음) | LF     |
| `*.md`   | UTF-8 (BOM 없음) | LF     |
| `*.json` | UTF-8 (BOM 없음) | LF     |

> ✅ Git 클론 후 `git config core.autocrlf false` 설정 필수 (Windows 환경).
> 자세한 내용은 `DevSetup_Guide.md` §2 참고.

---

## 6. 테스트 격리 전략 (`07_Test`)

`07_Test/` 폴더는 `Lemegeton.Test.asmdef`가 적용되어 있어:

- 릴리즈 빌드에서 **자동 제외**
- 에디터 전용 테스트 씬 / 스크립트 / 임시 에셋 보관

포함된 테스트 씬:

- `FogTest.unity` — 안개 시스템 테스트
- `Ui_Test.unity` — UI 레이아웃 테스트
- `Test.unity` — 일반 기능 테스트

---

## 7. 현재 상태 및 보완 사항

### 완료된 항목

- [x] `Resources/` 폴더 완전 비움 → Addressables 100% 전환
- [x] `ResourceTracker`로 Addressables 핸들 누수 방지
- [x] `07_Test/` `Lemegeton.Test.asmdef` 적용 → 빌드 격리
- [x] 인코딩/줄바꿈 `.editorconfig` + `.gitattributes` 적용
- [x] 에디터 자동화 도구 6종 구축

### 미완성/보완 필요 항목

- 🔴 **`05_Audio/` 비어있음** — AudioManager 미구현, 사운드 시스템 전무
  - 구현 시 `BGM/`, `SFX/` 폴더링 + Addressables Group 계획 필요
- 🟡 **`02_Prefabs/UI/` 폴더 미신설** — 전역 UI(`Common`)와 씬 UI가 혼재
  - `02_Prefabs/UI/` 신설 후 UI 관련 프리팹 일원화 권장
- 🔵 **단위 테스트 전무** — `Unity Test Framework 1.1.33` 설치됨, 활용 필요

---

_Asset Management & Data Pipeline Report — Lemegeton Project Documentation — 2026-03-05_
