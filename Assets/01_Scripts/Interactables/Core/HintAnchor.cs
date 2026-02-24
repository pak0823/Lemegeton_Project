// HintAnchor.cs
using UnityEngine;

public class HintAnchor : MonoBehaviour
{
    [Header("UI Root Offset (per object)")]
    [Tooltip("기본값은 오브젝트 중심 좌표, 이 값을 조절하여 머리 위 등에 UI 전체 묶음을 띄웁니다.")]
    public Vector2 uiOffset = new Vector2(90f, 35f);
}
