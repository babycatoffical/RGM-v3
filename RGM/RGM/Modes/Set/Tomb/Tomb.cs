using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Mirror;
using UnityEngine;
using Exiled.API.Features.Items;
using RGM.API.Features;

using PlayerRoles;
using RGM.API.DataBases;
using Exiled.API.Extensions;
using Random = UnityEngine.Random;

namespace RGM.Modes
{
    [Mode(ModeCategory.Public, ModeInfo.Set, ModeType.Tomb)]
    class Tomb : Mode
    {
        public override string Name => "무덤";
        public override string Description => "살아남으려면 뭐라도 해야 합니다.";
        public override string Detail =>
"""
널리 펼쳐진 평지에는 <b>수많은 아이템</b>이 널려 있습니다.

배틀그라운드와 흡사하죠.</b>
""";
        public override string Color => "333333";

        public static Tomb Instance;

        private readonly List<Player> _player = [];
        private readonly List<ItemType> _ignoreItems = 
        [
            ItemType.Snowball,
            ItemType.Coal,
            ItemType.SpecialCoal,
            ItemType.SCP1507Tape,
            ItemType.SCP244a,
            ItemType.SCP244b,
            ItemType.SCP018,
            ItemType.SCP2176,
            ItemType.SCP1576
        ];

        private CoroutineHandle _onModeStarted;

        private static Vector3 RandomPosition()
        {
            return new Vector3(Random.Range(45, 144), 350, Random.Range(-95, 4));
        }

        public override void OnEnabled()
        {
            Server.FriendlyFire = true;
            Round.IsLocked = true;
            Respawn.PauseWaves(); 

            Exiled.Events.Handlers.Player.Died += OnDied;

            _onModeStarted = Timing.RunCoroutine(OnModeStarted());
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Died -= OnDied;

            Timing.KillCoroutines(_onModeStarted);
        }

        private IEnumerator<float> OnModeStarted()
        {
            Tools.LoadMap($"plane");

            PlayerManager.List.CopyTo(_player);

            var itemTypes = Tools.EnumToList<ItemType>()
                .Where(x => !_ignoreItems.Contains(x))
                .ToList();
            var ammoTypes = Tools.EnumToList<ItemType>().Where(x => x.IsAmmo()).ToList();

            for (int i = 1; i <= 999; i++)
            {
                try
                {
                    Item item = Item.Create(itemTypes.GetRandomValue());

                    if (item is Firearm firearm)
                        firearm.MagazineAmmo = firearm.MaxMagazineAmmo;

                    item.CreatePickup(RandomPosition());
                }
                catch (Exception e)
                {
                    Log.Error($"Mod Error: {e}");
                }
            }

            for (int i = 1; i <= 357; i++)
            {
                try
                {
                    Item item = Item.Create(ammoTypes.GetRandomValue());

                    item.CreatePickup(RandomPosition());
                }
                catch (Exception e)
                {
                    Log.Error($"Mod Error: {e}");
                }
            }

            foreach (var player in PlayerManager.List)
            {
                try
                {
                    player.Role.Set(RoleTypeId.Tutorial);
                    player.Position = RandomPosition();
                }
                catch (Exception e)
                {
                    Log.Error($"Mod Error: {e}");
                }
            }
            yield return 0f;
            
            yield return Timing.WaitForSeconds(120f);

            GameObject busterCall = GameObject.Find("[SP] Base");
            
            if (busterCall == null)
            {
                Log.Error("[SpearShield] [SP] Base 스폰 지점을 찾지 못했습니다.");
                yield break;
            }

            var busterCallPosition = busterCall.transform.position;
            
            foreach (var player in PlayerManager.List)
            {
                player.Position = busterCallPosition;
                player.AddBroadcast(20, "<b><size=30>[<color=yellow>버스터콜</color>]</size></b>\n<size=20>모두가 한자리에 모입니다.</size>");
            }
        }

        private void OnDied(Exiled.Events.EventArgs.Player.DiedEventArgs ev)
        {
            if (!_player.Contains(ev.Player)) return;
            _player.Remove(ev.Player);

            if (_player.Count >= 2) return;
            Round.IsLocked = false;

            PlayerManager.List.ToList().ForEach(x => x.AddBroadcast(20, $"승리자 : {_player[0].DisplayNickname}"));
            Timing.RunCoroutine(Tools.SetWinner([_player[0]], 5));
        }
    }
}
