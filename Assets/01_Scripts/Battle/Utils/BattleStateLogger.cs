using UnityEngine;
using System.Text;

public class BattleStateLogger : MonoBehaviour
{
    private void Start()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnStateChanged += HandleStateChange;
            Debug.Log($"[BattleStateLogger] Start logging. Initial State: {BattleManager.Instance.state}");
        }
        else
        {
            Debug.LogWarning("[BattleStateLogger] BattleManager instance not found.");
        }
    }

    private void OnDestroy()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnStateChanged -= HandleStateChange;
        }
    }

    private void HandleStateChange(BattleState oldState, BattleState newState)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"[BattleState] <color=yellow>{oldState}</color> -> <color=green>{newState}</color>");
        
        if (BattleManager.Instance.ActingUnit != null)
        {
            sb.Append($" | Acting: {BattleManager.Instance.ActingUnit.name}");
        }

        // 간단한 스택 트레이스 (호출자 추적)
        // 너무 길어지지 않게 상위 3줄 정도만
        string stack = System.Environment.StackTrace;
        string[] lines = stack.Split('\n');
        
        // 0: Environment.StackTrace
        // 1: HandleStateChange
        // 2: BattleManager.set_state
        // 3: Call site (우리가 원하는 정보)
        
        if (lines.Length > 3)
        {
            sb.Append("\nCalled from:");
            for (int i = 3; i < Mathf.Min(lines.Length, 6); i++)
            {
               if (!lines[i].Contains("UnityEngine")) // 유니티 엔진 내부 호출은 생략 가능하면 생략
                   sb.Append($"\n   {lines[i].Trim()}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
