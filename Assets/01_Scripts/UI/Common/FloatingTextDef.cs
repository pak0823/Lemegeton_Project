using UnityEngine;

// 1. 스타일 종류 정의
public enum FloatingTextStyle
{
    Damage,     // 일반 데미지 (Red)
    Critical,   // 크리티컬 (Yellow/Orange, Big)
    Heal,       // 회복 (Green)
    Buff,       // 버프 (Blue)
    Debuff,     // 디버프 (Purple)
    VigorLoss,  // 활기 소모 (Gray/White)
    Miss,       // 회피/실패 (Gray)
    ExpGain     // 경험치 획득 (Cyan)
}

// 2. 스타일 설정 데이터 구조
[System.Serializable]
public struct FloatingTextConfig
{
    public FloatingTextStyle style;
    public Color color;
    public float scaleMultiplier;   // 기본 1.0f
    public AnimationCurve scaleCurve; // 크리티컬 같이 터지는 느낌 줄 때 사용
    public float moveSpeedMultiplier; // 올라가는 속도 조절
}
