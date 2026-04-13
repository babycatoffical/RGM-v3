using AdminToys;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.OverlayAnims;
using PlayerStatsSystem;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using RGM.API.Interfaces;
using RGM.Modes.SubClass;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RGM.Variables.Variable;

namespace RGM.API.Features
{
    public static class PlayerManager
    {
        public static List<Player> List => Main.Instance.Config.FixedModes.Any() ? Player.List.ToList() : Player.List.Where(x => x.IsNPC || (!x.IsDND() && !x.IsNonePlayer())).ToList();

        public static bool IsUsingTranslator(this Player player)
        {
            return Main.Instance.Config.FixedModes.Any() && TranslatorPlayers[player] != "ko";
        }

        public static bool IsDND(this Player player)
        {
            return !Main.Instance.Config.FixedModes.Any() && UsersManager.UsersCache[player.UserId][23] == "1";
        }

        public static bool IsNonePlayer(this Player player)
        {
            return NonePlayer.Players.Contains(player);
        }

        public static bool IsScpRole(this Player player)
        {
            return IsScpRole(player.Role.Type);
        }

        public static bool IsScpRole(this RoleTypeId roleTypeId)
        {
            return roleTypeId.IsScp() || roleTypeId.ToString().Contains("Flamingo");
        }

        public static void Setup(this Player player)
        {
            TranslatorPlayers.Add(player, "ko");
            Chats.Add(player, new List<string>());

            var text = Tools.CreateText(Vector3.zero, new Quaternion(0, 180, 0, 0), "", 0);
            text.Parent = player.Transform;
            Texts.Add(player, text);

            OnGround.Add(player.UserId, 5);

            if (!PlayersAudio.ContainsKey(player))
            {
                AudioPlayer audioPlayer = AudioPlayer.CreateOrGet($"Player - {player.UserId}", condition: hub =>
                {
                    Player ply = Player.Get(hub);

                    return (ply == player && !MuteBGMPlayers.Contains(ply)) ||
                    (player.CurrentSpectatingPlayers.Contains(ply) && !MuteBGMPlayers.Contains(ply));
                }
                , onIntialCreation: p =>
                {
                    Speaker speaker = p.AddSpeaker("Main", isSpatial: false, minDistance: 0, maxDistance: 5000);
                });

                PlayersAudio.Add(player, audioPlayer);
            }

            if (!PlayersReport.ContainsKey(player.UserId))
            {
                PlayersReport.Add(player.UserId, new PlayerReport
                {
                    Kill = 0,
                    Death = 0,
                    Revive = 0,
                    KillScp = 0,
                    KillHuman = 0,
                    Damage = 0,
                    LastDeath = DateTime.MinValue
                });
            }
        }

        public static void AddEffect(this Player player, EffectType type, int intensity, float duration = 0f, bool addDuration = false)
        {
            if (!EffectIntensities.ContainsKey(player))
                EffectIntensities[player] = new Dictionary<EffectType, int>();

            if (!EffectIntensities[player].ContainsKey(type))
                EffectIntensities[player][type] = 0;

            EffectIntensities[player][type] += intensity;

            const byte MaxIntensity = 255;
            byte applyIntensity = (byte)Math.Min(EffectIntensities[player][type], MaxIntensity);

            var effect = player.ActiveEffects.FirstOrDefault(x => x.GetEffectType() == type);
            float newDuration = effect && addDuration ? effect.Duration + duration : Math.Max(effect?.Duration ?? 0, duration);

            player.DisableEffect(type);
            player.EnableEffect(type, applyIntensity, duration == 0f ? 0f : newDuration);

            if (duration > 0f)
            {
                Timing.CallDelayed(duration, () =>
                {
                    if (EffectIntensities.ContainsKey(player) && EffectIntensities[player].ContainsKey(type))
                    {
                        EffectIntensities[player][type] -= intensity;
                        if (EffectIntensities[player][type] <= 0)
                        {
                            EffectIntensities[player].Remove(type);
                            player.DisableEffect(type);
                        }
                        else
                        {
                            byte newApplyIntensity = (byte)Math.Min(EffectIntensities[player][type], 255);
                            player.EnableEffect(type, newApplyIntensity);
                        }
                    }
                });
            }
        }

