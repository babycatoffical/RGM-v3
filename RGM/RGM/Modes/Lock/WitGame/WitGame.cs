using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using UnityEngine;
using Exiled.API.Enums;
using PlayerRoles;

using RGM.API.Features;
using Exiled.Events.EventArgs.Player;

namespace RGM.Modes
{
    [Mode(ModeCategory.Public, ModeInfo.Lock, ModeType.WitGame)]
    class WitGame : Mode
    {
        public override string Name => "눈치게임";
        public override string Description => "눈치가 가장 좋은 자만 살아남습니다.";
        public override string Detail =>
"""
<size=40><b>탈락의 조건</b></size>
2명 이상이 동시에 점프하거나(오차 간격: 0.5초),
마지막까지 점프하지 않으면 승천합니다.
""";
        public override string Color => "CEECF5";
        public override string Map => "wg";

        public static WitGame Instance;

        int Stack = 0;
        int Remain = 0;
        List<Player> JumpingPlayers = [];
        List<Player> PassPlayers = [];

        CoroutineHandle _onModeStarted;
        CoroutineHandle _jumpHint;

        public override void OnEnabled()
        {
            Round.IsLocked = true;
            Respawn.PauseWaves(); 
            Server.FriendlyFire = true;

            Exiled.Events.Handlers.Player.Jumping += OnJumping;
            Exiled.Events.Handlers.Player.Hurting += OnHurting;
            Exiled.Events.Handlers.Player.Kicking += OnKicking;

            _onModeStarted = Timing.RunCoroutine(OnModeStarted());
            _jumpHint = Timing.RunCoroutine(JumpHint());
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Jumping -= OnJumping;
            Exiled.Events.Handlers.Player.Hurting -= OnHurting;
            Exiled.Events.Handlers.Player.Kicking -= OnKicking;

            Timing.KillCoroutines(_onModeStarted);
            Timing.KillCoroutines(_jumpHint);
        }

        public IEnumerator<float> OnModeStarted()
        {
            foreach (var player in PlayerManager.List)
            {
                player.Role.Set(RoleTypeId.ClassD);
                player.EnableEffect(EffectType.Ensnared);
            }

            void SetPlayerLocation()
            {
                int playerCount = PlayerManager.List.Where(x => x.IsAlive).ToList().Count;
                float distance = 0f;

                distance = 1f + (playerCount - 2) * 0.3f;

                List<Vector3> platforms = Tools.GetCirclePoints(new Vector3(77.1384f, 322.2464f, -42.116f), distance, playerCount);

                foreach (var player in PlayerManager.List.Where(x => x.IsAlive))
                {
                    player.Position = platforms[PlayerManager.List.Where(x => x.IsAlive).ToList().IndexOf(player)];
                    player.Health = player.MaxHealth;
                }

                Stack = 0;

                PassPlayers.Clear();
            }

            while (PlayerManager.List.Where(x => x.IsAlive).ToList().Count > 1)
            {
                if (PlayerManager.List.Where(x => x.IsAlive).ToList().Count < 2)
                    break;

                if (JumpingPlayers.Where(x => x.IsAlive && x != null).ToList().Count > 0)
                {
                    if (JumpingPlayers.Where(x => x.IsAlive && x != null).ToList().Count > 1)
                    {
                        foreach (var boom in JumpingPlayers.Where(x => x.IsAlive && x != null))
                            Timing.RunCoroutine(Tools.DoRocket(boom, boom, 0.5f));

                        MultiBroadcast.API.MultiBroadcast.AddMapBroadcast(3, $"<size=25>마음이 너무 잘 맞았던 {JumpingPlayers.Where(x => x.IsAlive && x != null).ToList().Count}명({string.Join(", ", JumpingPlayers.Where(x => x.IsAlive && x != null).Select(x => x.DisplayNickname))})은 사이좋게 하늘로 갔습니다.</size>");

                        Remain = PlayerManager.List.Where(x => x.IsAlive && !JumpingPlayers.Contains(x)).ToList().Count;

                        SetPlayerLocation();

                        for (int i = 1; i < 4; i++)
                        {
                            MultiBroadcast.API.MultiBroadcast.AddMapBroadcast(1, $"<b>{i}</b>");

                            yield return Timing.WaitForSeconds(1f);
                        }
                    }
                    else
                    {
                        PassPlayers.Add(JumpingPlayers.Where(x => x.IsAlive && x != null).ToList()[0]);

                        Stack++;

                        MultiBroadcast.API.MultiBroadcast.AddMapBroadcast(3, $"<size=20>{JumpingPlayers.Where(x => x.IsAlive && x != null).ToList()[0].DisplayNickname}, {Stack}!</size>");
                    }
                }

                if (PassPlayers.Count > 0)
                {
                    List<Player> Cowards = PlayerManager.List.Where(x => x.IsAlive && !PassPlayers.Contains(x)).ToList();

                    if (Cowards.Count < 2)
                    {
                        Timing.RunCoroutine(Tools.DoRocket(Cowards[0], Cowards[0], 0.8f));

                        MultiBroadcast.API.MultiBroadcast.AddMapBroadcast(3, $"<size=25>눈치가 느린 {Cowards[0].DisplayNickname}은(는) 하늘로 갔습니다.</size>");

                        Remain = PlayerManager.List.Where(x => x.IsAlive && !Cowards.Contains(x)).ToList().Count;

                        SetPlayerLocation();

                        for (int i = 1; i < 4; i++)
                        {
                            MultiBroadcast.API.MultiBroadcast.AddMapBroadcast(1, $"<b>{i}</b>");

                            yield return Timing.WaitForSeconds(1f);
                        }
                    }
                }

                if (Remain != PlayerManager.List.Where(x => x.IsAlive).ToList().Count)
                {
                    Remain = PlayerManager.List.Where(x => x.IsAlive).ToList().Count;

                    SetPlayerLocation();
                }

                JumpingPlayers.Clear();

                yield return Timing.WaitForSeconds(0.5f);
            }

            Round.IsLocked = false;

            PlayerManager.List.ToList().ForEach(x => x.AddBroadcast(20, $"<size=25>🎉 <b><color=yellow>{PlayerManager.List.Where(x => x.IsAlive).ToList()[0].DisplayNickname}</color></b>(이)가 <color=#81F79F>눈치게임</color>에서 우승했습니다! 🎉</size>"));
            Timing.RunCoroutine(Tools.SetWinner(new List<Player>() { PlayerManager.List.Where(x => x.IsAlive).ToList()[0] }, 5));

            PlayerManager.List.Where(x => x.IsAlive).ToList()[0].DisableEffect(EffectType.Ensnared);

            yield break;
        }

        public IEnumerator<float> JumpHint()
        {
            while (true)
            {
                foreach (var player in PlayerManager.List.Where(x => x.IsAlive && !PassPlayers.Contains(x)))
                {
                    player.AddHint("눈치 챙겨", "Spaceㅣ점프를 뛰어 눈치를 챙기세요.", 1.2f);
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }

        public void OnJumping(JumpingEventArgs ev)
        {
            if (!PassPlayers.Contains(ev.Player))
                JumpingPlayers.Add(ev.Player);
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player != ev.Attacker && ev.DamageHandler.Type == DamageType.Explosion)
                ev.IsAllowed = false;
        }

        public void OnKicking(KickingEventArgs ev)
        {
            if (ev.Reason.ToLower().Contains("afk"))
                ev.IsAllowed = false;
        }
    }
}
