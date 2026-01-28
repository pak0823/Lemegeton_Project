using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Passives/No Eul/BleedCountAgility",
    fileName = "Passive_BleedCountAgility")]
public class BleedCountAgilityPassive : PassiveAsset
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
            owner.SetPassiveAgilityMultiplier(1f); // ¿ø»ó º¹±¸

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

        // ÃâÇ÷ ÁßÃ¸ÀÌ ÀÖ´Â Àû À¯´Ö ¼ö °è»ê
        foreach (var enemy in battle.GetLivingEnemiesOf(owner))
        {
            if (enemy == null) continue;
            var sc = enemy.GetComponent<StatusController>();
            if (sc == null) continue;

            if (sc.Has(StatusId.Bleeding))
                bleedCount++;
        }

        // ±âº» ¹ÎÃ¸ ¹è¼ö = (1.40 ^ ÃâÇ÷ Àû ¼ö)
        float multiplier = Mathf.Pow(1.40f, bleedCount);

        owner.SetPassiveAgilityMultiplier(multiplier);

        Debug.Log(
            $"[Passive:BleedCountAgility] {owner.name} ÃâÇ÷ Àû {bleedCount}¸í ¡æ " +
            $"¹è¼ö {multiplier:0.000}, baseAGI={owner.AGI:0.000}, effectiveAGI={owner.EffectiveAGI:0.000}"
        );
    }
}
