using System;
using System.Text.RegularExpressions;
using CommandSystem;
using Exiled.API.Features;

namespace RGM.Modes;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class SetCount : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        try
        {
            if (!Round.IsStarted)
            {
                response = "라운드 시작 전 사용할 수 없습니다.";
                return false;
            }

            if (!SpeedStore.IsEnabled)
            {
                response = "해당 모드가 활성화되지 않았습니다. 활성화된 상태에서 다시 시도하세요.";
                return false;
            }

            if (arguments.Count <= 0)
            {
                response = SpeedStore.Count <= 0 ? "그 누구도 죽지 않았습니다." :
                    SpeedStore.Count >= 125 ? "이 시체더미의 제단 속에서 당신의 속도는 한계까지 빨라집니다." :
                    $"{SpeedStore.Count}번의 죽음만큼 속도가 빨라집니다.";
                PlayerFeatures.AddEffects();
                return true;
            }

            if (arguments.At(0).ToLower().Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                PlayerFeatures.UnloadEffects();
                SpeedStore.Clear();
                response = "해당 명령이 성공적으로 처리되었습니다.";
                return true;
            }

            if (arguments.At(0).ToLower().Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                response = """ 
                           /setcount 명령어 by babycat_official
                           add <숫자> - 현재 횟수에 숫자를 추가합니다.
                           set <숫자> - 현재 횟수를 숫자로 설정합니다.
                           remove (또는 rm) <숫자> - 현재 횟수에서 숫자를 제거합니다.
                           clear - 현재 횟수를 0으로 설정합니다.
                           help - 도움말을 표시합니다.
                           """;
                return true;
            }

            if (!Regex.IsMatch(arguments.At(0).ToLower(), "^(add|set|remove|rm)+$"))
            {
                response = """ 
                           알 수 없는 명령어입니다.
                           add <숫자> - 현재 횟수에 숫자를 추가합니다.
                           set <숫자> - 현재 횟수를 숫자로 설정합니다.
                           remove (또는 rm) <숫자> - 현재 횟수에서 숫자를 제거합니다.
                           clear - 현재 횟수를 0으로 설정합니다.
                           help - 도움말을 표시합니다.
                           """;
                return false;
            }

            if (arguments.Count < 2)
            {
                response = "숫자를 입력해주세요.";
                return false;
            }

            var data = byte.TryParse(arguments.At(1), out var value);
            
            if (data && value <= 125)
            {
                switch (arguments.At(0).ToLower())
                {
                    case "add":
                        if (SpeedStore.Count - value < 0)
                        {
                            response = $"현재 횟수({SpeedStore.Count}) 보다 작거나 같은 값을 입력해주세요.";
                            return false;
                        }
                        
                        SpeedStore.Count = (byte)(value + SpeedStore.Count) > 125 ? 
                            (byte)125 : 
                            (byte)(SpeedStore.Count + value);
                        break;
                    case "set":
                        SpeedStore.Count = value;
                        break;
                    case "remove":
                    case "rm":
                        PlayerFeatures.UnloadEffects();
                        var results = SpeedStore.TryRemove(value, out response);
                        if (!results)
                            return false;
                        break;
                    default:
                        throw new ArgumentException($"알 수 없는 명령어입니다. 해당 로직에 들어온 값은 {arguments.At(0)}입니다.");
                }

                PlayerFeatures.AddEffects();
            }
            else
            {
                response = """
                           수가 알맞지 않거나, 너무 큽니다.
                           0 ~ 125 사이의 수이면서 문자가 들어가지 않은 수를 입력해주세요.
                           """;
                return false;
            }

            response = "해당 명령이 성공적으로 처리되었습니다.";
            return true;
        }
        catch (Exception ex)
        {
            response = "해당 명령을 처리하는 도중 오류가 발생했습니다. 해당 오류는 로그에 기록됩니다.";
            Log.Error(ex);
            return false;
        }
    }

    public string Command => "setcount";

    public string[] Aliases { get; } =
    [
        "count",
        "setc"
    ];

    public string Description =>
        """
        모드 "한국인이 좋아하는 속도"의 카운트를 설정합니다.
        추가: /setcount add <값>
        설정: /setcount set <값>
        제거: /setcount remove <값> 또는 /setcount rm <값>
        초기화: /setcount clear 
        (0 ~ 255 사이로 설정해주세요.)
        """;
}