        public static void RemoveEffect(this Player player, EffectType type, int intensity)
        {
            if (!EffectIntensities.ContainsKey(player) || !EffectIntensities[player].ContainsKey(type))
                return;

            EffectIntensities[player][type] -= intensity;
            if (EffectIntensities[player][type] <= 0)
            {
                EffectIntensities[player].Remove(type);
                player.DisableEffect(type);
            }
            else
            {
                byte applyIntensity = (byte)Math.Min(EffectIntensities[player][type], 255);
                player.EnableEffect(type, applyIntensity);
            }
        }

        public static void ClearEffect(this Player player)
        {
            foreach (var effect in player.ActiveEffects.ToList())
                player.DisableEffect(effect.GetEffectType());

            if (EffectIntensities.ContainsKey(player))
                EffectIntensities[player].Clear();
        }

        public static void Hit(this Player player, Player attacker, float damage)
        {
            attacker.ShowHitMarker(damage / 10);
            player.Hurt(
                new DisruptorDamageHandler(
                    new InventorySystem.Items.Firearms.ShotEvents.DisruptorShotEvent
                        (InventorySystem.Items.ItemIdentifier.None, attacker.Footprint, InventorySystem.Items.Firearms.Modules.DisruptorActionModule.FiringState.FiringRapid)
                , player.Position, 
                damage));
        }

        public static bool HasKeycardPermission(this Player player, KeycardPermissions permissions, bool requiresAllPermissions = false)
        {
            if (player.IsEffectActive<AmnesiaVision>())
                return false;

            return requiresAllPermissions
                ? player.Items.Any(item => item is Keycard keycard && keycard.Permissions.HasFlag(permissions))
                : player.Items.Any(item => item is Keycard keycard && (keycard.Permissions & permissions) != 0);
        }

        public static void AddCustomKeycard(this Player player, string info)
        {
            Server.ExecuteCommand($"/ckeycard {player.Id} {info}");

            Server.ExecuteCommand($"/ckeycard 1 KeycardCustomSite02 커스텀_키카드 0 0 0 #56C491 #C1CE79 jumpscare-Scp939 #891064 AudioClips 100");
        }

        public static bool AddBadge(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            if (arguments.HasValue && arguments.Value.Count < 2)
            {
                response = "칭호추가 <player> <badge name>";
                return false;
            }

            if (Badges.ContainsKey(args))
            {
                List<string> uc = UsersManager.UsersCache[userId];

                if (uc[10] == "0")
                {
                    uc[10] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add badge.";

                    UsersManager.SaveUsers();
                    return true;
                }
                if (uc[10].Split('/').Contains(args))
                {
                    response = "This player already have this badge.";
                    return false;
                }
                uc[10] += $"/{args}";
                UsersManager.UsersCache[userId] = uc;
                response = "Successfully add badge.";

                UsersManager.SaveUsers();
                return true;
                
            }

            response = "This badge is not exist.";
            return false;
        }

        public static bool AddCustom(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            if (arguments is { Count: < 2 })
            {
                response = "커스텀추가 <player> <custom feature name>";
                return false;
            }
            if (Customizations.ContainsKey(args))
            {
                List<string> uc = UsersManager.UsersCache[userId];

                if (uc[7] == "0")
                {
                    uc[7] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add custom feature.";

                    UsersManager.SaveUsers();
                    return true;
                }
                if (uc[7].Split('/').Contains(args))
                {
                    response = "This player already have this custom feature.";
                    return false;
                }
                uc[7] += $"/{args}";
                UsersManager.UsersCache[userId] = uc;
                response = "Successfully add custom feature.";

                UsersManager.SaveUsers();
                return true;
            }
            response = "This custom feature is not exist.";
            return false;
        }

