using System.Threading;
using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using RGM.API.Features;

namespace RGM.Modes.Abilities.Legend;

[Ability("도박사", "아이템을 버리면 새로운 아이템으로 변환합니다.",
    AbilityCategory.Legend, AbilityType.LEGEND_GAMBLER, isUnique: true)]
public class Gambler : Ability
{
    private Mutex _mutex;
    
    public override void OnEnabled()
    {
        if (Owner.IsScpRole() || Owner.Role.Type.ToString().Contains("Flamingo"))
            Owner.AddHint("도박", $"<size=20>[Space + ALT]ㅣ도박을 진행할 수 있습니다.</size>", 10);

        Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
        Exiled.Events.Handlers.Player.TogglingNoClip += OnTogglingNoClip;
        
        _mutex = new Mutex();
    }

    public override void OnDisabled()
    {
        Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
        Exiled.Events.Handlers.Player.TogglingNoClip -= OnTogglingNoClip;
        
        _mutex?.Dispose();
    }

    private void OnDroppingItem(DroppingItemEventArgs ev)
    {
        if (ev.Player != Owner ||
            ev.Player.IsScpRole() || 
            ev.Player.Role.Type.ToString().Contains("Flamingo") || 
            !PlayerManager.List.Contains(ev.Player) ||
            !_mutex.WaitOne(1000)) return;

        ev.Item.Destroy();
        Item currentItem = Owner.AddRandomItem();
        Owner.DropItem(currentItem);
        _mutex.ReleaseMutex();
    }

    private void OnTogglingNoClip(TogglingNoClipEventArgs ev)
    {
        if (!(Owner.IsScpRole() || 
              Owner.Role.Type.ToString().Contains("Flamingo")) ||
            !Owner.IsJumping ||
            !_mutex.WaitOne(1000))
            return;

        Owner.AddRandomItem();
        _mutex.ReleaseMutex();
    }
}
