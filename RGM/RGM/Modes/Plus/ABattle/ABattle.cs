using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommandSystem;
using DiscordInteraction.Discord;
using Exiled.API.Extensions;
using Exiled.API.Features;
using InventorySystem.Items.Firearms.Attachments;
using MapGeneration.Holidays;
using MEC;

using PlayerRoles;
using RemoteAdmin;
using RGM.API.Features;
using RGM.Modes.Commands;
using RGM.Modes.Plus.ABattle;
using UserSettings.ServerSpecific;
using static RGM.Variables.Variable;
using Random = UnityEngine.Random;

namespace RGM.Modes;

[Mode(ModeCategory.Public, ModeInfo.Plus, ModeType.ABattle)]  
public class ABattle : Mode
{
    public override string Name => "워크스테이션 업그레이드";
    public override string Description => "워크스테이션에서 업그레이드하세요!";
    public override string Detail =>
"""
<color=#F5DA81>인간 진영</color>일 경우, 워크스테이션에서 점프하면 능력을 1개 얻습니다.
<color=red>SCP-079</color>일 경우, 레벨이 올라갈 때마다 능력을 1개 얻습니다.

각 능력 등급들의 확률을 확인하려면 아래를 참고하십시오.
• <color=#A4A4A4>일반</color> - 70%
• <color=#2ECCFA>희귀</color> - 24.7%
• <color=#FF00FF>영웅</color> - 5.05%
• <color=#ffd700>전설</color> - 0.2%
• <color=#DF0101>신화</color> - 0.05%
• <color=#F7819F>전용</color> - 5% (능력 선택 옵션 독립)
• <color=#DEEFED>시너지</color> - ???

66.6% 확률로 추가 모드가 활성화됩니다.
워크스테이션이 시설에 더 추가됩니다.

<size=25><b>모드 전용 명령어</b></size>
<size=20>.(번호) - 1번부터 5번까지 있습니다. 능력을 선택할 때 사용됩니다.</size>
<size=20>.추가모드 - 현재 워크스테이션 업그레이드 모드의 추가 모드를 확인합니다.</size>
""";
    public override string Color => "00FFFF";
    public override string Map => "ABattle";

    public override string Author => "GoldenPig1205, RGM Contributors :D";

    public static ABattle Instance;

    // 동기화 객체
    private readonly object _selectionLock = new object();
    private readonly object _cursorLock = new object();

    public Dictionary<Player, List<WorkstationController>> PlayerWorkstations = new Dictionary<Player, List<WorkstationController>>();
    public Dictionary<AbilityType, AbilityData> Abilities = new Dictionary<AbilityType, AbilityData>();
    public Dictionary<AbilityType, List<AbilityType>> SynergyAbilities = new Dictionary<AbilityType, List<AbilityType>>();
    public Dictionary<Player, List<Ability>> PlayerAbilities = new Dictionary<Player, List<Ability>>();
    public Dictionary<Player, List<AbilityType>> Selections = new Dictionary<Player, List<AbilityType>>();
    public Dictionary<Player, bool> IsSelecting = new Dictionary<Player, bool>();
    public Dictionary<Player, int> SelectionCursor = new Dictionary<Player, int>();
    public Dictionary<Player, bool> IsLifeUsed = new Dictionary<Player, bool>();

    private ABattleEventHandler _eventHandler;

