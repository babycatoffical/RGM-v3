using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using RGM.API.Features;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerRoles;
using Exiled.Events.EventArgs.Scp049;

namespace RGM.Modes.Abilities.Mythic;

[Ability(
    "구속", "적을 공격 불가 상태로 구속할 수 있는 리볼버를 얻습니다. (관통샷 가능, 사거리 100)\nalt를 눌러 대상의 구속을 해제할 수 있습니다.",
    AbilityCategory.Mythic,
    AbilityType.MYTHIC_ANCHOR)] 
public class Anchor : Ability
{
    ushort itemSerial = 0;
    public List<Player> TargetPlayer = new();
    private Dictionary<Player, byte> LWPlayerIntensity = new();
    private Dictionary<Player, float> LWPlayerDuration = new();
    private Dictionary<Player, byte> FPlayerIntensity = new();
    private Dictionary<Player, float> FPlayerDuration = new();
    public override void OnEnabled()
    {
        Item item = Owner.AddItem(ItemType.GunRevolver);

        itemSerial = item.Serial;

        Exiled.Events.Handlers.Player.ChangedItem += OnChangedItem;
        Exiled.Events.Handlers.Player.Shooting += OnShooting;
        Exiled.Events.Handlers.Player.TogglingNoClip += OnTogglingNoClip;
        Exiled.Events.Handlers.Player.Hurting += OnHurting;
        Exiled.Events.Handlers.Scp049.Attacking += On049Attack;
        Exiled.Events.Handlers.Player.Handcuffing += OnArrest;
        
        Timing.RunCoroutine(Main());
    }

    public override void OnDisabled()
    {
        Timing.RunCoroutine(Disable());
    }

    public void OnChangedItem(ChangedItemEventArgs ev)
    {
        if (ev.Item == null) return;
        if (itemSerial != ev.Player.CurrentItem.Serial) return;
        if (ev.Player != Owner) return;
        if (itemSerial == ev.Item.Serial)
                ev.Player.AddHint("구속", $"<b><color={ABattle.RatingColor["신화"]}>구속</color></b> 능력이 있는 <b>리볼버</b>입니다!");
        
    }

    private void OnShooting(ShootingEventArgs ev)
    {
        if (ev.Player != Owner) return;
        if (ev.Player.CurrentItem.Serial != itemSerial) return;
        if (ev.Item.Serial != itemSerial) return;
        ev.Player.CurrentItem.As<Firearm>().MagazineAmmo = 6;

        if (!Tools.TryGetLookPlayers(ev.Player, 100f, out List<Player> players, out _)) return;
        bool enemy = false;

        foreach (var player in players)
        {
            if (player == null) continue;
            if (player.Role == RoleTypeId.Spectator) continue;
            if (!HitboxIdentity.IsEnemy(ev.Player.ReferenceHub, player.ReferenceHub)) continue;
            if (TargetPlayer.Contains(player)) continue;

            if (Owner.IsCaptured())
            {
                Owner.AddHint("알림", $"구속당한 상태에서 다른 플레이어를 구속할 수 없습니다.", 3f);
                continue;
            }

            if (player.IsCaptured())
            {
                Owner.AddHint("알림", $"다른 플레이어에게 구속당한 플레이어는 구속할 수 없습니다.", 3f);
                continue;
            }
            byte LWintensity = player.GetEffect(EffectType.Lightweight).Intensity;
            float LWduration = player.GetEffect(EffectType.Lightweight).Duration;

            byte Fintensity = player.GetEffect(EffectType.Lightweight).Intensity;
            float Fduration = player.GetEffect(EffectType.Lightweight).Duration;

            LWPlayerIntensity.Add(player, LWintensity);
            LWPlayerDuration.Add(player, LWduration);
            FPlayerIntensity.Add(player, Fintensity);
            FPlayerDuration.Add(player, Fduration);
            TargetPlayer.Add(player);

            enemy = true;
        }

        if (enemy)
            Hitmarker.SendHitmarkerDirectly(ev.Player.ReferenceHub, 3f);

        
    }

    private IEnumerator<float> Main()
    {
        while (true)
        {
            if (Owner.IsDead || Owner == null) yield break;

            if(TargetPlayer.Count > 0)
            {
                Owner.AddHint("알림", $"구속을 해제하려면 리볼버를 들고 [ALT]를 누르십시오.\n현재 붙잡힌 플레이어 수: {TargetPlayer.Count}", 0.1f);
            }

            //Vector3 origin = Owner.ReferenceHub.PlayerCameraReference.position;
            //Vector3 direction = Owner.ReferenceHub.PlayerCameraReference.forward;

            //float maxDistance = 4f;
            //int ignorePlayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            Vector3 position = Owner.Position;

            //if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, ignorePlayerMask))
                //position = hit.point - (direction * 0.2f);

            foreach (var player in TargetPlayer.ToList())
            {
                if (player == null || player.IsDead || player.Role == RoleTypeId.Spectator)
                {
                    TargetPlayer.Remove(player);
                    continue;
                }
                player.Position = position;

                byte LWintensity = player.GetEffect(EffectType.Lightweight).Intensity;
                float LWduration = player.GetEffect(EffectType.Lightweight).Duration;

                byte Fintensity = player.GetEffect(EffectType.Lightweight).Intensity;
                float Fduration = player.GetEffect(EffectType.Lightweight).Duration;

                player.EnableEffect(EffectType.Fade, 179, 0.5f); //시야 방해 방지
                player.EnableEffect(EffectType.Ensnared, 1, 2f);
                player.EnableEffect(EffectType.Lightweight, 1, 0.5f);
                player.AddHint("알림", $"{Owner.DisplayNickname}에게 붙잡혔습니다.\n다른 플레이어를 공격 할 수 없습니다.", 0.1f);
            }
             yield return Timing.WaitForSeconds(0.034f);
        }
    }


    public void OnTogglingNoClip(TogglingNoClipEventArgs ev)
    {
        if (ev.Player != Owner) return;
        Timing.RunCoroutine(Disable());
    }

    public void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker == null || ev.Attacker == ev.Player) return;
        if (ev.Attacker == Owner && Owner.CurrentItem.Serial == itemSerial)
            ev.IsAllowed = false;

        if (TargetPlayer.Contains(ev.Attacker))
        {
            ev.IsAllowed = false;
        }
    }

    public void On049Attack(AttackingEventArgs ev)
    {
        if (ev.Player == null) return;
        if (TargetPlayer.Contains(ev.Player)) ev.IsAllowed = false;
    }

    public void OnArrest(HandcuffingEventArgs ev)
    {
        if (ev.Target == null || ev.Player == null) return;
        if (ev.Target == Owner) ev.IsAllowed = false;
    }

    public IEnumerator<float> Disable()
    {
        foreach (var player in TargetPlayer.ToList())
        {
            if (player == null) continue;

            Timing.WaitForSeconds(0.3f);
            player.EnableEffect(EffectType.Lightweight, LWPlayerIntensity[player], LWPlayerDuration[player]);
            player.EnableEffect(EffectType.Fade, FPlayerIntensity[player], FPlayerDuration[player]);
        }
        LWPlayerDuration.Clear();
        LWPlayerIntensity.Clear();
        FPlayerIntensity.Clear();
        FPlayerDuration.Clear();
        TargetPlayer.Clear();

        yield break;
    }
}

