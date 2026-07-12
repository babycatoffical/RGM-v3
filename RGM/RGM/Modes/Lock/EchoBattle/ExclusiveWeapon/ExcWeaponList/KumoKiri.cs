using Exiled.Events.EventArgs.Player;
using RGM.Modes;
using UnityEngine;

namespace RGM.Modes.ExclusiveWeapon;

/// <summary>
/// KumoKiri.
/// Passive: Attack 11%+(res*2%). On hit (1%*res) chance for 618.03 fixed damage.
/// </summary>
[ExclusiveWeapon(
    "쿠모키리",
    "공격력 11% + (공진 수치 * 2%) 증가. 적 타격 시 (1.4% * 공진 수치) 확률로 618.03 고정 피해.",
    ExclusiveWeaponType.KumoKiri)]
public class KumoKiri : ExcWeapon
{
    public override float AttackFlatMin => 4.0f;
    public override float AttackFlatMax => 50.0f;
    public override ExclusiveWeaponSecondaryStat SecondaryStat => ExclusiveWeaponSecondaryStat.CriticalChance;
    public override float SecondaryStatMin => 8.0f;
    public override float SecondaryStatMax => 36.0f;

    public override float PassiveAttackPercent => 11f + Resonance * 2f;

    const float FixedDamage = 618.03f;

    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Hurting += OnHurting;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;
    }

    void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker != Owner || Owner == null)
            return;

        if (ev.DamageHandler == null || ev.DamageHandler.Damage <= 0f)
            return;

        if (!HitboxIdentity.IsEnemy(ev.Attacker.ReferenceHub, ev.Player.ReferenceHub))
            return;

        if (EchoStats.AreAttackModifiersIgnored(Owner))
            return;

        float chance = 1.4f * Resonance;
        if (Random.Range(0f, 100f) >= chance)
            return;

        ev.DamageHandler.Damage += FixedDamage;
        EchoBattleCore.ShowNotification(
            Owner,
            $"<color=#cc88ff>KumoKiri</color> 고정 피해 +{FixedDamage:0.##}",
            1.2f);
    }
}
