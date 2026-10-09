using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp106;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using PlayerRoles;
using RGM.API.Features;
using RGM.Modes.Abilities.Synergy;

namespace RGM.Modes.Abilities.Legend;

[Ability("리인카네이션", """
                   사망 판정을 받을 시, 60초간 모든 공격에 무적이 되며, 능력치가 상승합니다.
                   단, 일정 횟수 이상 적 타격에 실패할 시 사망합니다.
                   """, AbilityCategory.Legend, AbilityType.LEGEND_REINCARNATION)]
public class Reincarnation : Ability, IDeathPreventionAbility
{
    private const float ContractDuration = 60f;
    private const float CooldownDuration = 120f;
    private const int RequiredHits = 70;
    private const int RequiredKills = 5;
    private const int MovementBoostIntensity = 40;
    
    private static bool _isDetonatingState;

    // 일반·영웅 사망 방어가 모두 불가능할 때만 소모한다.
    public int DeathPreventionPriority => 300;

    private bool _isContractActive;
    private bool _isCoolingDown;
    private int _hitCount;
    private int _killCount;
    private int _contractVersion;

    public override void OnEnabled()
    {
        Exiled.Events.Handlers.Player.Dying += OnDying;
        Exiled.Events.Handlers.Player.Hurting += OnHurting;
        Exiled.Events.Handlers.Player.Hurt += OnHurt;
        Exiled.Events.Handlers.Player.Died += OnDied;
        Exiled.Events.Handlers.Scp106.Attacking += OnScp106Attacking;
        Exiled.Events.Handlers.Warhead.Detonating += OnDetonating;
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.Dying -= OnDying;
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;
        Exiled.Events.Handlers.Player.Hurt -= OnHurt;
        Exiled.Events.Handlers.Player.Died -= OnDied;
        Exiled.Events.Handlers.Scp106.Attacking -= OnScp106Attacking;
        Exiled.Events.Handlers.Warhead.Detonating -= OnDetonating;

        _contractVersion++;

        if (_isContractActive)
            Owner.RemoveEffect(EffectType.MovementBoost, MovementBoostIntensity);

        _isContractActive = false;
        _isCoolingDown = false;
        _hitCount = 0;
        _killCount = 0;
    }

    private void OnDetonating(DetonatingEventArgs _)
    {
        if (_isDetonatingState)
            return;

        _isDetonatingState = true;
        Timing.CallDelayed(Timing.WaitForOneFrame, () => _isDetonatingState = false);
    }

    private void OnDying(DyingEventArgs ev)
    {
        if (ev.Player != Owner || IsContractExemptDamage(ev.DamageHandler.Type) ||
            WeakPointAttack.ShouldIgnoreDefenses(ev.Attacker))
            return;

        if (_isContractActive)
        {
            ev.IsAllowed = false;
            return;
        }

        if (DeathPreventionResolver.IsSelected(this, ev.Attacker, ev.DamageHandler.Type) && TryStartContract())
            ev.IsAllowed = false;
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        if (WeakPointAttack.ShouldIgnoreDefenses(ev.Attacker))
        {
            if (_isContractActive && ev.Attacker == Owner)
                ev.DamageHandler.Damage *= 1.35f;
            return;
        }

        if (ev.Player == Owner &&
            !_isContractActive &&
            !IsContractExemptDamage(ev.DamageHandler.Type) &&
            IsLethalDamage(ev) &&
            DeathPreventionResolver.IsSelected(this, ev.Attacker, ev.DamageHandler.Type) &&
            TryStartContract())
        {
            ev.IsAllowed = false;
            ev.DamageHandler.Damage = 0f;
            return;
        }

        if (_isContractActive &&
            ev.Player == Owner &&
            (!IsContractExemptDamage(ev.DamageHandler.Type) ||
             ev.DamageHandler.Type == DamageType.PocketDimension && !ev.IsInstantKill))
            ev.IsAllowed = false;

        if (_isContractActive && ev.Attacker == Owner)
            ev.DamageHandler.Damage *= 1.35f;
    }

    public bool CanPreventDeath(Player attacker, DamageType damageType)
    {
        if (IsContractExemptDamage(damageType) || WeakPointAttack.ShouldIgnoreDefenses(attacker))
            return false;

        if (_isContractActive)
            return true;

        return !_isCoolingDown && !DeathPreventionResolver.IsLifeUsed(Owner);
    }

