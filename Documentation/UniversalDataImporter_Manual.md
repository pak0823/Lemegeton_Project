# Universal Data Importer 사용 설명서

## 개요

**Universal Data Importer**는 구글 스프레드시트의 데이터를 유니티의 `SkillAsset`, `PassiveAsset`, `TraitAsset`으로 손쉽게 동기화해주는 도구입니다.

## 사전 준비

1. **Google Sheet CSV URL 확보**:
   - 구글 시트에서 `파일 > 공유 > 웹에 게시`를 선택하세요.
   - '전체 문서' 대신 특정 시트(탭)를 선택하고, 형식을 '쉼표로 구분된 값(.csv)'으로 선택하세요.
   - 생성된 링크를 복사해둡니다.

2. **스크립트 설정**:
   - `Assets/Editor/UniversalDataImporter.cs` 파일을 여세요.
   - 상단의 URL 상수에 복사한 링크를 붙여넣으세요.
     ```csharp
     private const string SKILL_SHEET_URL = "여기에_스킬_CSV_링크";
     private const string PASSIVE_SHEET_URL = "여기에_패시브_CSV_링크";
     private const string TRAIT_SHEET_URL = "여기에_TRAIT_CSV_링크";
     ```

## 사용 방법

1. 유니티 에디터 상단 메뉴에서 `Tools > Data` 항목을 클릭합니다.
2. 원하는 동기화 작업을 선택합니다:
   - **Sync Skills**: 스킬 데이터 동기화
   - **Sync Passives**: 패시브 데이터 동기화
   - **Sync Traits**: 특성(Trait) 데이터 동기화

## 데이터 규칙

- **ID 기반 매칭**:
  - 모든 시트의 **A열(첫 번째 열)**은 반드시 고유한 **ID**여야 합니다.
  - 기존 에셋에 `id` 값이 있다면 해당 에셋을 찾아 갱신합니다.
  - 없다면 새로운 에셋 파일(`Skill_{ID}.asset` 등)을 생성합니다.

## 주의 사항

- **SkillAsset 생성**:
  - 스킬은 추상 클래스이므로, 신규 생성 시 기본적으로 `ParametricDamageSkill` 타입으로 생성되도록 설정되어 있습니다. 다른 타입이 필요하다면 코드를 수정하거나, 미리 에셋을 만들어두고 ID를 일치시키세요.
- **백업 권장**:
  - 대량의 데이터를 덮어쓰기 전에 반드시 프로젝트를 백업하세요.
