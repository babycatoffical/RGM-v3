using Exiled.API.Enums;
using Exiled.Events.EventArgs.Scp106;
using RGM.API.Features;

namespace RGM.Modes.Abilities.Unique.Scp106.Epic;

[Ability("도움딛기", 
    "스토킹에서 나온 후, 5초간 이동 속도가 추가로 30%p 증가합니다.", 
    AbilityCategory.Epic, AbilityType.EPIC_SCP106_STEPPING, RoleAbility.Scp106)]

public class Stepping : Ability
{
    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Scp106.ExitStalking += OnExitStalking;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Scp106.ExitStalking -= OnExitStalking;
    }

    private void OnExitStalking(ExitStalkingEventArgs ev)
    {
        if (ev.Player == null || ev.Player != Owner) return;
        Owner.AddEffect(EffectType.MovementBoost, 30, 5);
    }
}