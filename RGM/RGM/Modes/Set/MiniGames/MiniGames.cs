using System.Collections.Generic;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using RGM.API.Features;
using UnityEngine;

namespace RGM.Modes
{
    [Mode(ModeCategory.Private, ModeInfo.Set, ModeType.MiniGames)]
    class MiniGames : Mode
    {
        public override string Name => "미니게임";
        public override string Description => "간단한 게임들을 즐겨보세요!";
        public override string Detail =>
"""
10개 이상의 미니게임들이 준비되어 있습니다.

총 3개 라운드로 구성되어 있습니다. (이 모드에서는 EXP, 랜덤코인 지급 안됨)
""";
        public override string Color => "A4A4A4";
        public override string Map => "ru";

        public static MiniGames Instance;

        int RoundCount = 0;
        List<string> Games = new List<string>()
        {
            //"jail", - 뭔 게임인지 모름
            //"airstrike", - 순수 노잼
            //"dm", - 모드로 이미 존재함
            //"escape", - 버그 있음
            "battle",
            "versus",
            "cs",
            "glass",
            "deathrun",
            //"line", - 모드로 이미 존재함
            "dodge",
            "fall",
            "football",
            //"gungame", - 겁나 오래 걸림
            "knives",
            "chair",
            "puzzle",
            "race",
            "light",
            "spleef",
            "tag",
            //"tdm", - 모드로 이미 존재함
            "lava",
            "zombie",
            "zombie2",
            //"amongus" - 24인 이하 제한 걸려 있어서 제외
        };
        Vector3 pos = new Vector3(14.07672f, 328.4382f, 11.39262f);

        CoroutineHandle _onModeStarted;

        public override void OnEnabled()
        {
            Round.IsLocked = true;
            Exiled.API.Features.Map.IsDecontaminationEnabled = false;

            _onModeStarted = Timing.RunCoroutine(OnModeStarted());
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_onModeStarted);
        }

        public IEnumerator<float> OnModeStarted()
        {
            Respawn.PauseWaves();

            foreach (var player in PlayerManager.List)
            {
                player.Role.Set(PlayerRoles.RoleTypeId.ClassD);
                player.Position = pos;
            }

            yield return Timing.WaitForSeconds(10f);

            while (RoundCount < 3)
            {
                bool end = true;

                Server.ExecuteCommand($"/ev run {Games.GetRandomValue()}");

                while (end)
                {
                    foreach (var player in PlayerManager.List)
                    {
                        if (!Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 10, (LayerMask)1) ||
                            hit.transform.name != "classname=brush.003") continue;
                        end = false;
                        break;
                    }

                    yield return Timing.WaitForSeconds(1f);
                }

                RoundCount += 1;

                foreach (var player in PlayerManager.List)
                    player.Position = pos;

                if (RoundCount != 3)
                {
                    for (int i = 1; i < 10; i++)
                    {
                        MultiBroadcast.API.MultiBroadcast.AddMapBroadcast(1, $"<b><color=red>{10 - i}</color>초 후 <color=yellow>{RoundCount + 1}번째 라운드</color>가 시작됩니다.</b>");

                        yield return Timing.WaitForSeconds(1f);
                    }
                }
            }

            Round.IsLocked = false;
        }
    }
}