    private void OnHurt(HurtEventArgs ev)
    {
        if (!_isContractActive ||
            ev.Attacker == null ||
            ev.Attacker != Owner ||
            ev.DamageHandler.Damage <= 0 ||
            !HitboxIdentity.IsEnemy(Owner.ReferenceHub, ev.Player.ReferenceHub))
            return;

        _hitCount++;

        if (_hitCount >= RequiredHits)
            EndContract(success: true);
    }

    private void OnDied(DiedEventArgs ev)
    {
        if (!_isContractActive ||
            ev.Attacker != Owner ||
            !HitboxIdentity.IsEnemy(RoleExtensions.GetTeam(Owner.Role.Type), RoleExtensions.GetTeam(ev.TargetOldRole)))
            return;

        _killCount++;

        if (_killCount >= RequiredKills)
            EndContract(success: true);
    }

    private void OnScp106Attacking(AttackingEventArgs ev)
    {
        if (ev.Target != Owner || !_isContractActive || WeakPointAttack.ShouldIgnoreDefenses(ev.Player))
            return;

        ev.IsAllowed = false;
    }

    private bool TryStartContract()
    {
        if (_isCoolingDown)
            return false;

        if (!ABattle.Instance.IsLifeUsed.TryGetValue(Owner, out bool isLifeUsed))
            ABattle.Instance.IsLifeUsed[Owner] = false;
        else if (isLifeUsed)
            return false;

        ABattle.Instance.IsLifeUsed[Owner] = true;
        Timing.CallDelayed(Timing.WaitForOneFrame, () =>
        {
            if (ABattle.Instance.IsLifeUsed.ContainsKey(Owner))
                ABattle.Instance.IsLifeUsed[Owner] = false;
        });

        StartContract();
        return true;
    }

    private bool IsLethalDamage(HurtingEventArgs ev)
    {
        float damage = ev.DamageHandler.Damage;
        if (damage <= 0f && !ev.IsInstantKill)
            return false;

        float totalHealth = Owner.Health + Owner.ArtificialHealth + Owner.HumeShield;
        return ev.IsInstantKill || damage >= totalHealth;
    }

    private void StartContract()
    {
        _isContractActive = true;
        _hitCount = 0;
        _killCount = 0;

        foreach (var effectType in Owner.ActiveEffects
                     .Select(effect => effect.GetEffectType())
                     .Where(effectType => !EffectManager.IsKeptBuff(effectType))
                     .ToList())
        {
            Owner.DisableEffect(effectType);
        }

        Owner.AddEffect(EffectType.MovementBoost, MovementBoostIntensity, ContractDuration);
        Owner.AddHint("리인카네이션",
            $"영혼 계약이 시작되었습니다. {ContractDuration:0}초 내에 적을 {RequiredHits}회 타격하거나 {RequiredKills}회 처치하세요.");

        int contractVersion = ++_contractVersion;
        Timing.CallDelayed(ContractDuration, () =>
        {
            if (_isContractActive && _contractVersion == contractVersion)
                EndContract(success: false);
        });
    }

    private void EndContract(bool success)
    {
        if (!_isContractActive)
            return;

        _isContractActive = false;
        _contractVersion++;
        Owner.RemoveEffect(EffectType.MovementBoost, MovementBoostIntensity);

        if (!success)
        {
            _isCoolingDown = true;
            Owner.RemoveAllAbilities();
            Owner.Kill("영혼 계약에 실패하여 산화되었습니다.");
            return;
        }

        Owner.Health = Owner.MaxHealth;
        _isCoolingDown = true;
        Owner.AddHint("리인카네이션",
            $"영혼 계약을 달성했습니다. {CooldownDuration:0}초 후 다시 사용할 수 있습니다.");

        Timing.CallDelayed(CooldownDuration, () => _isCoolingDown = false);
    }

    private static bool IsContractExemptDamage(DamageType damageType)
    {
        if (_isDetonatingState ||
            damageType is DamageType.Warhead or DamageType.PocketDimension or DamageType.Crushed or DamageType.Falldown)
            return true;

        return false;
    }
}