using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace RGM.Modes;

/// <summary>
/// 치명 피해를 막는 능력이 여러 개 있을 때의 명시적인 선택 규칙입니다.
/// 값이 작을수록 먼저 소모됩니다.
/// </summary>
public interface IDeathPreventionAbility
{
    int DeathPreventionPriority { get; }

    /// <summary>
    /// 이 피해에 대해 실제로 발동할 수 있는지 여부만 판정합니다.
    /// 이 메서드에서는 횟수 소모나 효과 적용 같은 상태 변경을 해서는 안 됩니다.
    /// </summary>
    bool CanPreventDeath(Player attacker, DamageType damageType);
}

public static class DeathPreventionResolver
{
    public static bool IsLifeUsed(Player player)
    {
        return ABattle.Instance.IsLifeUsed.TryGetValue(player, out bool isLifeUsed) && isLifeUsed;
    }

    public static bool IsSelected(Ability ability, Player attacker, DamageType damageType)
    {
        if (ability is not IDeathPreventionAbility preventionAbility)
            return false;

        IDeathPreventionAbility selected = ABattle.Instance
            .GetAbilities(ability.Owner)
            .OfType<IDeathPreventionAbility>()
            .Where(candidate => candidate.CanPreventDeath(attacker, damageType))
            .OrderBy(candidate => candidate.DeathPreventionPriority)
            .FirstOrDefault();

        return ReferenceEquals(selected, preventionAbility);
    }
}
