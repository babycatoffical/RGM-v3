using Exiled.API.Enums;
using Exiled.Events.EventArgs.Scp1509;
using MEC;
using RGM.API.Features;

namespace RGM.EventArgs
{
    public static class Scp1509Events
    {
        public static void OnResurrecting(ResurrectingEventArgs ev)
        {
            // SCP-1509에 의해 부활한 대상은 1509Resurrected 이펙트를 적용받지 않게 함.
            Timing.CallDelayed(0.5f, () =>
            {
                ev.Player.ClearEffect();
                ev.Player.EnableEffect(EffectType.FogControl, 1);
            });
        }
    }
}
