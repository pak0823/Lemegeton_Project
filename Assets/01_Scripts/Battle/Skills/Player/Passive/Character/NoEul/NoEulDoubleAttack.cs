using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Passives/No Eul/Passive_1",
    fileName = "Passive_DoubleAttack")]
public class NoEulDoubleAttack : PassiveAsset
{
    // 애니메이션에서 공격을 2번 실행하게 설정해뒀음
    public override void OnAttach(BattleUnit _owner, BattleManager _battlemanager) { }

    public override void OnDetach(BattleUnit _owner, BattleManager _battlemanager){ }
}
