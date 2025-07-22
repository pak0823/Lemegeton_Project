using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDebuffController : MonoBehaviour
{
    private PlayerMovement mover;
    private float baseSpeed;
    private List<float> activeSlows = new List<float>();

    void Awake()
    {
        mover = GetComponent<PlayerMovement>();
        if (mover != null)
            baseSpeed = mover.moveSpeed;
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

    private IEnumerator HandleSlow(DebuffData data) //Slow 함정 기능
    {
        // 1) 등록
        activeSlows.Add(data.magnitude);
        RecalculateSpeed();

        // 2) 지속 시간 대기
        yield return new WaitForSeconds(data.duration);

        // 3) 제거
        activeSlows.Remove(data.magnitude);
        RecalculateSpeed();
    }
    private void RecalculateSpeed() //이동속도 감소 중첩 계산
    {
        float totalMultiplier = 1f;
        foreach (var mag in activeSlows)
            totalMultiplier *= (1f - mag);

        mover.moveSpeed = baseSpeed * totalMultiplier;
    }
}
