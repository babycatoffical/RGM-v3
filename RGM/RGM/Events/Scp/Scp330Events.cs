using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Hazards;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Scp330;
using InventorySystem.Items.Usables.Scp330;
using MapGeneration.Holidays;
using MEC;
using PlayerRoles;
using RGM.API.Features;
using System.Linq;
using UnityEngine;

namespace RGM.Events
{
    public static class Scp330Events
    {
        public static void OnInteractingScp330(InteractingScp330EventArgs ev)
        {
            if (ev.Player.IsScpRole())
                return;

            Timing.CallDelayed(Timing.WaitForOneFrame, () =>
            {
                ev.Player.TryRemoveCandу(ev.Candy);
                ev.Player.AddRandomCandy();
            });
        }

        public static void OnEatingScp330(EatingScp330EventArgs ev)
        {
            if (!HolidayUtils.IsHolidayActive(HolidayType.Halloween))
            {
                if (ev.Candy.Kind == CandyKindID.Gray)
                {
                    ev.Player.AddEffect(EffectType.Lightweight, 125, 20);
                }

                if (ev.Candy.Kind == CandyKindID.White)
                {
                    ev.Player.AddEffect(EffectType.Fade, 240, 20);
                    ev.Player.AddEffect(EffectType.Ghostly, 1, 20);
                }

                if (ev.Candy.Kind == CandyKindID.Orange)
                {
                    var f = (FlashGrenade)Item.Create(ItemType.GrenadeFlash, ev.Player);
                    f.FuseTime = 3;
                    f.SpawnActive(ev.Player.Position, ev.Player);

                    Timing.CallDelayed(0.6f, () =>
                    {
                        ev.Player.RemoveEffect(EffectType.Flashed, 1);

                        Timing.CallDelayed(3, () =>
                        {
                            foreach (var p in PlayerManager.List.Where(x => Vector3.Distance(x.Position, ev.Player.Position) < 10))
                            {
                                p.RemoveEffect(EffectType.Flashed, 1);
                            }
                        });
                    });
                }

                if (ev.Candy.Kind == CandyKindID.Brown)
                {
                    if (ev.Player.IsScpRole())
                    {
                        ev.IsAllowed = false;
                        ev.Player.TryRemoveCandу(CandyKindID.Brown);

                        TantrumHazard.PlaceTantrum(ev.Player.Position);
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0, 100) < 1)
                        {
                            ev.IsAllowed = false;
                            ev.Player.TryRemoveCandу(CandyKindID.Brown);

                            Tools.PlaySound(ev.Player.Transform, "brown-candy-shit");

                            Timing.CallDelayed(3, () =>
                            {
                                Vector3 pos = ev.Player.Position;

                                var g = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE, ev.Player);
                                g.FuseTime = 0.01f;
                                g.SpawnActive(pos, ev.Player);

                                TantrumHazard.PlaceTantrum(pos);
                            });
                        }
                    }
                }

                if (ev.Candy.Kind == CandyKindID.Evil)
                {
                    ev.IsAllowed = false;
                    ev.Player.TryRemoveCandу(CandyKindID.Evil);

                    var g = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE, ev.Player);
                    g.FuseTime = 0.01f;
                    g.SpawnActive(ev.Player.Position, ev.Player);

