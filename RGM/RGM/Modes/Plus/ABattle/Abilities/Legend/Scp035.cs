using RGM.Modes.Sets.AddScp.Scps;

namespace RGM.Modes.Abilities.Legend;

[Ability(
    "SCP-035",
    "빙의 가면, SCP-035로 변경됩니다.",
    AbilityCategory.Legend, 
    AbilityType.LEGEND_SCP035)]
public class ChangeScp035 : Ability
{
    public override void OnEnabled()
    {
        Scp035.Create(Owner);
    }

    public override void OnDisabled()
    {
    }
}
