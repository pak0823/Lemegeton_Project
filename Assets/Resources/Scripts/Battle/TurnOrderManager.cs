using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnOrderManager : MonoBehaviour
{
    #region Variables
    readonly System.Random rng = new();
    readonly List<BattleUnit> order = new();
    int index = 0;
    #endregion

    #region Build / Current
    public void BuildOrder(IEnumerable<BattleUnit> units)
    {
        order.Clear();

        // 죽은 유닛 제외 + AGI 정렬(동률은 소수점 난수로 타이브레이크)
        order.AddRange(units
            .Where(u => u != null && !u.IsDead)
            .OrderByDescending(u => u.AGI + (float)rng.NextDouble() * 0.01f));

        index = 0;
    }

    public BattleUnit Current
    {
        get
        {
            if (order.Count == 0) return null;

            // 현재 인덱스가 죽었거나 null이면 즉시 정리
            int safety = order.Count;
            while (safety-- > 0 && (order[index] == null || order[index].IsDead))
            {
                order.RemoveAt(index);
                if (order.Count == 0) return null;
                if (index >= order.Count) index = 0;
            }

            return order[index];
        }
    }
    #endregion

    #region Turn Progression
    // 한 행동 후 다음 턴으로 진행
    public void Advance()
    {
        if (order.Count == 0) return;

        int safety = order.Count;
        do
        {
            index = (index + 1) % order.Count;
        } while (safety-- > 0 && (order[index] == null || order[index].IsDead));
    }
    #endregion

    #region Unit Removal
    // 죽은 유닛 제거(현재 인덱스 보정)
    public void Remove(BattleUnit u)
    {
        int i = order.IndexOf(u);
        if (i < 0) return;

        if (i <= index && index > 0) index--;
        order.RemoveAt(i);
        if (index >= order.Count) index = 0;
    }
    #endregion

    #region Utilities
    public IReadOnlyList<BattleUnit> Snapshot() => order;
    #endregion
}
