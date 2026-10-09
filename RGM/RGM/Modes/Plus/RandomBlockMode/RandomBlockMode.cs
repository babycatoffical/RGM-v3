using System;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles.FirstPersonControl;
using RGM.API.Features;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RGM.Variables.Variable;

namespace RGM.Modes
{
    [Mode(ModeCategory.Public, ModeInfo.Plus, ModeType.RandomBlockMode)]
    public class RandomBlockMode : Mode
    {
        public override string Name => "랜덤금지모드";
        public override string Description => "절대로 금지된 행동을 해선 안됩니다!";
        public override string Detail =>
"""
5 ~ 120초 사이에 플레이어마다 금지된 행동이 변경됩니다.
금지된 행동이 고지되기 전에 2초 간 경고 시간이 주어집니다.

금지된 행동을 하면 귀여워질 수 있습니다.
""";
        public override string Color => "d97053";

        private CoroutineHandle _onModeStarted;
        private CoroutineHandle _check;

        private readonly Dictionary<Player, BlockedActions> _dict = new();
        private readonly Dictionary<Player, Vector3> _posDict = new();

        private enum BlockedActions
        {
            Running, // 달리기 금지
            Jumping, // 점프 금지
            Attacking, // 공격 금지
            Speaking, // 말하기 금지
            UsingItem, // 아이템사용 금지
            InteractingDoor, // 문 상호작용 금지
            OpenGenerator, // 발전기 열기 금지
            HeldKeyCard, // 키카드 들기 금지
            HeldGun, // 총 들기 금지
            HeldMedicalItem, // 의료 아이템 들기 금지
            WalkSlowly, // 천천히 걷기 금지
            Escaping, // 탈출하기 금지
            Moving, // 움직이기 금지
        }

        public override void OnEnabled()
        {
            Exiled.Events.Handlers.Player.Jumping += OnJumping;
            Exiled.Events.Handlers.Player.Hurting += OnHurting;
            Exiled.Events.Handlers.Player.VoiceChatting += OnVoiceChatting;
            Exiled.Events.Handlers.Player.UsingItem += OnUsedItem;
            Exiled.Events.Handlers.Player.InteractingDoor += OnInteractingDoor;
            Exiled.Events.Handlers.Player.OpeningGenerator += OnOpeningGenerator;
            Exiled.Events.Handlers.Player.ChangingItem += OnChangingItem;
            Exiled.Events.Handlers.Player.Escaping += OnEscaping;

            _onModeStarted = Timing.RunCoroutine(OnModeStarted());
            _check = Timing.RunCoroutine(Check());
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Jumping -= OnJumping;
            Exiled.Events.Handlers.Player.Hurting -= OnHurting;
            Exiled.Events.Handlers.Player.VoiceChatting -= OnVoiceChatting;
            Exiled.Events.Handlers.Player.UsingItem -= OnUsedItem;
            Exiled.Events.Handlers.Player.InteractingDoor -= OnInteractingDoor;
            Exiled.Events.Handlers.Player.OpeningGenerator -= OnOpeningGenerator;
            Exiled.Events.Handlers.Player.ChangingItem -= OnChangingItem;
            Exiled.Events.Handlers.Player.Escaping -= OnEscaping;

            Timing.KillCoroutines(_onModeStarted);
            Timing.KillCoroutines(_check);
        }

        private IEnumerator<float> OnModeStarted()
        {
            while (true)
            {
                foreach (var room in Room.List)
                    room.Color = UnityEngine.Color.red;

                GlobalPlayer.TryPlay("출석 체크", 1.5f);

                yield return Timing.WaitForSeconds(2);

                foreach (var room in Room.List)
                    room.ResetColor();

                ushort time = (ushort)UnityEngine.Random.Range(5, 121);

                foreach (var player in PlayerManager.List)
                {
                    if (!_dict.ContainsKey(player))
                        _dict.Add(player, BlockedActions.Running);

                    var blockedAction = Tools.EnumToList<BlockedActions>().GetRandomValue();
                    _dict[player] = blockedAction;

                    player.AddBroadcast(time, $"<size=30>당신은 <color=red>{blockedAction.ToString().Replace("_", " ")}</color>(을)를 할 수 없습니다.</size>");
                }

                yield return Timing.WaitForSeconds(time);
            }
        }

