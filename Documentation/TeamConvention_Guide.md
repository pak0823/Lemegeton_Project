# 팀 컨벤션 가이드 (Team Convention Guide)

**분류:** 인수인계 — 팀 개발 컨벤션
**작성일:** 2026-03-05
**대상:** 프로젝트에 참여하는 모든 개발자

---

## 목차

1. [Git 워크플로우](#1-git-워크플로우)
2. [커밋 메시지 규칙](#2-커밋-메시지-규칙)
3. [코드 스타일 규칙](#3-코드-스타일-규칙)
4. [네이밍 컨벤션](#4-네이밍-컨벤션)
5. [스크립트 구조 규칙](#5-스크립트-구조-규칙)
6. [Unity 작업 규칙](#6-unity-작업-규칙)
7. [문서화 규칙](#7-문서화-규칙)
8. [코드 리뷰 체크리스트](#8-코드-리뷰-체크리스트)

---

## 1. Git 워크플로우

### 브랜치 전략

```
main          — 안정 릴리즈 브랜치 (직접 커밋 금지)
dev           — 개발 통합 브랜치 (기능 완성 후 머지)
feature/[이름] — 기능 개발 브랜치 (dev에서 분기, 완성 후 PR)
fix/[이름]    — 버그 수정 브랜치
refactor/[이름] — 리팩토링 브랜치
```

### 브랜치 작업 순서

```bash
# 1. dev에서 최신 상태 동기화
git checkout dev
git pull origin dev

# 2. 기능 브랜치 생성
git checkout -b feature/new-skill-system

# 3. 작업 후 커밋
git add .
git commit -m "feat: [캐릭터명] [스킬명] 스킬 에셋 및 로직 추가"

# 4. 원격 푸시 후 PR 생성
git push origin feature/new-skill-system
```

### PR (Pull Request) 규칙

- PR 제목은 커밋 메시지 규칙 동일 적용
- 변경 사항 설명 필수 (무엇을 왜 바꿨는지)
- Unity 씬/프리팹 변경 시 스크린샷 첨부 권장
- 빌드 오류 없음 확인 후 PR 생성

---

## 2. 커밋 메시지 규칙

### 접두사(Prefix) 분류

| 접두사      | 사용 상황                       | 예시                                              |
| ----------- | ------------------------------- | ------------------------------------------------- |
| `feat:`     | 새로운 기능 추가                | `feat: 노을 이중공격 패시브 구현`                 |
| `fix:`      | 버그 수정                       | `fix: ATB 충전 중복 계산 버그 수정`               |
| `refactor:` | 코드 구조 개선 (기능 변경 없음) | `refactor: BattleUnit 스탯 계산 UnitStats로 분리` |
| `ui:`       | UI/디자인 변경                  | `ui: 캠프 스킬 슬롯 레이아웃 개선`                |
| `data:`     | 데이터 에셋 변경 (SO, CSV 등)   | `data: 기간트 스킬 파워 수치 밸런싱`              |
| `docs:`     | 문서 추가/수정                  | `docs: ContentCreation_Workflow.md 추가`          |
| `chore:`    | 빌드 설정, 패키지 변경 등       | `chore: Addressables 그룹 재구성`                 |
| `test:`     | 테스트 코드 추가/수정           | `test: BattleSkillProcessor 단위 테스트 추가`     |

### 커밋 메시지 형식

```
[접두사]: [무엇을 했는가] (50자 이내)

[선택: 상세 설명 — 왜 했는가, 어떻게 했는가]

[선택: 관련 이슈 번호]
```

**예시:**

```
feat: 라스트보르그 분노 패시브 스택 한도 UI 표시 추가

StatusController에서 분노 스택 변경 이벤트를 구독하여
UI 게이지에 실시간으로 반영되도록 구현함.

Fixes #42
```

---

## 3. 코드 스타일 규칙

### 인코딩 / 줄바꿈 (자동 적용)

`.editorconfig`에 의해 자동 적용됨:

- 인코딩: **UTF-8** (BOM 없음)
- 줄바꿈: **LF** (CRLF 금지)
- 끝 공백 제거: 자동
- 파일 끝 빈 줄: 1줄

### 비동기 코드 표준

```csharp
// ✅ 권장 — UniTask 기반
public async UniTask DoSomethingAsync(CancellationToken ct = default)
{
    try
    {
        await UniTask.Delay(1000, cancellationToken: ct);
    }
    catch (OperationCanceledException)
    {
        // 취소 처리
    }
}

// ✅ 이벤트 핸들러에서 — UniTaskVoid 사용
public async UniTaskVoid OnButtonClickedAsync()
{
    try { await SomeCoroutineAsync(); }
    catch (Exception e) { Debug.LogException(e); }
}

// ❌ 금지 — async void
public async void OnButtonClicked() { ... }
```

### 싱글톤 패턴

```csharp
// 씬 범위 싱글톤 (씬 파괴 시 함께 파괴됨)
public class MyManager : MonoBehaviour
{
    public static MyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}

// 영구 싱글톤 (씬 전환 후 생존)
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}
```

### 이벤트 구독 / 해제

```csharp
// ✅ 반드시 OnEnable/OnDisable 또는 Start/OnDestroy 쌍으로 관리
private void OnEnable()
{
    GameEventBus.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
}

private void OnDisable()
{
    GameEventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
}
```

### Null 체크

```csharp
// ✅ 권장
if (target == null) return;
var result = target?.GetComponent<BattleUnit>();

// ✅ Unity 오브젝트 — Unity의 == 연산자 사용 (is not null 금지)
if (target != null) { ... }  // 올바른 Unity Null Check
```

---

## 4. 네이밍 컨벤션

### C# 코드

| 대상                       | 규칙                  | 예시                                    |
| -------------------------- | --------------------- | --------------------------------------- |
| 클래스 / enum              | PascalCase            | `BattleManager`, `UnitStateId`          |
| 메서드                     | PascalCase            | `GetFinalSkillDamage()`, `FindPath()`   |
| 공개 프로퍼티              | PascalCase            | `CurrentHP`, `IsAlive`                  |
| 공개 필드 (SerializeField) | camelCase             | `[SerializeField] private int maxHP`    |
| 비공개 필드                | `_camelCase` (언더바) | `private int _currentHP`                |
| 지역 변수                  | camelCase             | `int damageAmount`                      |
| 상수                       | ALL_CAPS              | `const float MAX_ATB = 100f`            |
| 인터페이스                 | `I` 접두사            | `IInventory`, `IExplorationPersistable` |

### 파일 / 에셋 네이밍

| 대상                  | 규칙                            | 예시                              |
| --------------------- | ------------------------------- | --------------------------------- |
| C# 스크립트           | PascalCase                      | `BattleUnit.cs`                   |
| ScriptableObject 에셋 | PascalCase + 접미사             | `GigantSlashSkill.asset`          |
| UnitData 에셋         | `[이름]Data.asset`              | `GigantData.asset`                |
| 패시브 에셋           | `[캐릭터][패시브]Passive.asset` | `GigantCounterStackPassive.asset` |
| 맵 프리팹             | `Map_[스테이지]_[번호]`         | `Map_Stage1_01.prefab`            |
| 애니메이터            | `[캐릭터]Animator.controller`   | `GigantAnimator.controller`       |

### 스킬 ID 문자열 규칙

```
[캐릭터약어]_[스킬기능]
예: "gigant_slash", "noeul_bleed_shot", "luckysix_rapid_fire"
```

---

## 5. 스크립트 구조 규칙

### Region 구조 (대형 클래스)

```csharp
public class BattleUnit : MonoBehaviour
{
    #region 1. Core Data & Configuration
    /* ... */
    #endregion

    #region 2. Dependencies
    /* ... */
    #endregion

    #region 3. Runtime Status
    /* ... */
    #endregion

    #region 4. ATB System
    /* ... */
    #endregion

    #region 5. Visuals & Animation
    /* ... */
    #endregion

    #region 6. Internal Logic & Cache
    /* ... */
    #endregion

    #region 7. Events
    /* ... */
    #endregion
}
```

### 클래스 길이 기준

| 줄 수        | 조치                 |
| ------------ | -------------------- |
| ~300줄       | 일반 수준, 문제 없음 |
| 300~600줄    | Region으로 구분      |
| 600줄 이상   | 책임 분리 검토 필요  |
| 1,000줄 이상 | 반드시 컴포넌트 분리 |

### 주석 규칙

```csharp
// 한국어로 작성한다.
// 코드가 명확하면 주석 불필요. 복잡한 로직에만 달기.

/// <summary>
/// 최종 스킬 데미지를 계산하여 반환한다.
/// 기본 데미지에 전방 보너스, 상태이상 배율, 분노 보정을 순서대로 적용한다.
/// </summary>
public int GetFinalSkillDamage(BattleUnit caster, BattleUnit target, SkillAsset skill)

// 최적화 완료 표시
// [Optimization] Use registry instead of FindObjectsOfType

// 미완성 표시 — PR 전 반드시 해결할 것
// TODO: 실제 통찰(INS) 스탯 연동 구현 필요
```

---

## 6. Unity 작업 규칙

### 씬 / 프리팹 작업

- 씬 파일(`.unity`)은 가능하면 혼자 작업하여 머지 충돌 방지
- 프리팹 변경 시 **Apply All** 후 커밋 (Apply 누락으로 인한 인스턴스 불일치 방지)
- `07_Test/` 폴더 에셋은 실제 게임에 사용 금지

### Addressables 작업 규칙

- 에셋 이동/삭제 시 반드시 **Addressables Groups 창에서** 수행
- 새 에셋 추가 후 **반드시 빌드 실행** 후 커밋
- Address명 변경 시 해당 주소를 참조하는 코드 검색 후 함께 변경

### ScriptableObject 작업

- SO 에셋은 `.asset` 파일까지 반드시 Git에 포함 (`.gitignore` 확인)
- Unity 직렬화 특성상 SO 필드 추가/삭제 시 기존 에셋 데이터 손실 주의

### 테스트 씬 사용

```
07_Test/Scenes/FogTest.unity   — 안개 시스템 테스트
07_Test/Scenes/Ui_Test.unity   — UI 레이아웃 테스트
07_Test/Scenes/Test.unity      — 일반 기능 테스트
```

테스트 씬 실행은 에디터에서만 가능 (Lemegeton.Test.asmdef가 빌드 제외).

---

## 7. 문서화 규칙

### 문서 업데이트 시점

| 작업 내용                | 업데이트해야 할 문서                   |
| ------------------------ | -------------------------------------- |
| 새 시스템 추가           | 해당 파트 문서 (Part1~4) + Research.md |
| 폴더 구조 변경           | Research.md §3, INDEX.md               |
| 버그 수정 완료           | Research.md §16 (액션 플랜 완료 표시)  |
| 새 콘텐츠 추가 방법 변경 | ContentCreation_Workflow.md            |

### 문서 파일 위치

```
Documentation/
├── INDEX.md                     — 전체 문서 인덱스 (항상 최신 유지)
├── Research.md                  — 메인 분석 보고서 (최신 갱신일 기재)
├── Part1_BattleSystem_Detail.md
├── Part2_ExplorationSystem_Detail.md
├── Part3_Character_Skill_Inventory_Detail.md
├── Part4_UI_Infrastructure_Detail.md
├── DevSetup_Guide.md
├── ContentCreation_Workflow.md
└── TeamConvention_Guide.md      ← 이 파일
```

---

## 8. 코드 리뷰 체크리스트

PR을 생성하거나 리뷰할 때 아래를 확인한다.

### 작성자 셀프 체크

- [ ] 컴파일 오류 없음 (Unity Console 에러 0개)
- [ ] `async void` 사용 없음 → `async UniTaskVoid` 또는 `async UniTask`
- [ ] 이벤트 구독(`Subscribe`) 있으면 반드시 해제(`Unsubscribe`) 짝 존재
- [ ] `FindObjectsOfType` 미사용 → 레지스트리/캐시 사용
- [ ] Addressables 에셋 추가 시 빌드 포함
- [ ] `#if UNITY_EDITOR` 없이 개발용 플래그 코드 노출 없음
- [ ] 새 공개 메서드/클래스에 XML 주석 또는 한국어 주석 작성
- [ ] 1,000줄 이상 파일 없음 (불가피한 경우 이유 PR에 명시)

### 리뷰어 체크

- [ ] 싱글톤 추가 시 씬 범위와 영구 범위 구분이 적절한가
- [ ] SO 필드 추가/제거로 기존 에셋 데이터 손실 위험 없는가
- [ ] 씬/프리팹 변경으로 기존 설정이 날아간 것은 없는가
- [ ] 인코딩(UTF-8), 줄바꿈(LF) 일관성 유지되는가

---

_Team Convention Guide — Lemegeton Project Documentation — 2026-03-05_
