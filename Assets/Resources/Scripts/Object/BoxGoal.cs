using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxGoal : MonoBehaviour
{
    public Sprite inactiveSprite;
    public Sprite activeSprite;
    private SpriteRenderer spriterenderer;
    public bool IsActive { get; private set; }

    private void Awake()
    {
        spriterenderer = GetComponent<SpriteRenderer>();
        spriterenderer.sprite = inactiveSprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<PuzzleBox>(out var box))
        {
            IsActive = true;
            spriterenderer.sprite = activeSprite;
            Shared.PuzzleManager.NotifyGoalChanged();
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<PuzzleBox>(out var box))
        {
            IsActive = false;
            spriterenderer.sprite = inactiveSprite;
            Shared.PuzzleManager.NotifyGoalChanged();
        }
    }

}