    public static Dictionary<string, string> RatingColor = new Dictionary<string, string>()
    {
        {"일반", "#A4A4A4"},
        {"희귀", "#2ECCFA"},
        {"영웅", "#FF00FF"},
        {"전설", "#ffd700"},
        {"신화", "#DF0101"},
        {"전용", "#F7819F"},
        {"시너지", "#DEEFED"}
    };
    public static Dictionary<string, string> SelectFormat = new Dictionary<string, string>()
    {
        {"일반", "<b><color=#404040>일반</color></b>"},
        {"희귀", "<b><color=#47DAFF>희귀</color></b>"},
        {"영웅", "<b><color=#F185FF>영웅</color></b>"},
        {"전설", "<b><color=#FFF70A>전설</color></b>"},
        {"신화", "<b><color=#F52500>신화</color></b>"},
        {"알 수 없음", "<b><color=#000000>알수없음</b>"}
    };
    public static Dictionary<string, string> ExtraModes = new Dictionary<string, string>()
    {
        {"기본", "워크스테이션 업그레이드를 즐기세요!"},
        {"1 + 1", "능력 선택창에 등장하는 능력의 수가 1개인 대신, 동일한 등급의 능력을 1개를 더 받습니다."},
        {"수저", "능력 선택창에서 등장하는 능력의 수가 최대 5개까지 늘어날 수 있습니다."},
        //{"골드 전주곡", $"스폰 즉시 <color={RatingColor["영웅"]}>영웅</color> 등급의 능력을 얻습니다."},
        //{"프리즘 전주곡", $"스폰 즉시 <color={RatingColor["영웅"]}>영웅</color> 등급의 능력을 얻습니다. 낮은 확률로 <color={RatingColor["전설"]}>전설</color>, <color={RatingColor["신화"]}>신화</color> 등급의 능력이 지급될 수 있습니다."},
        {"잔칫상", $"<color={RatingColor["희귀"]}>희귀</color> 이상 등급의 능력이 등장할 확률이 높아집니다."},
        {"스펙업", "능력을 획득하면 추가 최대 체력이 지급됩니다. (+10 (SCP의 경우 +50))"},
        {"캐시 청소", "9분마다 모든 유저의 워크스테이션 획득 기록이 초기화됩니다."},
        {"대출", "워크스테이션 제한이 해제됩니다. 각 워크스테이션마다 처음 1회를 제외하고 추가로 얻으려고 시도하는 경우, 20% 확률로 아사합니다."},
        {"지원", "1~3분마다 모두에게 능력 선택창이 열립니다."},
        {"난장판", "두가지의 추가 모드(난장판 포함)가 적용되며, 관리자의 제약이 모두 풀립니다."}
    };
    public static List<ICommand> DotCommands = new()
    {
        new SelectFirst(),
        new SelectSecond(),
        new SelectThird(),
        new SelectFourth(),
        new SelectFifth(),
        new GetExtraMode(),
        new CASSIE()
    };
    public static List<ICommand> RemoteAdminCommands = new()
    {
        new AddAbility(),
        new AddExtraMode()
    };

    public static string ColorFormat(string text)
    {
        return text.Replace("[시너지]", $"<color={RatingColor["시너지"]}>[시너지]</color>")
                    .Replace("[전용]", $"<color={RatingColor["전용"]}>[전용]</color>")
                    .Replace("[신화]", $"<color={RatingColor["신화"]}>[신화]</color>")
                    .Replace("[전설]", $"<color={RatingColor["전설"]}>[전설]</color>")
                    .Replace("[영웅]", $"<color={RatingColor["영웅"]}>[영웅]</color>")
                    .Replace("[희귀]", $"<color={RatingColor["희귀"]}>[희귀]</color>")
                    .Replace("[일반]", $"<color={RatingColor["일반"]}>[일반]</color>");
    }

    public string PickExtraMode(List<string> exceptModes = null)
    {
        if (exceptModes == null)
        {
            exceptModes = new List<string>();
        }

        if (Random.Range(1, 7) == 1)
        {
            return "기본";
        }   
        else
        {
            string extraMode = Tools.GetRandomValue(ExtraModes.Keys.Where(x => !exceptModes.Contains(x)).ToList());

            if (!CurrentExtraModes.Contains(extraMode))
                CurrentExtraModes.Add(extraMode);

            Webhook.Send($"추가 모드: {extraMode}");
            Log.Info($"추가 모드: {extraMode}");

            if (extraMode == "캐시 청소")
                Timing.RunCoroutine(Instance.ClearCache());

            if (extraMode == "지원")
                Timing.RunCoroutine(Instance.Backup());

            if (extraMode == "난장판")
            {
                for (int i = 0; i < 2; i++)
                    PickExtraMode();
            }

            return extraMode;
        }
    }

    public static List<string> CurrentExtraModes = new();

    CoroutineHandle _onModeStarted;
    CoroutineHandle _hintCoroutine;

