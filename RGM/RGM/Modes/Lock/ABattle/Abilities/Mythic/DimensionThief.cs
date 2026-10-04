using Exiled.Events.EventArgs.Player;
using RGM.API.Features;
using System.Linq;

namespace RGM.Modes.Abilities.Mythic;

[Ability("차원 강탈자", "처치한 자의 능력을 모조리 흡수합니다! (반사경 효과 미적용)",
    AbilityCategory.Mythic, AbilityType.MYTHIC_DIMENSIONTHIEF, isUnique: true)]
public class DimensionThief : Ability
{
    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Dying += OnDying;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Dying -= OnDying;
    }

    private void OnDying(DyingEventArgs ev)
    {
        if (ev.Attacker == null || ev.Attacker != Owner) return;

        if (!ev.Player.IsDead) return;
        _ = ABattle.Instance.AddAbilityAsync(ev.Attacker, [.. ABattle.Instance.PlayerAbilities[ev.Player]
            .Select(a => a.Data.AbilityType)], allowReflector: false);

        ev.Player.AddHint("차원 강탈자", "능력을 강탈당했습니다!");
    }
}
