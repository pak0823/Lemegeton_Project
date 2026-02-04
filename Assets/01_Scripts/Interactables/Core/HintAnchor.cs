// HintAnchor.cs
using UnityEngine;

public class HintAnchor : MonoBehaviour
{
    [Header("Hint Offsets (per object)")]
    public Vector2 surveyOffset = new Vector2(60f, 0f);  // F용
    public Vector2 commOffset = new Vector2(0f, 80f);  // E용
    public Vector2 cancelOffset = new Vector2(60f, 0f);  // E용
}
