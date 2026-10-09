using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using RGM.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Extensions;
using static RGM.Variables.Variable;

namespace RGM.Modes
{
    [Mode(ModeCategory.Public, ModeInfo.Plus, ModeType.SoulMate)]
    class SoulMate : Mode
    {
        public override string Name => "소울메이트";
        public override string Description => "단짝 친구와 모든 것을 함께하세요!";
        public override string Detail =>
"""
<b><color=#FF00DD>소</color><color=#EB01CD>울</color><color=#D702BD>메</color><color=#C404AD>이</color><color=#B0059D>트</color> <color=#89077D>메</color><color=#76096D>이</color><color=#620A5D>킹</color></b>을 시도할 때,
체력이 높은 쪽으로 지정됩니다.

<color=red>SCP</color>가 포함된 짝들만 살아남은 경우 반드시 서로를 죽여야 합니다.
<color=red><b>이행하지 않으면 제재 대상에 해당됩니다.</b></color>
""";
        public override string Color => "FF00FF";

        public static SoulMate Instance;

        private readonly Dictionary<Player, Player> _soulMates = [];
        private readonly List<Player> _waitingPlayers = [];

        private CoroutineHandle _onModeStarted;
        private CoroutineHandle _soulMateMatching;
        private CoroutineHandle _currentItemAsync;
        private CoroutineHandle _checkIfScpSoulMate;

        public override void OnEnabled()
        {
            Exiled.Events.Handlers.Player.Dying += OnDying;
            Exiled.Events.Handlers.Player.Hurt += OnHurt;
            Exiled.Events.Handlers.Player.Healed += OnHealed;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
            Exiled.Events.Handlers.Player.UsingItemCompleted += OnUsingItemCompleted;
            Exiled.Events.Handlers.Player.Escaping += OnEscaping;

            Exiled.Events.Handlers.Scp3114.Revealing += OnRevealing;

            _onModeStarted = Timing.RunCoroutine(OnModeStarted());
            _soulMateMatching = Timing.RunCoroutine(SoulMateMatching());
            _currentItemAsync = Timing.RunCoroutine(CurrentItemCoroutine());
            _checkIfScpSoulMate = Timing.RunCoroutine(CheckIfScpSoulMate());
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Dying -= OnDying;
            Exiled.Events.Handlers.Player.Hurt -= OnHurt;
            Exiled.Events.Handlers.Player.Healed -= OnHealed;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
            Exiled.Events.Handlers.Player.UsingItemCompleted -= OnUsingItemCompleted;
            Exiled.Events.Handlers.Player.Escaping -= OnEscaping;

            Exiled.Events.Handlers.Scp3114.Revealing -= OnRevealing;

            Timing.KillCoroutines(_onModeStarted);
            Timing.KillCoroutines(_soulMateMatching);
            Timing.KillCoroutines(_currentItemAsync);
            Timing.KillCoroutines(_checkIfScpSoulMate);
        }