        private IEnumerator<float> Check()
        {
            while (true)
            {
                foreach (var player in _dict.Keys.Where(x => !x.IsDead))
                {
                    try
                    {
                        var blockedAction = _dict[player];
                        FirstPersonMovementModule fpcModule =
                            (player.ReferenceHub.roleManager.CurrentRole as FpcStandardRoleBase)?.FpcModule;
                        if (fpcModule is null) continue;

                        switch (blockedAction)
                        {
                            case BlockedActions.Running:
                            {
                                if (fpcModule.CurrentMovementState == PlayerMovementState.Sprinting)
                                    player.ExplodeGrenade(ignore: true);
                                break;
                            }
                            case BlockedActions.WalkSlowly:
                            {
                                if (fpcModule.CurrentMovementState == PlayerMovementState.Sneaking)
                                    player.ExplodeGrenade(ignore: true);
                                break;
                            }
                            case BlockedActions.Moving:
                            {
                                if (!_posDict.ContainsKey(player))
                                    _posDict.Add(player, player.Position);

                                if (_posDict[player] != player.Position)
                                    player.ExplodeGrenade(ignore: true);

                                _posDict[player] = player.Position;
                                break;
                            }
                            case BlockedActions.Jumping:
                            case BlockedActions.Attacking:
                            case BlockedActions.Speaking:
                            case BlockedActions.UsingItem:
                            case BlockedActions.InteractingDoor:
                            case BlockedActions.OpenGenerator:
                            case BlockedActions.HeldKeyCard:
                            case BlockedActions.HeldGun:
                            case BlockedActions.HeldMedicalItem:
                            case BlockedActions.Escaping:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                yield return Timing.WaitForOneFrame;
            }
        }

        private void OnJumping(JumpingEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (_dict.ContainsKey(ev.Player) && _dict[ev.Player] == BlockedActions.Jumping)
                ev.Player.ExplodeGrenade(ignore: true);
        }

        private void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (ev.Attacker != null && _dict.ContainsKey(ev.Attacker) && _dict[ev.Attacker] == BlockedActions.Attacking)
                ev.Attacker.ExplodeGrenade(ignore: true);
        }

        private void OnVoiceChatting(VoiceChattingEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (_dict.ContainsKey(ev.Player) && _dict[ev.Player] == BlockedActions.Speaking && !ev.Player.IsDead)
                ev.Player.ExplodeGrenade(ignore: true);
        }

        private void OnUsedItem(UsingItemEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (_dict.ContainsKey(ev.Player) && _dict[ev.Player] == BlockedActions.UsingItem)
                ev.Player.ExplodeGrenade(ignore: true);
        }

        private void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (_dict.ContainsKey(ev.Player) && _dict[ev.Player] == BlockedActions.InteractingDoor)
                ev.Player.ExplodeGrenade(ignore: true);
        }

        private void OnOpeningGenerator(OpeningGeneratorEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (_dict.ContainsKey(ev.Player) && _dict[ev.Player] == BlockedActions.OpenGenerator)
                ev.Player.ExplodeGrenade(ignore: true);
        }

        private void OnChangingItem(ChangingItemEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (!_dict.ContainsKey(ev.Player)) return;
            
            if (_dict[ev.Player] == BlockedActions.HeldKeyCard && ev.Item.Type.IsKeycard())
                ev.Player.ExplodeGrenade(ignore: true);
            
            if (_dict[ev.Player] == BlockedActions.HeldGun && ev.Item.Type.IsWeapon())
                ev.Player.ExplodeGrenade(ignore: true);
            
            if (_dict[ev.Player] == BlockedActions.HeldMedicalItem && ev.Item.Type.IsMedical())
                ev.Player.ExplodeGrenade(ignore: true);
        }

        private void OnEscaping(EscapingEventArgs ev)
        {
            if (ev.Player.IsDead)
                return;

            if (_dict.ContainsKey(ev.Player) && _dict[ev.Player] == BlockedActions.Escaping)
                ev.Player.ExplodeGrenade(ignore: true);

            ev.IsAllowed = false;
        }
    }
}
