using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Passives/No Eul/Passive_2",
    fileName = "Passive_BleedCountAgility")]
public class NoEulBleedCountAgilityPassive : PassiveAsset
{
    private BattleUnit owner;
    private BattleManager battle;

    public override void OnAttach(BattleUnit _owner, BattleManager _battlemanager)
    {
        owner = _owner;
        battle = _battlemanager;

        RecalculateMultiplier();

        BattleManager.OnAnyUnitTurnStarted += HandleTurnStarted;
        _battlemanager.OnUnitEndTurn += HandleTurnEnded;
    }

    public override void OnDetach(BattleUnit _owner, BattleManager _battlemanager)
    {
        if (battle != null)
        {
            BattleManager.OnAnyUnitTurnStarted -= HandleTurnStarted;
            _battlemanager.OnUnitEndTurn -= HandleTurnEnded;
        }

        if (owner != null)
            owner.SetPassiveAgilityMultiplier(1f); // 원상 복구

        owner = null;
        battle = null;
    }

    private void HandleTurnStarted(BattleUnit unit)
    {
        if (unit == owner)
            RecalculateMultiplier();
    }

    private void HandleTurnEnded(BattleUnit unit)
    {
        if (unit == owner)
            RecalculateMultiplier();
    }

    private void RecalculateMultiplier()
    {
        if (owner == null || battle == null)
            return;

        int bleedCount = 0;

        // 출혈 중첩이 있는 적 유닛 수 계산
        foreach (var enemy in battle.GetLivingEnemiesOf(owner))
        {
            if (enemy == null) continue;
            var sc = enemy.GetComponent<StatusController>();
            if (sc == null) continue;

            if (sc.Has(StatusId.Bleeding))
                bleedCount++;
        }

        // 기본 민첩 배수 = (1.40 ^ 출혈 적 수)
        float multiplier = Mathf.Pow(1.40f, bleedCount);

        float beforeAGI = owner.EffectiveAGI; // 변경 전 값 계산

        owner.SetPassiveAgilityMultiplier(multiplier);

        float afterAGI = owner.EffectiveAGI;  // 변경 후 값 재계산

        // 로그 출력 (값이 변했다면 출력)
        if (!Mathf.Approximately(beforeAGI, afterAGI))
        {
            Debug.Log($"[Passive] {owner.name} (Bleed targets: {bleedCount}) AGI Updated: {beforeAGI:F1} -> {afterAGI:F1}");
        }
    }
}
