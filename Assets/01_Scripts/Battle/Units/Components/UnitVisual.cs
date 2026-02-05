using UnityEngine;
using Cysharp.Threading.Tasks;

// SRP: Handles Animations and VFX
public class UnitVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    public void Initialize()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public async UniTask PlayTriggerAsync(string triggerName, float timeout = 1.0f)
    {
        if (!animator) return;
        
        animator.SetTrigger(triggerName);
        // Simplification: In real implementation, wait for animation event or state info
        await UniTask.Delay(System.TimeSpan.FromSeconds(timeout));
    }
}
