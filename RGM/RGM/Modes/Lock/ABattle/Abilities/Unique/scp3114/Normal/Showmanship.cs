using System.Collections.Generic;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using MEC;
using RGM.API.Features;

namespace RGM.Modes.Abilities.Unique.Scp3114.Normal;

//[Ability("쇼맨쉽", "춤추는 동안 지속적인 데미지를 받습니다. 대신 외부로부터 받는 데미지를 66% 줄입니다. <size=10>idea by 조용히게임함</size>", AbilityCategory.Common, AbilityType.NORMAL_SCP3114_SHOWMANSHIP, RoleAbility.Scp3114)]
public class Showmanship : Ability
{
    CoroutineHandle coroutine;

    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Hurting += OnHurting;

        coroutine = Timing.RunCoroutine(onStarted());
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;

        Timing.KillCoroutines(coroutine);
    }

    private IEnumerator<float> onStarted()
    {
        while (true)
        {
            Scp3114Role scp3114 = (Scp3114Role)Owner.Role;

            if (scp3114.Dance)
            {
                Owner.Hit(Owner, 1f);
            }

            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Player != Owner)
            return;

        Scp3114Role scp3114 = (Scp3114Role)ev.Player.Role;

        if (scp3114.Dance)
        {
            ev.DamageHandler.Damage /= 3;
        }
    }
}
