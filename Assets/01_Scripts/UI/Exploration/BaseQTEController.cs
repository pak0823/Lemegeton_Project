using System;
using UnityEngine;

// 모든 QTE 컨트롤러는 이 클래스를 상속받아야 함.
// 즉, 나중에 다른 유형 QTE 만들 때도 이것만 상속받으면 매니저 코드 수정 필요 없음

// 결과 상태 정의
public enum QTEResult
{
    Fail,       // 실패 (-1)
    Success,    // 일반 성공 (+1)
    Perfect     // 대성공 (+2)
}

public abstract class BaseQTEController : MonoBehaviour
{
    // 내용은 자식들이 알아서 구현 
    public abstract void StartQTE(Action<QTEResult> onResult);

    // 공통적으로 초기화나 종료 로직이 필요하면 여기에 가상 함수(virtual) 추가 가능
    public virtual void Init() { }
}