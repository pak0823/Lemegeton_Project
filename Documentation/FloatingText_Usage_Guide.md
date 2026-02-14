# Floating Text 시스템 사용 가이드

새롭게 리팩토링된 Floating Text 시스템의 사용법입니다.

## 1. 스타일 설정 (Inspector)

`FloatingTextManager` 프리팹 (혹은 씬 내 인스턴스)의 인스펙터에서 **Style Configs** 리스트를 열어 스타일을 정의합니다.

1.  **Style**: `FloatingTextStyle` Enum 값 선택 (Damage, Critical, Heal, VigorLoss 등)
2.  **Color**: 해당 스타일의 텍스트 색상 지정 (예: Damage는 빨강, Heal은 초록)
3.  **Scale Multiplier**: 텍스트 크기 배율 (기본 1.0, 강조 시 1.5 등)
4.  **Move Speed Multiplier**: 텍스트가 떠오르는 속도 (기본 1.0)
5.  **Scale Curve**: (선택) 등장 시 '펑' 하는 효과 등을 원하면 Animation Curve 설정

## 2. 스크립트에서 호출

기존처럼 문자열에 `<color>` 태그를 넣지 않고, **Enum**을 함께 전달합니다.

### 기본 사용법

```csharp
// 1. 데미지 (Damage 스타일)
FloatingTextManager.Instance.Spawn(position, "100", FloatingTextStyle.Damage);

// 2. 회복 (Heal 스타일 - 초록색 등 설정 필요)
FloatingTextManager.Instance.Spawn(position, "50", FloatingTextStyle.Heal);

// 3. 치명타 (Critical 스타일 - 큼직하게 설정 필요)
FloatingTextManager.Instance.Spawn(position, "CRITICAL!", FloatingTextStyle.Critical);

// 4. 활기 소모 (VigorLoss 스타일)
FloatingTextManager.Instance.Spawn(position, "-5", FloatingTextStyle.VigorLoss);
```

### 주의사항

- 스타일 설정이 없는 `FloatingTextStyle`로 호출할 경우, **기본값 (흰색, 크기 1.0)**으로 출력됩니다.
- 반드시 인스펙터에서 사용하려는 스타일에 대한 Config를 추가해주세요.

## 3. 새로운 스타일 추가

새로운 텍스트 타입(예: 경험치 획득)이 필요하다면:

1.  `FloatingTextDef.cs` 파일의 `FloatingTextStyle` Enum에 새 항목 추가 (`ExpGain`).
2.  유니티 에디터 -> `FloatingTextManager` 인스펙터 -> `Style Configs`에 새 항목 추가 및 설정.
3.  스크립트에서 `FloatingTextManager.Instance.Spawn(..., FloatingTextStyle.ExpGain)` 호출.
