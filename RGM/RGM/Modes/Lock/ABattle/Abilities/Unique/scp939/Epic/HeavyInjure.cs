using Exiled.API.Enums;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;

namespace RGM.Modes.Abilities.Unique.Scp939.Epic;

[Ability("중상", "SCP-939의 기본 공격에 대상의 최대 HP 30%만큼 추가 데미지를 가합니다.", 
    AbilityCategory.Epic, AbilityType.LEGEND_HUMAN_EMP, RoleAbility.Human, isUnique:true)]

public class HeavyInjure : Ability
{
    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Hurting += OnHurting;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;
    }
    
    private void OnHurting(HurtingEventArgs ev)
    {
        if (Owner.Role is not Scp939Role scp939) return;
        if (ev.DamageHandler.Type != DamageType.Scp939) return;
        if (ev.Attacker.ReferenceHub == scp939.Owner.ReferenceHub)
            ev.DamageHandler.Damage += ev.Player.MaxHealth * 0.3f;
    }
}