    // 플러그인에 있는 모든 능력 검색
    public override void OnEnabled()
    {
        Instance = this;

        PickExtraMode();

        _eventHandler = new ABattleEventHandler(this);
        _eventHandler.RegisterEvents();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var abilityAttribute = type.GetCustomAttribute<AbilityAttribute>();

            if (abilityAttribute == null)
                continue;

            if (!typeof(Ability).IsAssignableFrom(type))
                continue;

            if (abilityAttribute.HolidayType == AbilityHolidayType.Christmas && !HolidayUtils.IsHolidayActive(HolidayType.Christmas))
                continue;

            if (abilityAttribute.HolidayType == AbilityHolidayType.Halloween && !HolidayUtils.IsHolidayActive(HolidayType.Halloween))
                continue;

            Abilities.Add(abilityAttribute.Type, new AbilityData
            {
                Type = type,
                Name = abilityAttribute.Name,
                Description = abilityAttribute.Description,
                Category = abilityAttribute.Category,
                AbilityType = abilityAttribute.Type,
                HolidayType = abilityAttribute.HolidayType,
                Keep = abilityAttribute.Keep
            });

            var requiresAbilityAttribute = type.GetCustomAttribute<RequiresAbilityAttribute>();

            if (requiresAbilityAttribute != null && requiresAbilityAttribute.Abilities.Length > 0)
            {
                SynergyAbilities.Add(abilityAttribute.Type, requiresAbilityAttribute.Abilities.ToList());

                Abilities[abilityAttribute.Type].Category = AbilityCategory.Synergy;
                Abilities[abilityAttribute.Type].Requires = requiresAbilityAttribute.Abilities.ToList();
            }
        }

        foreach (var dot in DotCommands)
            QueryProcessor.DotCommandHandler.RegisterCommand(dot);

        foreach (var ra in RemoteAdminCommands)
            CommandProcessor.RemoteAdminCommandHandler.RegisterCommand(ra);

        _onModeStarted = Timing.RunCoroutine(OnModeStarted());
        _hintCoroutine = Timing.RunCoroutine(HintCoroutine());