        public static bool AddKillEffect(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            if (arguments is {  Count: < 2 })
            {
                response = "킬이펙트추가 <player> <kill effect name>";
                return false;
            }

            if (KillEffects.ContainsKey(args))
            {
                List<string> uc = UsersManager.UsersCache[userId];

                if (uc[3] == "0")
                {
                    uc[3] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add kill effect.";

                    UsersManager.SaveUsers();
                    return true;
                }
                if (uc[3].Split('/').Contains(args))
                {
                    response = "This player already have this kill effect.";
                    return false;
                }
                uc[3] += $"/{args}";
                UsersManager.UsersCache[userId] = uc;
                response = "Successfully add kill effect.";

                UsersManager.SaveUsers();
                return true;
            }
            response = "This kill effect is not exist.";
            return false;
        }

        public static bool AddPaint(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            if (arguments.HasValue && arguments.Value.Count < 2)
            {
                response = "페인트추가 <player> <paint name>";
                return false;
            }
            else if (Paints.ContainsKey(args))
            {
                List<string> uc = UsersManager.UsersCache[userId];

                if (uc[8] == "0")
                {
                    uc[8] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add paint.";

                    UsersManager.SaveUsers();
                    return true;
                }
                else
                {
                    if (uc[8].Split('/').Contains(args))
                    {
                        response = "This player already have this paint.";
                        return false;
                    }
                    else
                    {
                        uc[8] += $"/{args}";
                        UsersManager.UsersCache[userId] = uc;
                        response = "Successfully add paint.";

                        UsersManager.SaveUsers();
                        return true;
                    }
                }
            }
            else
            {
                response = "This paint is not exist.";
                return false;
            }
        }

        public static bool SetCash(this string userId, int cash, out string response, bool result = true)
        {
            List<string> uc = UsersManager.UsersCache[userId];

            if (result)
            {
                if (cash < 0)
                {
                    response = "0 upper";
                    return false;
                }
                else
                {
                    uc[2] = cash.ToString();
                    UsersManager.UsersCache[userId] = uc;
                    response = "successfully set up Cash.";

                    UsersManager.SaveUsers();
                    return true;
                }
            }
            else
            {
                response = $"{uc[2]}";
                return true;
            }
        }

        public static bool SetRC(this string userId, int rc, out string response, bool result = true)
        {
            List<string> uc = UsersManager.UsersCache[userId];

            if (result)
            {
                if (rc < 0)
                {
                    response = "0 upper.";
                    return false;
                }
                else
                {
                    uc[1] = rc.ToString();
                    UsersManager.UsersCache[userId] = uc;
                    response = "successfully set up Random Coin.";

                    UsersManager.SaveUsers();
                    return true;
                }
            }
            else
            {
                response = $"{uc[1]}";
                return false;
            }
        }

        public static bool SetExp(this string userId, int rc, out string response, bool result = true)
        {
            List<string> uc = UsersManager.UsersCache[userId];

            if (result)
            {
                if (rc < 0)
                {
                    response = "0 upper.";
                    return false;
                }
                else
                {
                    uc[0] = rc.ToString();
                    UsersManager.UsersCache[userId] = uc;
                    response = "successfully set up Random Coin.";

                    UsersManager.SaveUsers();
                    return true;
                }
            }
            else
            {
                response = $"{uc[0]}";
                return false;
            }
        }

        public static bool AddProduct(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            if (arguments.HasValue && arguments.Value.Count < 2)
            {
                response = "아이템추가 <player> <item name>";
                return false;
            }
            else if (Products.Select(x => x.Name).Contains(args))
            {
                List<string> uc = UsersManager.UsersCache[userId];

                if (uc[18] == "0")
                {
                    uc[18] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add item.";

                    UsersManager.SaveUsers();
                    return true;
                }
                else
                {
                    uc[18] += $"/{args}";
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add item.";

                    UsersManager.SaveUsers();
                    return true;
                }
            }
            else
            {
                response = "This item is not exist.";
                return false;
            }
        }

