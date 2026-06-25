using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Extensions;
using Exiled.API.Features;
using RGM.API.Features;

namespace RGM.Modes.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class GetAbility : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        if (!Round.IsStarted)
        {
            response = "라운드가 시작되지 않았습니다.";
            return false;
        }
        
        try
        {
            if (arguments.Count == 0 || !arguments.At(0).Any() || !arguments.At(1).Any())
            {
                response = "명령어 사용법: getability <플레이어> [--random (-R), --colored (-C)]";
                return false;
            }
            
            StringBuilder builder = new(256, 1024);

            if (!Player.TryGet(arguments.At(0), out var player))
            {
                response = $"플레이어 {arguments.At(0)}을(를) 찾을 수 없습니다.";
                return false;
            }

            var abilities = arguments.Contains("--random")
                            || arguments.Contains("-R")
                ? PlayerManager.List.GetRandomValue().GetAbility()
                : player.GetAbility();

            if (arguments.Contains("--colored") || arguments.Contains("-C"))
                CreateColoredString();
            else
                CreateNonColoredString();

            response = builder.ToString();
            return true;

            void CreateNonColoredString()
            {
                builder.Append($"""
                                플레이어 {player.Nickname}의 적용된 능력:
                                |    능력 이름    |   갯수   |   등급   |
                                """);

                foreach (var items in abilities.ToList())
                {
                    var data = abilities.Select(x => x.Data.Name == items.Data.Name)
                        .Count() == 1
                        ? "*"
                        : $"{items.Data.Name}개";

                    if (builder.MaxCapacity >= 490)
                    {
                        builder.Append($"...(그 외 {abilities.Count}개)");
                        break;
                    }

                    abilities.RemoveAll(x => x.Data.Name == items.Data.Name);

                    builder.Append(
                        $"| {items.Data.Name,-13} | {data,-8} | {items.Data.Category.GetTranslation(),-8} |\n");
                }
            }

            void CreateColoredString()
            {
                builder.Append($"""
                                플레이어 <color=#399acb>{player.Nickname}</color>의 적용된 능력:
                                <b>|    능력 이름    |   갯수   |   등급   |</b>
                                """);

                foreach (var items in abilities.ToList())
                {
                    var data = abilities.Select(x => x.Data.Name == items.Data.Name)
                        .Count() == 1
                        ? "<b>*</b>"
                        : $"<b><color=#ffd230>{items.Data.Name}개</color></b>";

                    if (builder.MaxCapacity >= 1000)
                    {
                        builder.Append($"...(그 외 {abilities.Count}개)");
                        break;
                    }

                    abilities.RemoveAll(x => x.Data.Name == items.Data.Name);

                    builder.Append($"<b>| {items.Data.Name,-13} " +
                                   $"| {data,-8} " +
                                   $"| <color={items.Data.Category.GetColor()}>{items.Data.Category.GetTranslation(),-8}</color> |</b>\n");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"{e}");
            response = "명령어 처리 중 오류가 발생하였습니다.";
            return false;
        }
    }


    public string Command => "getability";
    public string[] Aliases => ["ga", "ability"];
    public string Description => "워크스테이션 업그레이드 | 특정 유저의 능력을 가져옵니다.";
}