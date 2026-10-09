using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using RGM.API.DataBases;
using RGM.API.Features;

namespace RGM.Modes.Abilities.Normal;

[Ability("보험", "사망 판정을 받을 경우 1번 버텨냅니다.", AbilityCategory.Normal, AbilityType.NORMAL_INSURANCE)]
public class Insurance : Ability, IDeathPreventionAbility
{
    private static bool _isDetonatingState;

    // 낮은 등급의 사망 방어부터 소모한다.
    public int DeathPreventionPriority => 100;

    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Dying += OnDying;
        Exiled.Events.Handlers.Warhead.Detonating += OnDetonating;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Dying -= OnDying;
        Exiled.Events.Handlers.Warhead.Detonating -= OnDetonating;
    }

    private void OnDying(DyingEventArgs ev)
    {
        if (ev.Player != Owner ||
            !DeathPreventionResolver.IsSelected(this, ev.Attacker, ev.DamageHandler.Type))
            return;

        ev.IsAllowed = false;
        if (ev.Player.Health <= 0) ev.Player.Health = 0.1f;
        ev.Player.RemoveAbility(this);
        OnDisabled();

        Owner.AddAbility(AbilityType.DUMMY_EXPIREDINSURANCE);
        Owner.AddHint("보험", $"사망 판정을 받았지만 <color={ABattle.RatingColor["일반"]}>보험</color>으로 인해 1번 버텨냅니다.");

        ABattle.Instance.IsLifeUsed[Owner] = true;
        Timing.CallDelayed(Timing.WaitForOneFrame, () => ABattle.Instance.IsLifeUsed[Owner] = false);
    }

    public bool CanPreventDeath(Exiled.API.Features.Player attacker, Exiled.API.Enums.DamageType damageType)
    {
        return !DeathPreventionResolver.IsLifeUsed(Owner) &&
               !Datas.BlockDamageTypes.Contains(damageType) &&
               !_isDetonatingState;
    }

    private static void OnDetonating(DetonatingEventArgs e)
    {
        if (_isDetonatingState) return;
        
        _isDetonatingState = true;
        Timing.CallDelayed(Timing.WaitForOneFrame, () => _isDetonatingState = false);
    } 
}
