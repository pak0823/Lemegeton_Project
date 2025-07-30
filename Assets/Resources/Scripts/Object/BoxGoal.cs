using UnityEngine;
using UnityEngine.Tilemaps;

public class BoxGoal : MonoBehaviour
{
    public Sprite inactiveSprite;
    public Sprite activeSprite;

    private SpriteRenderer spriterenderer;
    public Vector3Int Cell { get; private set; }
    public bool IsActive { get; private set; }

    public void Init(Tilemap floor)
    {
        spriterenderer = GetComponent<SpriteRenderer>();
        Cell = floor.WorldToCell(transform.position);
        SetActive(false);
    }
    public void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;
        spriterenderer.sprite = on ? activeSprite : inactiveSprite;
    }

}