        public static bool AddIcon(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            if (arguments.HasValue && arguments.Value.Count < 2)
            {
                response = "아이콘추가 <player> <icon name>";
                return false;
            }
            else if (Icons.ContainsKey(args))
            {
                List<string> uc = UsersManager.UsersCache[userId];

                if (uc[24] == "0")
                {
                    uc[24] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = "Successfully add icon.";

                    UsersManager.SaveUsers();
                    return true;
                }
                else
                {
                    if (uc[24].Split('/').Contains(args))
                    {
                        response = "This player already have this icon.";
                        return false;
                    }
                    else
                    {
                        uc[24] += $"/{args}";
                        UsersManager.UsersCache[userId] = uc;
                        response = "Successfully add icon.";

                        UsersManager.SaveUsers();
                        return true;
                    }
                }
            }
            else
            {
                response = "This icon is not exist.";
                return false;
            }
        }

        public static bool AddWarn(this string userId, string args, out string response, ArraySegment<string>? arguments = null)
        {
            List<string> uc = UsersManager.UsersCache[userId];

            if (args == "")
            {
                response = $"{string.Join("\n", uc[22].Split('/'))}\n-";
                return true;
            }
            else
            {
                if (uc[22] == "0")
                {
                    uc[22] = args;
                    UsersManager.UsersCache[userId] = uc;
                    response = $"Successfully add warn.\n\n{string.Join("\n", uc[22].Split('/'))}\n-";

                    UsersManager.SaveUsers();
                    return true;
                }
                else
                {
                    uc[22] += $"/{args}";
                    UsersManager.UsersCache[userId] = uc;
                    response = $"Successfully add warn.\n\n{string.Join("\n", uc[22].Split('/'))}\n-";

                    UsersManager.SaveUsers();
                    return true;
                }
            }
        }

        public static Item AddCandy(this Player player, CandyKindID candyKindID)
        {
            var existing = player.Items
                .Where(x => x.Type == ItemType.SCP330)
                .Select(x => x as Scp330)
                .FirstOrDefault(scp => scp != null && scp.Candies.Count() < 6);

            if (existing != null)
            {
                existing.AddCandy(candyKindID);
                return existing;
            }

            Scp330 scp330 = (Scp330)Item.Create(ItemType.SCP330);
            scp330.AddCandy(candyKindID);
            scp330.RemoveCandy(scp330.Candies.ToList()[0]);
            player.AddItem(scp330);
            return scp330;
        }

        public static void Push(this Player player, Player target, float distance = 5, float height = 1)
        {
            Vector3 horizontalDirection = target.Position - player.Position;
            horizontalDirection.y = 0;
            horizontalDirection = horizontalDirection.normalized;

            Vector3 throwVector = horizontalDirection * distance;
            throwVector.y = height;

            RaycastHit[] hits = Physics.RaycastAll(
                player.ReferenceHub.PlayerCameraReference.position + player.ReferenceHub.PlayerCameraReference.forward * 0.2f,
                player.ReferenceHub.PlayerCameraReference.forward,
                distance
            );

            RaycastHit? validHit = null;
            foreach (var h in hits.OrderBy(hit => hit.distance))
            {
                if (!h.collider.TryGetComponent<IDestructible>(out IDestructible destructible))
                {
                    validHit = h;
                    break;
                }
            }

            if (validHit.HasValue)
            {
                target.Position = validHit.Value.point;
            }
            else
            {
                target.Position = player.Position + throwVector;
            }
        }

        public static void ApplyGodMode(this Player player, float time)
        {
            GodModePlayers.Add(player);

            Timing.CallDelayed(time, () =>
            {
                if (GodModePlayers.Contains(player))
                    GodModePlayers.Remove(player);
            });
        }

