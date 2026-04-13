using System;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using RGM.API.Features;

namespace RGM.Modes.Commands;

public class AddAbility : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (Round.IsStarted)
        {
            var players = arguments.At(0) == "all" ? PlayerManager.List : new List<Player> { Player.Get(arguments.At(0)) };
            var args = string.Join(" ", arguments.Skip(1));

            if (arguments.Count < 2)
            {
                foreach (var player in players)
                    ABattle.Instance.AddAbility(player, ABattle.Instance.GetRandomAbilities(player, AbilityCategory.Dummy, 1)[0]);
            }
            else
            {
                var ability = ABattle.Instance.FindAbility(args);

                if (ability == AbilityType.NONE)
                {
                    response = "해당 능력을 찾을 수 없습니다.";
                    return false;
                }

                foreach (var player in players)
                    try
                    {
                        ABattle.Instance.AddAbility(player, ability);
                    }
                    catch (Exception e)
                    {
                        response = arguments.At(0) == "all"
                            ? "모든 플레이어에게 역할을 지급하는 도중 예외 또는 오류가 발생하였습니다."
                            : $"""
                               플레이어에게 역할을 지급하는 도중 예외 또는 오류가 발생하였습니다.
                               단, 능력 지급이 완료됬을 수 있으며, 단순 능력 지급 도중 문제일 수 있습니다.
                               해당 문제는 로그에 기록됩니다.
                               플레이어 이름: {player.Nickname} ID: {player.Id}
                               """;
                        
                        Log.Error($"{e.Message} {e.StackTrace}");
                        return false;
                    }
            }

            response = "AddAbility Complete!";
            return true;
        }

        response = "라운드 시작 전에는 사용할 수 없습니다.";

        return false;
    }

    public string Command { get; } = "addability";

    public string[] Aliases { get; } = { "aa", "add" };

    public string Description { get; } = "워크스테이션 업그레이드ㅣ능력을 추가합니다.";
}