                    for (int i = 0; i < 4; i++)
                    {
                        var g1 = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE, ev.Player);
                        g1.FuseTime = 1.5f;
                        g1.SpawnActive(ev.Player.Position, ev.Player);
                    }
                }

                if (ev.Candy.Kind == CandyKindID.Black)
                {
                    ev.IsAllowed = false;
                    ev.Player.TryRemoveCandу(CandyKindID.Black);

                    int c = UnityEngine.Random.Range(1, 18);

                    if (c == 1)
                    {
                        ev.Player.Kill("Just died. Yeah, nothing special.");
                    }
                    else if (c == 2)
                    {
                        ev.Player.AddEffect(EffectType.Flashed, 1, 10);
                        ev.Player.AddEffect(EffectType.AntiScp207, 1);
                    }
                    else if (c == 3)
                    {
                        ev.Player.AddEffect(EffectType.MovementBoost, 140, 10);
                        Timing.CallDelayed(10, () =>
                        {
                            ev.Player.AddEffect(EffectType.Slowness, 90, 10);
                            ev.Player.AddEffect(EffectType.Concussed, 1, 10);
                        });
                    }
                    else if (c == 4)
                    {
                        var g = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE, ev.Player);
                        g.FuseTime = 0.01f;
                        g.SpawnActive(ev.Player.Position, ev.Player);
                    }
                    else if (c == 5)
                    {
                        var fg = (FlashGrenade)Item.Create(ItemType.GrenadeFlash, ev.Player);
                        fg.FuseTime = 2;
                        fg.SpawnActive(ev.Player.Position, null);
                    }
                    else if (c == 6)
                    {
                        ev.Player.AddEffect(EffectType.PocketCorroding, 1);
                    }
                    else if (c == 7)
                    {
                        ev.Player.EnableEffect(EffectType.SeveredHands, 1);
                        ev.Player.EnableEffect(EffectType.MovementBoost, 255);
                    }
                    else if (c == 8)
                    {
                        ev.Player.AddCandy(CandyKindID.Pink);
                    }
                    else if (c == 9)
                    {
                        for (int i = 0; i < 2; i++)
                            ev.Player.AddCandy(CandyKindID.Black);
                    }
                    else if (c == 10)
                    {
                        Player scp = PlayerManager.List.GetRandomValue(x => x.IsScpRole());

                        if (scp != null)
                        {
                            ev.Player.Position = scp.Position;
                        }
                    }
                    else if (c == 11)
                    {
                        Item coin = ev.Player.AddItem(ItemType.Coin);
                        ev.Player.CurrentItem = coin;
                    }
                    else if (c == 12)
                    {
                        Item micro = ev.Player.AddItem(ItemType.MicroHID);
                        ev.Player.CurrentItem = micro;
                    }
                    else if (c == 13)
                    {
                        Door door = Door.List.GetRandomValue();
                        ev.Player.Position = door.Position + new Vector3(0, 2, 0);
                    }
                    else if (c == 14)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            var grenade = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE, ev.Player);
                            grenade.FuseTime = 5f;
                            grenade.SpawnActive(ev.Player.Position, null);
                        }
                    }
                    else if (c == 15)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            Item ball = ev.Player.AddItem(ItemType.SCP018);
                            ev.Player.DropItem(ball);
                        }
                    }
                    else if (c == 16)
                    {
                        foreach (var d in ev.Player.CurrentRoom.Doors)
                        {
                            if (d is BreakableDoor breakableDoor)
                            {
                                breakableDoor.Break();
                            }
                        }

                        ev.Player.CurrentRoom.Blackout(0.2f);
                    }
                    else if (c == 17)
                    {
                        ev.Player.ThrowGrenade(ProjectileType.Scp2176);
                    }
                    else if (c == 18)
                    {
                        var deadPlayer = Player.List.GetRandomValue(x => x.IsDead);
                        if (deadPlayer != null)
                        {
                            deadPlayer.Role.Set(ev.Player.Role.Type);
                            deadPlayer.Position = ev.Player.Position;
                        }
                    }
                    else if (c == 19)
                    {
                        bool check(RoleTypeId roleTypeId)
                        {
                            if (ev.Player.IsNTF)
                                return roleTypeId.IsChaos();

                            if (ev.Player.IsCHI)
                                return roleTypeId.IsNtf();

                            if (ev.Player.Role.Type == RoleTypeId.ClassD)
                                return roleTypeId == RoleTypeId.Scientist;

                            if (ev.Player.Role.Type == RoleTypeId.Scientist)
                                return roleTypeId == RoleTypeId.ClassD;

                            return true;
                        }

                        RoleTypeId newRole = Tools.EnumToList<RoleTypeId>().GetRandomValue(x => check(x));
                        ev.Player.Role.Set(newRole, RoleSpawnFlags.None);
                    }
                }

                void removeCandy(CandyKindID candyKindID)
                {
                    ev.Player.TryRemoveCandу(candyKindID);
                    ev.IsAllowed = false;
                    ev.Player.CurrentItem = null;
                }

                if (ev.Candy.Kind == CandyKindID.Yellow)
                {
                    if (UnityEngine.Random.Range(0, 100) < 5)
                    {
                        ev.Player.AddEffect(EffectType.MovementBoost, 255, 4);
                    }
                    else
                    {
                        removeCandy(CandyKindID.Yellow);

                        ev.Player.Stamina += ev.Player.StaminaStat.MaxValue / 4;
                        ev.Player.AddEffect(EffectType.MovementBoost, 10, 8);
                        ev.Player.AddEffect(EffectType.Invigorated, 1, 8);
                    }
                }

                if (ev.Candy.Kind == CandyKindID.Rainbow)
                {
                    removeCandy(CandyKindID.Rainbow);

                    ev.Player.Heal(15);
                    ev.Player.AddEffect(EffectType.Invigorated, 1, 5);
                    ev.Player.AddEffect(EffectType.BodyshotReduction, 1);
                    ev.Player.AddEffect(EffectType.RainbowTaste, 1, 10);
                    ev.Player.AddAhp(20, sustain: 10);
                }

                if (ev.Candy.Kind == CandyKindID.Pink)
                {

                    if (UnityEngine.Random.Range(0f, 100f) < 5)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            ev.Player.ExplodeGrenade(ev.Player.Position);
                        }
                    }
                }
            }
        }
    }
}