        public static void Grab(this Player player)
        {
            OverlayAnimationsSubcontroller subcontroller;
            if (!(player.ReferenceHub.roleManager.CurrentRole is IFpcRole currentRole) ||
                !(currentRole.FpcModule.CharacterModelInstance is AnimatedCharacterModel
                    characterModelInstance) ||
                !characterModelInstance.TryGetSubcontroller<OverlayAnimationsSubcontroller>(out subcontroller))
            {
                return;
            }
            subcontroller._overlayAnimations[1].OnStarted();
            subcontroller._overlayAnimations[1].SendRpc();
        }

        public static Item AddRandomItem(this Player player)
        {
            List<ItemType> poll = new();

            List<ItemType> L = new()
            {
                // SCP 아이템
                ItemType.GunSCP127,
                ItemType.SCP1509,
                ItemType.SCP268,
                ItemType.SCP1344,

                // 무기
                ItemType.Jailbird,
                ItemType.MicroHID,
                ItemType.ParticleDisruptor,
            };
            List<ItemType> S = new()
            {
                // SCP 아이템
                ItemType.AntiSCP207,
                ItemType.SCP2176,
                ItemType.SCP018,
                ItemType.SCP1576,

                // 카드
                ItemType.KeycardO5,

                // 무기
                ItemType.GunLogicer,
                ItemType.GunFRMG0,
            };
            List<ItemType> A = new()
            {
                // SCP 아이템
                ItemType.SCP207,
                ItemType.SCP244a,
                ItemType.SCP244b,
                ItemType.SCP500,
                ItemType.SCP1853,

                // 카드
                ItemType.KeycardMTFCaptain,
                ItemType.KeycardMTFOperative,
                ItemType.KeycardChaosInsurgency,
                ItemType.KeycardFacilityManager,

                // 무기
                ItemType.GunE11SR,
                ItemType.GunAK,
                ItemType.GunA7,
                ItemType.GunShotgun,
                ItemType.GunCom45,

                // 치료
                ItemType.Adrenaline,

                // 방탄복
                ItemType.ArmorHeavy,

                // 기타
                ItemType.GrenadeHE
            };
            List<ItemType> B = new()
            {
                // SCP 아이템
                ItemType.SCP330,

                // 카드
                ItemType.KeycardZoneManager,
                ItemType.KeycardGuard,
                ItemType.KeycardMTFPrivate,
                ItemType.KeycardContainmentEngineer,

                // 무기
                ItemType.GunCrossvec,
                ItemType.GunRevolver,
                ItemType.GunFSP9,

                // 치료
                ItemType.Medkit,

                // 방탄복
                ItemType.ArmorCombat,

                // 기타
                ItemType.Radio,
                ItemType.GrenadeFlash
            };
            List<ItemType> C = new()
            {
                // 카드
                ItemType.KeycardJanitor,
                ItemType.KeycardScientist,
                ItemType.KeycardResearchCoordinator,
                ItemType.SurfaceAccessPass,

                // 무기
                ItemType.GunCOM18,
                ItemType.GunCOM15,

                // 탄약
                //ItemType.Ammo12gauge,
                //ItemType.Ammo44cal,
                //ItemType.Ammo556x45,
                //ItemType.Ammo762x39,
                //ItemType.Ammo9x19,

                // 치료
                ItemType.Painkillers,

                // 방탄복
                ItemType.ArmorLight,

                // 기타
                ItemType.Flashlight,
                ItemType.Lantern,
                ItemType.Coin,
            };
            List<ItemType> D = new()
            {
                // 카드
                ItemType.KeycardCustomManagement,
                ItemType.KeycardCustomMetalCase,
                ItemType.KeycardCustomSite02,
                ItemType.KeycardCustomTaskForce,

                // 기타
                ItemType.DebugRagdollMover,
            };

            foreach (var iL in L)
                poll.Add(iL);

            for (int i = 0; i < 2; i++)
            {
                foreach (var iS in S)
                    poll.Add(iS);
            }

            for (int i = 0; i < 5; i++)
            {
                foreach (var iA in A)
                    poll.Add(iA);
            }

            for (int i = 0; i < 10; i++)
            {
                foreach (var iB in B)
                    poll.Add(iB);
            }

            for (int i = 0; i < 20; i++)
            {
                foreach (var iC in C)
                    poll.Add(iC);
            }

            for (int i = 0; i < 2; i++)
            {
                foreach (var iD in D)
                    poll.Add(iD);
            }

            Item item = player.AddItem(poll.GetRandomValue());

            void light(Color color)
            {
                SchematicObject schematic = ObjectSpawner.SpawnSchematic("Light", Vector3.zero);
                LightSourceToy light = schematic.GetComponentsInChildren<LightSourceToy>().First();

                schematic.transform.parent = player.Transform;
                schematic.transform.localPosition = Vector3.zero;

                light.NetworkLightColor = color;
                light.NetworkLightRange = 50;
                light.NetworkLightIntensity = 10;

                Timing.CallDelayed(3, schematic.Destroy);
            }

            if (L.Contains(item.Type))
            {
                Tools.PlaySound(player.Transform, "L 등급", 4);
                light(Color.red);
            }
            if (S.Contains(item.Type))
            {
                Tools.PlaySound(player.Transform, "S 등급", 2);
                light(Color.yellow);
            }
            if (A.Contains(item.Type))
            {
                light(new Color(2.33f, 0.92f, 2.55f));
            }

            return item;
        }

