// DebuffData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "DebuffData", menuName = "Game/DebuffData")]
public class DebuffData : ScriptableObject
{
    public DebuffType debuffType;      // 디버프 종류
    public float duration;             // 지속 시간 (초)
    public float magnitude;            // 효과 강도 (예: 느려지는 비율)
    public GameObject effectPrefab;    // 디버프 시 재생할 VFX
}