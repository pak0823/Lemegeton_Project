# 개발 환경 셋업 가이드 (DevSetup Guide)

**분류:** 인수인계 — 개발 환경 셋업
**작성일:** 2026-03-05
**대상:** 프로젝트에 새로 합류하는 개발자

---

## 목차

1. [필수 소프트웨어 설치](#1-필수-소프트웨어-설치)
2. [저장소 클론 및 初期 설정](#2-저장소-클론-및-초기-설정)
3. [Unity 프로젝트 열기](#3-unity-프로젝트-열기)
4. [패키지 의존성 확인](#4-패키지-의존성-확인)
5. [Addressables 빌드](#5-addressables-빌드)
6. [IDE 설정 (Rider / Visual Studio)](#6-ide-설정-rider--visual-studio)
7. [Unity MCP 연동 설정](#7-unity-mcp-연동-설정)
8. [첫 플레이 테스트](#8-첫-플레이-테스트)
9. [자주 발생하는 문제 및 해결법](#9-자주-발생하는-문제-및-해결법)

---

## 1. 필수 소프트웨어 설치

### Unity Hub + Unity Editor

| 항목             | 버전               | 비고                                       |
| ---------------- | ------------------ | ------------------------------------------ |
| **Unity Hub**    | 최신 버전          | [unityhub.io](https://unity.com/unity-hub) |
| **Unity Editor** | **2022.3.x (LTS)** | URP 14.0.12 지원 버전                      |

> ⚠️ **중요:** Unity 버전이 다르면 렌더 파이프라인(URP 14.0.12)과 패키지 버전 충돌이 발생할 수 있다. 반드시 2022.3.x LTS를 사용한다.

Unity Hub에서 버전 설치 시 아래 모듈을 반드시 포함:

- **Android Build Support** (Android 빌드 시)
- **Windows Build Support (IL2CPP)**
- **WebGL Build Support** (선택)

### IDE (둘 중 하나)

| IDE                    | 버전      | 추천 여부                                       |
| ---------------------- | --------- | ----------------------------------------------- |
| **JetBrains Rider**    | 최신 버전 | ⭐ 권장 — Unity 전용 기능, Roslyn Analyzer 지원 |
| **Visual Studio 2022** | 최신 버전 | 대안                                            |

### Git

- **Git** 최신 버전 ([git-scm.com](https://git-scm.com/))
- 선택 사항: **GitHub Desktop** 또는 **SourceTree** (GUI 클라이언트)

---

## 2. 저장소 클론 및 초기 설정

### 클론

```bash
git clone https://github.com/[owner]/Lemegeton_Project.git
cd Lemegeton_Project
```

### Git 인코딩 설정 (필수)

프로젝트는 `.gitattributes`로 LF를 강제하고 있다. Windows 환경에서 아래 설정이 되어 있는지 확인한다.

```bash
git config core.autocrlf false
git config core.eol lf
```

`.gitattributes` 내용 (이미 적용되어 있음):

```
* text=auto eol=lf
*.cs text eol=lf
*.md text eol=lf
*.json text eol=lf
```

### .editorconfig 설명

```ini
[*.cs]
charset = utf-8         # UTF-8 (BOM 없음)
end_of_line = lf        # LF 강제
trim_trailing_whitespace = true
insert_final_newline = true
```

IDE에서 `.editorconfig`를 자동 감지하여 적용된다. Rider는 자동 지원, VS는 EditorConfig 플러그인 설치 필요.

---

## 3. Unity 프로젝트 열기

1. **Unity Hub** 실행
2. **Add** → 클론한 `Lemegeton_Project` 폴더 선택
3. 올바른 Unity 버전(2022.3.x)이 선택되었는지 확인 후 **Open**
4. 첫 오픈 시 패키지 임포트 및 Library 빌드가 자동으로 진행됨 (5~15분 소요)

> ⚠️ **첫 오픈 주의사항:**
>
> - `Library/` 폴더는 `.gitignore`에 포함되어 있어 클론 시 없다. Unity가 자동으로 재생성한다.
> - Addressables 초기화 경고가 뜰 수 있다 → [5번 섹션](#5-addressables-빌드) 참고

---

## 4. 패키지 의존성 확인

`Packages/manifest.json`에 정의된 핵심 패키지:

| 패키지                                 | 버전/소스  | 역할                      |
| -------------------------------------- | ---------- | ------------------------- |
| `com.unity.render-pipelines.universal` | `14.0.12`  | URP 렌더 파이프라인       |
| `com.unity.addressables`               | `1.22.3`   | 에셋 비동기 로딩          |
| `com.unity.inputsystem`                | `1.14.2`   | New Input System          |
| `com.unity.textmeshpro`                | `3.0.7`    | TextMesh Pro UI 텍스트    |
| `com.unity.feature.2d`                 | `2.0.1`    | 2D 기능 팩 (Tilemap 포함) |
| `com.cysharp.unitask`                  | Git (최신) | 비동기 처리 라이브러리    |
| `com.coplaydev.unity-mcp`              | Git (main) | AI 에디터 연동 도구       |
| `com.unity.test-framework`             | `1.1.33`   | 단위 테스트 프레임워크    |
| `com.unity.timeline`                   | `1.7.7`    | 타임라인                  |

패키지 오류 발생 시:

1. **Window → Package Manager** 열기
2. 오류가 있는 패키지를 선택 후 **Remove** → 재설치
3. Git URL 패키지(`UniTask`, `unity-mcp`)는 인터넷 연결 필요

---

## 5. Addressables 빌드

스킬 에셋, 맵 프리팹, 아이템 아이콘 등을 Addressables로 관리한다. **플레이 전 반드시 빌드가 필요하다.**

### 빌드 방법 (2가지)

**방법 A — 커스텀 에디터 메뉴 (권장)**

```
Unity 메뉴 → Tools → Addressables → Build Addressables
```

`AddressablesBuilder.cs`가 자동으로 실행된다.

**방법 B — 기본 메뉴**

```
Unity 메뉴 → Window → Asset Management → Addressables → Groups
→ Build → New Build → Default Build Script
```

### 빌드 경로

빌드된 카탈로그는 `Assets/AddressableAssetsData/` 및 `ServerData/` (로컬 빌드 시)에 위치한다.

> ⚠️ **주의:** Addressables 빌드 없이 플레이하면 `InvalidKeyException` 또는 맵/에셋이 로드되지 않은 채로 실행된다.

---

## 6. IDE 설정 (Rider / Visual Studio)

### JetBrains Rider 권장 설정

1. Unity → **Edit → Preferences → External Tools → External Script Editor** → `Rider` 선택
2. Rider 내에서 **File → Settings → Editor → Code Style → C#** → `.editorconfig` 자동 적용 확인
3. **Unity Support** 플러그인 활성화 확인 (기본 포함)

### Visual Studio 2022 설정

1. Unity → **Edit → Preferences → External Tools** → `Visual Studio 2022` 선택
2. VS에서 **확장 → 확장 관리 → EditorConfig** 설치 확인
3. **도구 → 옵션 → 텍스트 편집기 → C# → 코드 스타일** 설정

### 어셈블리 참조 설정

IDE에서 `UniTask`, `Addressables` 등을 인식하지 못하면:

```
Unity → Edit → Preferences → External Tools → [Regenerate project files]
```

---

## 7. Unity MCP 연동 설정

`com.coplaydev.unity-mcp` 패키지가 설치되어 있어 AI 에디터 연동이 가능하다. 일반 개발 작업에는 필요하지 않으며 필요 시 CoplayDev 문서를 참고한다.

---

## 8. 첫 플레이 테스트

### 씬 열기 순서

1. **Title씬** 확인: `Assets/00_Scenes/TitleScene.unity`
2. Addressables 빌드 완료 확인
3. **Play Mode** 진입
4. **Exploration씬** 접속: `Assets/00_Scenes/ExplorationScene.unity`
5. **Battle씬** 접속: `Assets/00_Scenes/BattleScene.unity`

### 씬별 빠른 확인 포인트

| 씬               | 확인 사항                                       |
| ---------------- | ----------------------------------------------- |
| TitleScene       | 타이틀 버튼 클릭 → ExplorationScene 전환        |
| ExplorationScene | 타일 클릭 → 플레이어 이동, `Tab` → 캠프 UI 열림 |
| BattleScene      | 유닛 ATB 충전 → 스킬 선택 → 전투 진행           |

### 빌드 제외 확인 (`07_Test`)

`07_Test/` 폴더는 `Lemegeton.Test.asmdef`가 적용되어 있어 릴리즈 빌드에서 자동 제외된다. 에디터에서만 접근 가능하다.

---

## 9. 자주 발생하는 문제 및 해결법

### 문제 1: 맵이 로드되지 않거나 빈 씬으로 시작됨

- **원인:** Addressables 빌드가 안 되어 있음
- **해결:** `Tools → Addressables → Build Addressables` 실행

### 문제 2: 한글 주석이 깨져 보임

- **원인:** 파일 인코딩이 UTF-8이 아님
- **해결:** `.editorconfig`의 `charset = utf-8` 확인, Rider/VS에서 UTF-8 강제 저장

### 문제 3: 컴파일 오류 — UniTask 관련 네임스페이스 없음

- **원인:** Git URL 패키지 다운로드 실패
- **해결:** Package Manager에서 `Cysharp/UniTask` 제거 후 재추가, 인터넷 연결 확인

### 문제 4: `NullReferenceException` in MapManager on Start

- **원인:** StageDatabase SO가 인스펙터에 연결되지 않음
- **해결:** ExplorationScene의 `MapManager` 오브젝트 인스펙터에서 `StageDatabase` 필드 확인

### 문제 5: FogManager 관련 컴파일 오류

- **원인:** 씬 참조 오브젝트 누락 또는 순환 참조
- **해결:** `MissingScriptFinder` 에디터 도구 실행 (`Tools → Find Missing Scripts`)

### 문제 6: Library 폴더 관련 오류 (클론 직후)

- **원인:** Library가 `.gitignore`되어 없음
- **해결:** Unity Hub에서 프로젝트를 열면 자동으로 재생성됨 (5~15분)

---

_DevSetup Guide — Lemegeton Project Documentation — 2026-03-05_