        public static void AddRandomCandy(this Player player)
        {
            player.AddCandy(Tools.PickRandomCandy());
        }

        public static void AddBroadcast(this Player player, ushort duration, string message, byte priority = 0, string tag = "")
        {
            message = message.Replace("<color=#855439>*</color>", "");

            if (player.IsUsingTranslator() && tag != "chat" && tag != "kill")
            {
                TranslationManager.TranslatePreserveNewlines(message, TranslatorPlayers[player], translated => 
                {
                    MultiBroadcast.API.BroadcastExtensions.AddBroadcast(player, duration, translated, priority, tag);
                });
            }
            else
                MultiBroadcast.API.BroadcastExtensions.AddBroadcast(player, duration, message, priority, tag);
        }

        public static void EditBroadcast(this Player player, string text, string tag)
        {
            if (player.IsUsingTranslator())
            {
                TranslationManager.TranslatePreserveNewlines(text, TranslatorPlayers[player], translated =>
                {
                    MultiBroadcast.API.BroadcastExtensions.EditBroadcast(player, translated, tag);
                });
            }
            else
                MultiBroadcast.API.BroadcastExtensions.EditBroadcast(player, text, tag);
        }

        public static void AddCustomHint(this Player player, HintServiceMeow.Core.Models.Hints.Hint hint)
        {
            if (player.IsUsingTranslator())
            {
                TranslationManager.TranslatePreserveNewlines(hint.Text, TranslatorPlayers[player], translated =>
                {
                    hint.Text = translated;
                    HintServiceMeow.Core.Extension.ExiledPlayerExtension.AddHint(player, hint);
                });
            }
            else
                HintServiceMeow.Core.Extension.ExiledPlayerExtension.AddHint(player, hint);
        }

        public static void ExplodeGrenade(this Player player, Vector3? pos = null, float fuseTime = 0, ItemType grenade = ItemType.GrenadeHE, bool ignore = false, bool kill = true)
        {
            if (pos == null)
                pos = player.Position;

            if (grenade == ItemType.GrenadeFlash)
            {
                var g = (FlashGrenade)Item.Create(grenade, player);
                g.FuseTime = fuseTime;
                g.SpawnActive(pos.Value, player);
            }
            else
            {
                var g = (ExplosiveGrenade)Item.Create(grenade, player);
                g.FuseTime = fuseTime;
                g.MaxRadius = ignore ? 0 : g.MaxRadius;
                g.SpawnActive(pos.Value, player);

                if (kill)
                    player.Kill(DamageType.Explosion);
            }
        }
    }
}
