using Cysharp.Threading.Tasks;

public class SkillCommand : ICommand
{
    private readonly BattleUnit _user;
    private readonly BattleUnit _target;
    private readonly SkillAsset _skill;

    public SkillCommand(BattleUnit user, BattleUnit target, SkillAsset skill)
    {
        _user = user;
        _target = target;
        _skill = skill;
    }

    public async UniTask ExecuteAsync()
    {
        if (_user == null || _skill == null) return;

        // 1. Trigger Animation
        string trigger = _user.GetAnimTriggerForSkill(_skill);
        await _user.Visual.PlayTriggerAsync(trigger);

        // 2. Logic (Damage, etc.)
        // Ideally, this should wait for an animation event, but for now we simulate execution.
        // The actual damage application logic might still reside in BattleUnit or BattleManager temporarily.
        // We will invoke the events that trigger the damage calculation.
        
        _user.NotifySkillUsed(_skill);
        
        // If the skill has immediate effect logic, it goes here.
        // For now, we assume the Animation Event triggered by PlayTriggerAsync calls the actual damage methods in BattleUnit.
    }
}
