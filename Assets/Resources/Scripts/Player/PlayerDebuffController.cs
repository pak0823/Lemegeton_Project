using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDebuffController : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private float baseSpeed;
    private List<float> activeSlows = new List<float>();

    public float minSpeed = 0.5f;   //플레이어 최소 속도 제한
    int slowCount = 0;
    private bool isStunned = false;
    public bool IsStunned => isStunned; // 외부에서는 읽기만 가능

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
            baseSpeed = playerMovement.defaultMoveSpeed;
    }

    public void ApplyDebuff(DebuffData data)
    {
        switch (data.debuffType)
        {
            case DebuffType.Slow:
                StartCoroutine(HandleSlow(data));
                break;
            //case DebuffType.Poison:
            //    break;
        }
    }

    private IEnumerator HandleSlow(DebuffData data) // Slow 함정 기능
    {
        if (isStunned) yield break; // 스턴 중이면 Slow 추가 적용 안 함

        // 등록
        activeSlows.Add(data.magnitude);
        RecalculateSpeed();
        slowCount++;

        // 3중첩 체크
        if (slowCount >= 3)
        {
            slowCount = 0; // 카운트 초기화
            yield return StartCoroutine(ApplyStun(3f));
            yield break; // 스턴 후 slow는 더 이상 적용하지 않음
        }
        Debug.Log("현재 적용중인 디버프 수: " + slowCount);
        // 지속 시간 대기
        yield return new WaitForSeconds(data.duration);

        // 제거
        if (activeSlows.Contains(data.magnitude))
            activeSlows.Remove(data.magnitude);

        slowCount = Mathf.Max(0, slowCount - 1); // 중첩 카운트 감소 + 음수 방지
        RecalculateSpeed();
        Debug.Log("디버프가 끝난 후 남은 디버프 수: " + slowCount);
    }
    private void RecalculateSpeed()
    {
        if (playerMovement == null) return;

        float speed = playerMovement.activeMoveSpeed;

        foreach (var slow in activeSlows)
        {
            speed *= slow; // slow는 0.5f 이런 값
        }

        // 최소 속도 제한
        speed = Mathf.Max(0.5f, speed);

        playerMovement.defaultMoveSpeed = speed;
    }
    private IEnumerator ApplyStun(float stunDuration)
    {
        isStunned = true;

        // 기존 slow 효과 전부 제거
        activeSlows.Clear();
        slowCount = 0;
        RecalculateSpeed();

        // 이동 완전 차단
        if (playerMovement != null)
        {
            playerMovement.defaultMoveSpeed = 0f;

            // Rigidbody 속도 제거
            Rigidbody2D rb = playerMovement.GetComponent<Rigidbody2D>();

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (playerMovement != null)
            {
                playerMovement.ClearPath();
            }
        }

        yield return new WaitForSeconds(stunDuration);

        // 스턴 해제 시 속도 복원
        RecalculateSpeed();
        isStunned = false;
    }
}
