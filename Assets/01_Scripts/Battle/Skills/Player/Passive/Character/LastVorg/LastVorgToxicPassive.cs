using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skill/Player/Passive/LastVorg/Toxic")]
public class LastVorgToxicPassive : PassiveAsset
{
    public StatusId targetStatus = StatusId.Poisoning;

    [Header("Tile Change Settings")]

    [Tooltip("변경할 독 타일 에셋")]
    public TileBase poisonTileAsset;

    [Tooltip("타일 변경 지속 턴")]

    public int tileChangeDuration = 2;



    // 런타임 핸들러 저장소

    private Dictionary<BattleUnit, System.Action<SkillAsset>> _handlers = new Dictionary<BattleUnit, System.Action<SkillAsset>>();



    public override void OnAttach(BattleUnit owner, BattleManager battle)

    {

        if (owner == null) return;



        // 중독 피해 면역 설정 (저항력 0 = 피해 0)

        var sc = owner.GetComponent<StatusController>();

        if (sc != null)

        {

            // 저항력을 0으로 설정 -> StatusController.OnTurnStart에서 데미지 계산 시 0이 됨

            sc.SetResistance(targetStatus, 0f);

            Debug.Log($"[Passive:Toxic] {owner.name}의 {targetStatus} 저항력이 0이 되었습니다. (피해 면역)");

        }



        // 스킬 사용 감지 핸들러 등록

        System.Action<SkillAsset> onSkillUsed = (skill) =>

        {

            // 연구 기술(SelfStateSkill) 사용 시

            if (skill is SelfStateSkill)

            {

                if (battle != null && poisonTileAsset != null)

                {

                    // 현재 위치를 독 타일로 변경 요청

                    battle.Field.CreateStatusTileZone(

                        owner,

                        owner.CurrentMap,

                        owner.Cell,

                        tileChangeDuration,

                        poisonTileAsset,

                        StatusId.Poisoning, // 부여할 상태

                        1,                  // 스택

                        3                   // 상태 지속시간

                    );

                }

            }

        };



        if (!_handlers.ContainsKey(owner))

        {

            _handlers.Add(owner, onSkillUsed);

            owner.OnSkillUsed += onSkillUsed;

        }

    }



    public override void OnDetach(BattleUnit owner, BattleManager battle)

    {

        if (owner == null) return;



        // 저항력 복구 (1.0f)

        var sc = owner.GetComponent<StatusController>();

        if (sc != null)

        {

            sc.SetResistance(targetStatus, 1.0f);

        }



        // 핸들러 해제

        if (_handlers.TryGetValue(owner, out var handler))

        {

            owner.OnSkillUsed -= handler;

            _handlers.Remove(owner);

        }

    }


}
