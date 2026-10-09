using System.Linq;
using UnityEngine;

using static RGM.Variables.Variable;

namespace RGM.Modes.Abilities.Normal;

[Ability("진화", "몸의 크기가 8%p 작아집니다. (최대 80%p까지 적용)", AbilityCategory.Normal, AbilityType.NORMAL_EVOLUTION)]
public class Evolution : Ability
{
    private const float Scale = 0.08f;
    private readonly Vector3 _deadline = new(0.2f, 0.2f, 0.2f);

    public override void OnEnabled()
    {
        if (Owner.Scale.x - Scale < _deadline.x ||
            Owner.Scale.y - Scale < _deadline.y ||
            Owner.Scale.z - Scale < _deadline.z)
            return;
        if (EnabledModeList.Any(x => x.Data.Type == ModeType.GulliversTravels))
            return;

        Owner.Scale = new Vector3(Owner.Scale.x - Scale, Owner.Scale.y - Scale, Owner.Scale.z - Scale);
    }
}