        ServerSpecificSettingsSync.ServerOnSettingValueReceived += ABattleSetting.OnSSInput;
    }

    public override void OnDisabled()
    {
        _eventHandler.UnregisterEvents();

        CurrentExtraModes.Clear();

        foreach (var dot in DotCommands)
        {
            if (QueryProcessor.DotCommandHandler.TryGetCommand(dot.Command, out ICommand command))
                QueryProcessor.DotCommandHandler.UnregisterCommand(command);
        }

        foreach (var ra in RemoteAdminCommands)
        {
            if (CommandProcessor.RemoteAdminCommandHandler.TryGetCommand(ra.Command, out ICommand command))
                CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(command);
        }

        foreach (var player in Player.List)
        {
            foreach (var ability in GetAbilities(player))
            {
                ability.OnDisabled();
            }
        }

        Timing.KillCoroutines(_onModeStarted);
        Timing.KillCoroutines(_hintCoroutine);

        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= ABattleSetting.OnSSInput;
    }

    private IEnumerator<float> OnModeStarted()
    {
        yield return Timing.WaitForOneFrame;

        foreach (var player in PlayerManager.List)
        {
            try
            {
                ExtraModeNotion(player);
                ApplyPrelude(player);
            }
            catch (Exception e)
            {
                Log.Error($"An error occurred while trying to add <b><i>{player.Nickname}</i></b> to the dictionary: {e}");
            }
        }

        yield break;
    }

    private IEnumerator<float> HintCoroutine()
    {
        while (true)
        {
            foreach (var player in Player.List.Where(x => !x.IsNPC))
            {
                var CurrentHint = player.CurrentHint;
                var isStatusHint = CurrentHint != null && (CurrentHint.Content.Contains("워크스테이션") || CurrentHint.Content.Contains("보유 업그레이드"));

                if (player.IsAlive)
                    player.AddHint("워크스테이션 힌트", FormatHint(player), 1.2f);
            }

            yield return Timing.WaitForOneFrame;
        }
    }

    private IEnumerator<float> ClearCache()
    {
        while (true)
        {
            foreach (var player in PlayerWorkstations.Keys)
            {
                PlayerWorkstations[player].Clear();

                if (player != null && player.IsConnected)
                    player.AddBroadcast(10, $"<b><size=20>캐시 청소가 완료되었습니다. 이전에 방문한 워크스테이션에서 능력을 다시 얻을 수 있습니다.</size></b>");
            }

            yield return Timing.WaitForSeconds(60 * 9);
        }
    }

    private IEnumerator<float> Backup()
    {
        while (true)
        {
            foreach (var player in PlayerManager.List.Where(x => !x.IsNPC && x.IsAlive && PlayerManager.List.Contains(x)))
            {
                StartSelect(player);
            }

            yield return Timing.WaitForSeconds(Random.Range(1, 4) * 60);
        }
    }

    private string FormatHint(Player player)
    {
        if (!PlayerAbilities.TryGetValue(player, out var ability))
        {
            return player.Role.Type == RoleTypeId.Scp079
                ? "<align=left><b><size=22>워크스테이션 상단을 핑으로 찍으면 능력을 획득할 수 있습니다.</size></b></align>"
                : "<align=left><b><size=22>워크스테이션 위에서 점프하면 능력을 획득할 수 있습니다.</size></b></align>";
        }

        if (!ability.Any())
        {
            return player.Role.Type == RoleTypeId.Scp079
                ? "<align=left><b><size=22>워크스테이션 상단을 핑으로 찍으면 능력을 획득할 수 있습니다.</size></b></align>"
                : "<align=left><b><size=22>워크스테이션 위에서 점프하면 능력을 획득할 수 있습니다.</size></b></align>";
        }

        var abilitiesText = string.Join(", ",
            PlayerAbilities[player]
                .GroupBy(x => x.Data.AbilityType)
                .Select(g => g.Count() > 1
                    ? $"{g.First().Data.GetFormattedName()} x{g.Count()}"
                    : g.First().Data.GetFormattedName())
                .ToList());

        return $"<align=left><b><size=25>보유 업그레이드</size></b>\n<size=20>{abilitiesText}</size>\n</align>";
    }

    public IEnumerator<float> RestoreAbilities(List<Player> players)
    {
        foreach (var player in players)
        {
            List<AbilityType> _abilities = PlayerAbilities[player].Select(x => x.Data.AbilityType).ToList();

            Reset(player);

            yield return Timing.WaitForOneFrame;

            foreach (var ability in _abilities)
                player.AddAbility(ability);

            yield return Timing.WaitForOneFrame;

            player.AddBroadcast(10, $"<size=25><b>모든 능력을 제거한 후, 수복하였습니다.</b></size>");
        }
    }

    public void ExtraModeNotion(Player player, bool enableBroadcast = true)
    {
        foreach (var cem in CurrentExtraModes)
        {
            string extraMode = $"<size=25><b><color=#fecdcd>{cem}</color></b></size>\n<size=20>{ExtraModes[cem]}</size>";

            if (enableBroadcast)
                player.AddBroadcast(10, extraMode);

            player.SendConsoleMessage("\n" + extraMode, "white");
        }
    }

    // 플레이어에게 특정 능력을 부여
    public void AddAbility(Player player, AbilityType type)
    {
        if (type.ToString().Contains("LEGEND"))
        {
            string name;

            switch (type) 
            {
                case AbilityType.LEGEND_LAVACHICKEN: name = "LavaChicken"; break;
                default: name = "누군가가 전설 능력을 획득하였습니다"; break;
            }

            if (GlobalPlayer.ClipsById.Where(x => x.Value.Clip == name).Count() < 1)
                Tools.PlayGlobalAudio(name, 1.5f);
        }
        else if (type.ToString().Contains("MYTHIC"))
        {
            string name;

            switch (type)
            {
                case AbilityType.MYTHIC_KINGSCOLOR: name = "시산혈해의 파도가 보인다"; break;
                default: name = "누군가가 신화 능력을 영접하였습니다"; break;
            }

            if (GlobalPlayer.ClipsById.Where(x => x.Value.Clip == name).Count() < 1)
                Tools.PlayGlobalAudio(name, 2.5f);
        }

        if (player.HasAbility(AbilityType.LEGEND_REFLECTOR))
        {
            if (Random.Range(1, 4) == 1)
                player.AddAbility(type);
        }

        Log.Info("AddAbility called with " + player.Nickname + " and " + type);

        if (!Abilities.ContainsKey(type))
        {
            Log.Error($"Ability {type} not found.");

            return;
        }

        if (!PlayerAbilities.ContainsKey(player))
        {
            Log.Info("No key");
            PlayerAbilities.Add(player, []);
        }

        var abilityData = Abilities[type];
        Ability ability;

        try
        {
             ability = Activator.CreateInstance(Abilities[type].Type) as Ability;
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while trying to create an instance of {abilityData.Name}: {e}");
            return;
        }

        if (ability == null)
        {
            Log.Error($"An error occurred while trying to create an instance of {abilityData.Name}. The instance is null.");
            return;
        }

        ability.Data = abilityData;
        ability.Owner = player;
        ability.OnEnabled();

        PlayerAbilities[player].Add(ability);
        EnableSynergyAbility(player);

        string styleName = ColorFormat(abilityData.GetFormattedName());

        string Message = $"<size=20>{styleName}</size>\n<size=15>{abilityData.Description}</size>";
        player.AddBroadcast(10, Message);
        player.SendConsoleMessage($"\n{Message}", "white");

        if (CurrentExtraModes.Contains("스펙업"))
        {
            int heal = player.IsScpRole() ? 50 : 10;
            player.MaxHealth += heal;
            player.Health += heal;
        }
    }

    // 플레이어에게 시너지 능력 부여
    private void EnableSynergyAbility(Player player)
    {
        List<Ability> abilities = PlayerAbilities[player];

        foreach (var synergy in SynergyAbilities)
        {
            // 시너지 능력의 요구사항이 중복을 포함할 수 있도록, 각 요구 능력의 개수를 세서 비교
            bool hasAllRequired = true;
            foreach (var req in synergy.Value.GroupBy(x => x))
            {
                int requiredCount = req.Count();
                int playerCount = abilities.Count(a => a.Data.AbilityType == req.Key);
                if (playerCount < requiredCount)
                {
                    hasAllRequired = false;
                    break;
                }
            }

            if (!hasAllRequired)
                continue;

            if (!Abilities.TryGetValue(synergy.Key, out var synergyAbilityType))
                continue;

            if (abilities.Any(a => a.Data.AbilityType == synergy.Key))
                continue;

            player.AddAbility(synergyAbilityType.AbilityType);
        }
    }

    // 플레이어로부터 특정 능력 제거
    public void RemoveAbility(Player player, AbilityType type)
    {
        if (!PlayerAbilities.TryGetValue(player, out var playerAbility))
            return;

        var ability = playerAbility.FirstOrDefault(x => x.Data.AbilityType == type);

        if (ability == null)
            return;

        ability.OnDisabled();
        PlayerAbilities[player].Remove(ability);

        RemoveSynergyAbility(PlayerAbilities[player]);
    }

    public void RemoveAbility(Player player, Ability ability)
    {
        if (ability == null)
            return;

        if (!PlayerAbilities.TryGetValue(player, out var playerAbility))
            return;

        if (!playerAbility.Contains(ability))
            return;

        ability.OnDisabled();
        PlayerAbilities[player].Remove(ability);

        RemoveSynergyAbility(PlayerAbilities[player]);
    }

    // 플레이어로부터 시너지 능력 확인 후 제거
    private void RemoveSynergyAbility(List<Ability> abilities)
    {
        foreach (var synergy in SynergyAbilities)
        {
            if (!synergy.Value.All(req => abilities.Any(a => a.Data.AbilityType == req)) &&
                abilities.Any(a => a.Data.AbilityType == synergy.Key))
            {
                var ability = abilities.First(a => a.Data.AbilityType == synergy.Key);
                ability.OnDisabled();
                abilities.Remove(ability);
            }
        }
    }

    // 플레이어로부터 모든 능력 제거
    public void RemoveAllAbilities(Player player)
    {
        if (!PlayerAbilities.TryGetValue(player, out var playerAbility))
            return;

        foreach (var ability in playerAbility)
            ability.OnDisabled();

        PlayerAbilities.Remove(player);
    }

    // 플레이어의 모든 능력 가져오기
    public List<Ability> GetAbilities(Player player)
    {
        return PlayerAbilities.TryGetValue(player, out var playerAbility) ? playerAbility : new List<Ability>();
    }

    public AbilityType FindAbility(string name)
    {
        return Abilities.FirstOrDefault(x => x.Value.Name == name).Key;
    }

    // 플레이어의 특정 능력 가져오기

    public Ability GetAbility(Player player, AbilityType type)
    {
        return GetAbilities(player).FirstOrDefault(x => x.Data.AbilityType == type);
    }

    // 플레이어의 모든 능력 가져오기
    public List<Ability> GetAbility(Player player)
    {
        return GetAbilities(player);
    }

    // 플레이어가 특정 능력을 가지고 있는지 확인
    public bool HasAbility(Player player, AbilityType type)
    {
        return GetAbility(player, type) != null;
    }

    public List<AbilityType> GetRandomAbilities(Player player, AbilityCategory category, int count)
    {
        var abilities = Abilities
            .Where(x => x.Value.Category == category)
            .Where(x =>
            {
                var conditionAttr = x.Value.Type.GetCustomAttribute<ConditionAbilityAttribute>();
                if (conditionAttr == null)
                    return true;

                return conditionAttr.Abilities.All(req => player.HasAbility(req));
            })
            .ToList();

        if (category == AbilityCategory.Dummy)
            abilities = Abilities.ToList();

        abilities.ShuffleList();

        var result = new List<AbilityType>();
        for (int i = 0; i < count; i++)
        {
            var picked = abilities.GetRandomValue().Key;
            result.Add(picked);
        }
        return result;
    }

    public void StartSelect(Player player, List<AbilityType> abilities = null, int count = 3)
    {
        if (CurrentExtraModes.Contains("1 + 1"))
        {
            count = 1;
        }    
        else if (CurrentExtraModes.Contains("수저"))
        {
            switch (Random.Range(1, 4))
            {
                case 1:
                    count = 5;
                    break;
                case 2:
                    count = 4;
                    break;
                case 3:
                    count = 3;
                    break;
            }
        }

        if (!Selections.ContainsKey(player))
            Selections.Add(player, new List<AbilityType>());

        lock (_selectionLock)
        {
            IsSelecting[player] = true;
        }

        var category = GetCategory(player);

        if (category == AbilityCategory.Dummy)
            return;

        if (CurrentExtraModes.Contains("1 + 1"))
        {
            player.AddAbility(GetRandomAbilities(player, category, 1).First());
        }

        abilities = abilities == null ? GetRandomAbilities(player, category, count) : abilities;
        var ignoredIndexes = new List<int>();

        if (abilities.Count == 0)
            return;

        if (abilities.Distinct().Count() == 1 && abilities.Count() > 2) // 능력 선택창에 등장한 능력이 최소 3개 이상이고, 전부 중복인 경우
        {
            player.AddAbility(AbilityType.SYNERGY_DUPLICATEFATE);

            foreach (var ability in abilities)
            {
                player.AddAbility(ability);
            }
        }

        if (Random.Range(1, 21) == 1) // 전용 능력
        {
            int index;

            do
            {
                index = Random.Range(0, 3);
            } while (ignoredIndexes.Contains(index));

            ignoredIndexes.Add(index);

            var ability = GetRandomAbilities(player, player.HasAbility(AbilityType.SYNERGY_BLACKMARKET) ? Tools.EnumToList<AbilityCategory>().GetRandomValue(x => !new List<AbilityCategory> { AbilityCategory.None, AbilityCategory.Dummy, AbilityCategory.Synergy }.Contains(x)) : player.GetAbilityCategory(), 1).First();

            abilities[index] = ability;
        }

        if (player.HasAbility(AbilityType.RARE_TRANSITION))
        {
            player.RemoveAbility(AbilityType.RARE_TRANSITION);

            var transition = Random.Range(1, 5) == 1;

            if (transition)
            {
                int index;

                do
                {
                    index = Random.Range(0, 3);
                } while (ignoredIndexes.Contains(index));

                ignoredIndexes.Add(index);

                for (int i = 0; i < 3; i++)
                {
                    abilities[i] = GetRandomAbilities(player, AbilityCategory.Epic, 1).First();
                }

                player.AddAbility(AbilityType.DUMMY_RARETRANSITIONSUCCESS);
            }
            else
                player.AddAbility(AbilityType.DUMMY_RARETRANSITIONFAILURE);
        }

        if (player.HasAbility(AbilityType.EPIC_TRANSITION))
        {
            player.RemoveAbility(AbilityType.EPIC_TRANSITION);

            var transition = Random.Range(1, 5) == 1;

            if (transition)
            {
                int index;

                do
                {
                    index = Random.Range(0, 3);
                } while (ignoredIndexes.Contains(index));

                ignoredIndexes.Add(index);

                for (int i = 0; i < 3; i++)
                {
                    abilities[i] = GetRandomAbilities(player, AbilityCategory.Legend, 1).First();
                }

                player.AddAbility(AbilityType.DUMMY_EPICTRANSITIONSUCCESS);
            }
            else
                player.AddAbility(AbilityType.DUMMY_EPICTRANSITIONFAILURE);
        }

        if (player.HasAbility(AbilityType.LEGEND_TRANSITION))
        {
            player.RemoveAbility(AbilityType.LEGEND_TRANSITION);

            var transition = Random.Range(1, 5) == 1;

            if (transition)
            {
                int index;

                do
                {
                    index = Random.Range(0, 3);
                } while (ignoredIndexes.Contains(index));

                ignoredIndexes.Add(index);

                for (int i = 0; i < 3; i++)
                {
                    abilities[i] = GetRandomAbilities(player, AbilityCategory.Mythic, 1).First();
                }

                player.AddAbility(AbilityType.DUMMY_LEGENDTRANSITIONSUCCESS);
            }
            else
                player.AddAbility(AbilityType.DUMMY_LEGENDTRANSITIONFAILURE);
        }

        lock (_selectionLock)
        {
            Selections[player] = abilities;
            SelectionCursor[player] = 0;
        }

        // 다음 타자, 코루틴!!!
        Timing.RunCoroutine(SelectionCoroutine(player));
    }

    private IEnumerator<float> SelectionCoroutine(Player player)
    {
        bool holidayFormat(AbilityType type, out string result)
        {
            result = "";

            if (Abilities[type].HolidayType == AbilityHolidayType.Halloween)
            {
                result = "<b><color=#FF9500>[</color><color=#FF9F09>H</color><color=#FFA912>A</color><color=#FFB31B>L</color><color=#FFBD24>L</color><color=#FFC72E>O</color><color=#FFDC37>W</color><color=#FFF240>E</color><color=#FFFF49>EE</color><color=#FFFF52>N</color><color=#FFFF5C>]</color></b>";
                return true;
            }

            if (Abilities[type].HolidayType == AbilityHolidayType.Christmas)
            {
                result = "<b><color=#FC0000>[</color><color=#EA1300>C</color><color=#D82600>h</color><color=#C63900>r</color><color=#B44C00>i</color><color=#A25F00>s</color><color=#917200>t</color><color=#7F8500>m</color><color=#6D9800>a</color><color=#5BAB00>s</color><color=#49BE00>]</color></b>";
                return true;
            }

            return false;
        }

        var abilities = Selections[player];

        string BuildSelectionText()
        {
            if (!SelectionCursor.ContainsKey(player))
                SelectionCursor[player] = 0;

            int cursor = SelectionCursor[player];
            if (abilities.Count > 0)
                cursor = Math.Max(0, Math.Min(cursor, abilities.Count - 1));

            return string.Join("\n", abilities.Select((x, i) =>
            {
                string prefix = i == cursor ? "▶ " : "   ";
                return $"{prefix}[{i + 1}] {x.GetTranslation()}\n<size=20>{(holidayFormat(x, out string result) ? $"{result} " : "")}{Abilities[x].Description}</size>\n";
            }));
        }

        string CheckAbilityGrade(string text)
        {
            if (text.Contains("일반")) return "일반";
            else if (text.Contains("희귀")) return "희귀";
            else if (text.Contains("영웅")) return "영웅";
            else if (text.Contains("전설")) return "전설";
            else if (text.Contains("신화")) return "신화";

            else return "알 수 없음";
        }

        for (var i = 0; i < 200; i++)
        {
            lock (_selectionLock)
            {
                if (player.IsDead || !Selections.ContainsKey(player))
                {
                    if (Selections.ContainsKey(player))
                        Selections.Remove(player);

                    SelectionCursor.Remove(player);
                    IsSelecting[player] = false;

                    yield break;
                }
            }

            var text = BuildSelectionText();
            player.AddHint("능력 선택",
            $"<align=left><size=40><b>능력 선택창ㅣ{SelectFormat[CheckAbilityGrade(text)]} ({(int)((200 - i) / 10)})</b></size>\n\n<size=30>{text}</size>\n\n<size=25><b>위/아래 키로 선택 후, Enter 키로 확정하세요.</b></size>\n<size=20><color=#bcbcbc><i>[ESC] -> [Settings] -> [Server-specific]</i></color></size></align>\n\n\n\n\n",
            1.2f);

            yield return Timing.WaitForSeconds(0.1f);
        }

        lock (_selectionLock)
        {
            IsSelecting[player] = false;

            if (!Selections.ContainsKey(player))
                yield break;

            var random = Random.Range(0, abilities.Count);

            player.AddAbility(abilities[random]);

            Selections.Remove(player);
            SelectionCursor.Remove(player);
        }
    }

    public AbilityCategory GetCategory(Player player)
    {
        if (!player.IsAlive) return AbilityCategory.Dummy;

        if (player.Role == RoleTypeId.Scp079)
            return AbilityCategory.Scp079;

        var random = Random.Range(1, 10001);

        if (CurrentExtraModes.Contains("잔칫상"))
        {
            switch (random)
            {
                case <= 10:
                    return AbilityCategory.Mythic;
                case <= 50:
                    return AbilityCategory.Legend;
                case <= 550:
                    return AbilityCategory.Epic;
                case <= 2950:
                    return AbilityCategory.Rare;
                default:
                    return AbilityCategory.Common;
            }
        }

        switch (random)
        {
            case <= 5:
                return AbilityCategory.Mythic;
            case <= 25:
                return AbilityCategory.Legend;
            case <= 535:
                return AbilityCategory.Epic;
            case <= 3005:
                return AbilityCategory.Rare;
            default:
                return AbilityCategory.Common;
        }
    }

    public bool Select(Player player, int index, out string response)
    {
        lock (_selectionLock)
        {
            if (!Selections.ContainsKey(player))
            {
                response = "선택할 수 있는 능력이 없습니다.";
                return false;
            }

            Log.Info("Select called with " + player.Nickname + " and " + index);

            AbilityType ability;

            Log.Info("All abilities are not the same");

            ability = Selections[player][index - 1];

            player.AddAbility(ability);

            Selections.Remove(player);
            SelectionCursor.Remove(player);

            player.AddHint("/?/", "", 0.1f);

            response = $"{index}번 능력 선택 완료!";
            return true;
        }
    }

    public void MoveSelectionCursor(Player player, int delta)
    {
        if (!Selections.TryGetValue(player, out var abilities) || abilities.Count == 0)
            return;

        lock (_cursorLock) // 교차 동기화 방지
        {
            if (!SelectionCursor.ContainsKey(player))
                SelectionCursor[player] = 0;

            int cursor = SelectionCursor[player];
            cursor = (cursor + delta) % abilities.Count;

            if (cursor < 0)
                cursor += abilities.Count;

            SelectionCursor[player] = cursor;
        }

        PlayersAudio[player].TryPlay("Select");
    }

    public bool ConfirmSelectionByCursor(Player player, out string response)
    {
        lock (_cursorLock)
        {
            if (!Selections.TryGetValue(player, out var abilities) || abilities.Count == 0)
            {
                response = "선택할 수 있는 능력이 없습니다.";
                return false;
            }

            if (!SelectionCursor.ContainsKey(player))
                SelectionCursor[player] = 0;

            PlayersAudio[player].TryPlay("SelectConfirm", 1.5f);

            int cursor = Math.Max(0, Math.Min(SelectionCursor[player], abilities.Count - 1));
            return Select(player, cursor + 1, out response);
        }
    }

    public void Reset(Player player)
    {
        player.RemoveAllAbilities();

        PlayerWorkstations[player].Clear();
        
        lock (_selectionLock)
        {
            IsSelecting[player] = false;
            SelectionCursor.Remove(player);
        }
        
        IsLifeUsed[player] = false;
    }

    public static void ApplyPrelude(Player player)
    {
        if (CurrentExtraModes.Contains("골드 전주곡"))
        {
            if (player.Role.Type == RoleTypeId.Scp079)
                player.AddAbility(Instance.GetRandomAbilities(player, AbilityCategory.Scp079, 1).First());

            else
                player.AddAbility(Instance.GetRandomAbilities(player, AbilityCategory.Epic, 1).First());
        }
        else if (CurrentExtraModes.Contains("프리즘 전주곡"))
        {
            if (player.Role.Type == RoleTypeId.Scp079)
            {
                for (int i = 0; i < Random.Range(1, 3); i++)
                    player.AddAbility(Instance.GetRandomAbilities(player, AbilityCategory.Scp079, 1).First());
            }

            else
            {
                AbilityCategory getRandom()
                {
                    if (Random.Range(1, 31) == 1)
                        return AbilityCategory.Mythic;

                    if (Random.Range(1, 21) == 1)
                        return AbilityCategory.Legend;

                    return AbilityCategory.Epic;
                }

                player.AddAbility(Instance.GetRandomAbilities(player, getRandom(), 1).First());
            }
        }
    }
}

public static class ABattleExtensions
{
    public static void AddAbility(this Player player, AbilityType type)
    {
        ABattle.Instance.AddAbility(player, type);
    }

    public static void RemoveAbility(this Player player, AbilityType type)
    {
        ABattle.Instance.RemoveAbility(player, type);
    }

    public static void RemoveAbility(this Player player, Ability ability)
    {
        ABattle.Instance.RemoveAbility(player, ability);
    }

    public static void RemoveAllAbilities(this Player player)
    {
        ABattle.Instance.RemoveAllAbilities(player);
    }

    public static List<Ability> GetAbilities(this Player player)
    {
        return ABattle.Instance.GetAbilities(player);
    }

    public static Ability GetAbility(this Player player, AbilityType type) 
        => ABattle.Instance.GetAbility(player, type);

    public static List<Ability> GetAbility(this Player player) 
        => ABattle.Instance.GetAbility(player);

    public static bool HasAbility(this Player player, AbilityType type) 
        => ABattle.Instance.HasAbility(player, type);
}