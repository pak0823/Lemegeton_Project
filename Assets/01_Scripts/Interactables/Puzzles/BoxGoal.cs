using UnityEngine;
using UnityEngine.Tilemaps;

public class BoxGoal : MonoBehaviour
{
    public Sprite inactiveSprite;
    public Sprite activeSprite;

    private SpriteRenderer spriteRenderer;
    public Vector3Int Cell { get; private set; }
    public bool IsActive { get; private set; }

    public void Init(Tilemap floor)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Cell = floor.WorldToCell(transform.position);
        SetActive(false);
    }

    public void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.sprite = on ? activeSprite : inactiveSprite;

        Debug.Log($"[BoxGoal] 상태 변경: {(on ? "활성화" : "비활성화")} at {Cell}");
    }
}