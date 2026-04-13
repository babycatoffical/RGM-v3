using Exiled.API.Enums;
using RGM.API.Features;

namespace RGM.Modes.Abilities.Normal;

[Ability("토끼뜀", "점프력이 10% 증가합니다.", AbilityCategory.Common, AbilityType.NORMAL_RABBIT)]
public class Rabbit : Ability
{
    private byte _count;
    public override void OnEnabled()
    {
        Owner.AddEffect(EffectType.Lightweight, 10);
        _count += 1;
    }

    public override void OnDisabled()
    {
        Owner.RemoveEffect(EffectType.Lightweight, 10 * _count);  
    }
}