using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using RGM.API.DataBases;
using RGM.API.Features;
using RGM.Modes.Sets.AddScp.Scps;
using SecretAPI.Extensions;

namespace RGM.Modes.Abilities.Unique.Human.Epic;

[Ability("마체테 클로", "처치한 대상자를 자신의 진영으로 즉시 변경시킵니다.", 
    AbilityCategory.Epic, AbilityType.EPIC_HUMAN_SCP1509HAND, RoleAbility.Human)]

public class Scp1509Hand : Ability
{
    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Died += OnDied;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Died -= OnDied;
    }

    private void OnDied(DiedEventArgs ev)
    {
        if (ev.Attacker == null || ev.Attacker != Owner || Datas.BlockDamageTypes.Contains(ev.DamageHandler.Type)) return;
        if (ev.DamageHandler.Type is DamageType.PocketDimension or DamageType.Scp106) return;
        if (ev.Player.IsEffectActive<Corroding>()) return;
            
        ev.Player.Role.Set(ev.Attacker.IsScp ? RoleTypeId.Scp0492 : ev.Attacker.Role.Type, RoleSpawnFlags.None);
    }
}