        private IEnumerator<float> OnModeStarted()
        {
            while (true)
            {
                foreach (var _ in PlayerManager.List
                             .Where(player => PlayerManager.List.Count(x => x.IsAlive) < 3 && 
                                              player.Role.Type != PlayerRoles.RoleTypeId.Tutorial))
                {
                    PlayerManager.List.Where(x => x.IsAlive).ToList().ForEach(x => Server.ExecuteCommand($"/fc {x.Id} Tutorial 1"));
                    PlayerManager.List.ForEach(x => x.AddBroadcast(15, $"<size=30><b>{(PlayerManager.List.Count(y => y.IsAlive) == 2 
                        ? "<color=#ffd700>소울메이트</color>"
                        : "<color=#BFFF00>외톨이</color>")}</b>({string.Join(", ", PlayerManager.List.Where(z => z.IsAlive).Select(w => w.DisplayNickname))})의 승리입니다!</size>"));
                    yield return Timing.WaitForSeconds(100f);
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }

        private IEnumerator<float> SoulMateMatching()
        {
            while (true)
            {
                foreach (var sm in _soulMates.Keys.ToList())
                {
                    if (_soulMates[sm] != null) continue;
                    Player soulMate = _soulMates[sm];

                    if (soulMate != null) _soulMates.Remove(soulMate);
                    _soulMates.Remove(sm);

                    sm.AddHint("소울메이트 매칭", "누군가와의 매칭이 해제되었습니다.", 1.2f);
                }

                foreach (var player in PlayerManager.List)
                {
                    if (player.IsAlive)
                    {
                        if (_soulMates.ContainsKey(player) || _waitingPlayers.Contains(player)) continue;
                        _waitingPlayers.Add(player);

                        player.AddHint("소울메이트 매칭", "누군가와 매칭되기를 기다리는 중입니다..", 1.2f);
                    }
                    else
                    {
                        _waitingPlayers.Remove(player);

                        if (!_soulMates.TryGetValue(player, out Player soulMate)) continue;
                        if (soulMate != null)
                            _soulMates.Remove(soulMate);
                        _soulMates.Remove(player);

                        player.AddHint("소울메이트 매칭", "누군가와의 매칭이 해제되었습니다.", 1.2f);
                        soulMate?.AddHint("소울메이트 매칭", "누군가와의 매칭이 해제되었습니다.", 1.2f);
                    }
                }

                while (_waitingPlayers.Count > 1)
                {
                    Player first = _waitingPlayers.GetRandomValue();
                    Player second = _waitingPlayers.Where(x => x != first).GetRandomValue();

                    _waitingPlayers.Remove(first);
                    _waitingPlayers.Remove(second);

                    _soulMates.Add(first, second);
                    _soulMates.Add(second, first);
                    
                    if (first.MaxHealth > second.MaxHealth)
                        SetSoulMate(second, first);
                    else
                        SetSoulMate(first, second);

                    first.AddHint("소울메이트 매칭", "누군가와 새롭게 매칭되었습니다.", 5);
                    second.AddHint("소울메이트 매칭", "누군가와 새롭게 매칭되었습니다.", 5);
                }

                yield return Timing.WaitForSeconds(1f);
            }

            void SetSoulMate(Player a, Player b)
            {
                a.MaxHealth = b.MaxHealth;
                a.Health = b.Health;

                a.ClearItems();
                foreach (var Item in b.Items)
                    a.AddItem(Item.Type);
            }
        }

        private IEnumerator<float> CurrentItemCoroutine() // CurrentItemAsync..?
        {
            Dictionary<Player, Item> currentItems = new Dictionary<Player, Item>();

            while (true)
            {
                foreach (var player in PlayerManager.List.Where(x => x.IsAlive &&
                                                                     _soulMates.ContainsKey(x)))
                {
                    if (currentItems.ContainsKey(player))
                    {
                        if (currentItems[player] != player.CurrentItem)
                            Timing.CallDelayed(0.1f, () =>
                            {
                                if (!_soulMates.TryGetValue(player, out Player soulMate) || soulMate == null) return;

                                if (player.CurrentItem == null)
                                    soulMate.CurrentItem = null;

                                else
                                {
                                    foreach (var Item in soulMate.Items)
                                    {
                                        if (Item.Type != player.CurrentItem.Type) continue;
                                        soulMate.CurrentItem = Item;
                                        break;
                                    }
                                }
                            });

                        currentItems[player] = player.CurrentItem;
                    }
                    else
                        currentItems.Add(player, player.CurrentItem);
                }

                yield return Timing.WaitForOneFrame;
            }
        }

        private IEnumerator<float> CheckIfScpSoulMate()
        {
            var teamKillEnabled = false;
            while (true)
            {
                try
                {
                    int totalSoulMatePairs = _soulMates.Count;
                    int scpSoulMatePairs = _soulMates.Count(pair => pair.Key.IsScpRole() || pair.Value.IsScpRole());

                    if (totalSoulMatePairs == scpSoulMatePairs)
                    {
                        if (!teamKillEnabled)
                        {
                            teamKillEnabled = true;
                            FriendlyFire.Instance.OnEnabledForNoNuke();
                        }

                        foreach (var player in PlayerManager.List.Where(x => x.IsAlive))
                            player.AddHint("소울메이트 경고",
                                $"<size=25><color=red>SCP</color>가 포함된 짝들만이 살아남았습니다. 지금부터 자신의 짝을 제외하고 서로 죽이세요.</size>\n<size=20><color=red><b>죽이지 않으면 제재 대상입니다.</b></color></size>",
                                1.2f);
                    }
                    else if (teamKillEnabled)
                    {
                        teamKillEnabled = false;
                        FriendlyFire.Instance.OnDisabled();
                    }}
                catch (Exception e)
                {
                    Log.Error($"CheckIfScpSoulMate: {e}");
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }

        private void OnDying(DyingEventArgs ev)
        {
            if (!_soulMates.TryGetValue(ev.Player, out Player soulMate) || soulMate == null) return;

            string playerColor = ev.Player.Role.Color.ToHex();
            string soulMateColor = soulMate.Role.Color.ToHex();

            Timing.CallDelayed(Timing.WaitForOneFrame, () =>
            {
                if (!ev.Player.IsDead ||
                    !_soulMates.TryGetValue(ev.Player, out soulMate) || 
                    soulMate == null ||
                    !soulMate.IsAlive) return;

                Timing.CallDelayed(Timing.WaitForOneFrame, () =>
                {
                    foreach (var player in PlayerManager.List.Where(x => x.IsDead))
                        player.AddBroadcast(10, $"<size=20><color={playerColor}>{ev.Player.DisplayNickname}</color>(와)과 <color={soulMateColor}>{soulMate.DisplayNickname}</color>(은)는 <color=#FE2EF7>소울메이트</color>였습니다.</size>");
                });

                soulMate.ClearInventory();
                if (GodModePlayers.Contains(soulMate)) GodModePlayers.Remove(soulMate);
                soulMate.Kill(ev.DamageHandler);

                Tools.MessageTranslated("", $"<color=red>{ev.Attacker?.DisplayNickname}</color>(이)가 영혼의 단짝이였던 <color=#5858FA>{ev.Player.DisplayNickname}</color>와(과) <color=#FE2EF7>{soulMate.DisplayNickname}</color>을(를) 사이좋게 하늘로 보냈습니다.");
            });
        }

        private void OnHurt(HurtEventArgs ev)
        {
            if (!_soulMates.TryGetValue(ev.Player, out var soulMate)) return;
            soulMate.MaxHealth = ev.Player.MaxHealth;
            soulMate.Health = ev.Player.Health;
        }

        private void OnHealed(HealedEventArgs ev)
        {
            if (!_soulMates.TryGetValue(ev.Player, out var soulMate)) return;
            soulMate.MaxHealth = ev.Player.MaxHealth;
            soulMate.Health = ev.Player.Health;
        }

        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (!_soulMates.TryGetValue(ev.Player, out var soulMate)) return;
            soulMate.AddItem(ev.Pickup.Type);
        }

        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (!_soulMates.TryGetValue(ev.Player, out var soulMate)) return;
            foreach (var Item in soulMate.Items)
            {
                if (Item.Type == ev.Item.Type)
                {
                    soulMate.RemoveItem(Item);
                    break;
                }
            }
        }

        private void OnUsingItemCompleted(UsingItemCompletedEventArgs ev)
        {
            if (!_soulMates.TryGetValue(ev.Player, out var soulMate)) return;
            foreach (var Item in soulMate.Items)
            {
                if (Item.Type == ev.Item.Type)
                {
                    soulMate.RemoveItem(Item);
                    break;
                }
            }
        }

        private void OnEscaping(EscapingEventArgs ev)
        {
            Timing.CallDelayed(0.1f, () =>
            {
                if (!_soulMates.TryGetValue(ev.Player, out var soulMate)) return;
                ev.Player.MaxHealth = soulMate.MaxHealth;
                ev.Player.Health = soulMate.Health;

                soulMate.ClearInventory();
                foreach (var Item in ev.Player.Items)
                    soulMate.AddItem(Item.Type);
            });
        }

        private void OnRevealing(Exiled.Events.EventArgs.Scp3114.RevealingEventArgs ev) 
            => ev.Player.DropItems();
    }
}
