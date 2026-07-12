using Exiled.API.Features;
using RGM.API.Features;
using System;
using System.Collections.Generic;

namespace RGM.Modes;

/// <summary>
/// Echo 성장 XP 공식.
/// 1→2 기준: Cost4=200, Cost3=140, Cost1=100
/// 이후: y = ceil(1.07x + 15)
/// </summary>
public static class EchoGrowth
{
    public const float LevelExpMultiplier = 1.05f;
    public const float LevelExpAdd = 20f;

    /// <summary>
    /// 같은 프레임/짧은 구간에 GrantExp가 여러 번 호출되어도 ApplyLoadout은 1회만 예약.
    /// 동시에 여러 Echo가 레벨업해도 한 번의 재적용으로 합산됩니다.
    /// </summary>
    static readonly HashSet<Player> PendingApplyLoadout = new();

    public static int GetBaseExp(EchoCost cost)
    {
        return cost switch
        {
            EchoCost.Cost4 => 200,
            EchoCost.Cost3 => 140,
            EchoCost.Cost1 => 100,
            _ => 100
        };
    }

    /// <summary>
    /// currentLevel → currentLevel+1 에 필요한 경험치.
    /// </summary>
    public static int GetRequiredExp(EchoCost cost, int currentLevel)
    {
        currentLevel = EchoStats.Clamp(currentLevel, 1, EchoInfo.MaxLevel);
        if (currentLevel >= EchoInfo.MaxLevel)
            return 0;

        int exp = GetBaseExp(cost);
        // level 1→2 는 base, 그 다음부터 공식 적용
        for (int level = 1; level < currentLevel; level++)
            exp = NextExp(exp);

        return exp;
    }

    public static int NextExp(int previousRequired)
    {
        return (int)Math.Ceiling(LevelExpMultiplier * previousRequired + LevelExpAdd);
    }

    /// <summary>
    /// 장착 중인 모든 Echo에 XP를 지급하고, 레벨업 시 로드아웃/인스턴스를 갱신합니다.
    /// </summary>
    public static void GrantExpToEquipped(Player player, int amount, string reason = null)
    {
        if (player == null || amount <= 0)
            return;

        if (!EchoInfo.PlayerLoadouts.TryGetValue(player, out var loadout))
            return;

        var leveledUp = new List<(EchoType Type, int OldLevel, int NewLevel)>();

        foreach (var type in loadout.GetEquipped())
        {
            int oldLevel = loadout.GetLevel(type);
            if (AddExp(player, loadout, type, amount, out int newLevel) && newLevel > oldLevel)
                leveledUp.Add((type, oldLevel, newLevel));
        }

        if (leveledUp.Count == 0)
            return;

        foreach (var (type, _, newLevel) in leveledUp)
            EchoStats.EnsureSubOptions(loadout, type, newLevel);

        // 다중 Echo 동시 레벨업: 브로드캐스트를 한 번에 묶어 표시
        if (leveledUp.Count == 1)
        {
            var (type, _, newLevel) = leveledUp[0];
            var data = type.GetData();
            string name = data?.Name ?? type.ToString();
            player.AddBroadcast(3, $"<size=25><color=#7CFC00>{name}</color> → Lv.<b>{newLevel}</b></size>");
        }
        else
        {
            var parts = new List<string>(leveledUp.Count);
            foreach (var (type, _, newLevel) in leveledUp)
            {
                var data = type.GetData();
                string name = data?.Name ?? type.ToString();
                parts.Add($"<color=#7CFC00>{name}</color> Lv.<b>{newLevel}</b>");
            }

            player.AddBroadcast(3, $"<size=22>{string.Join(" / ", parts)}</size>");
        }

        ScheduleApplyLoadout(player);
    }

    /// <summary>
    /// ApplyLoadout 예약을 debounce. 이미 대기 중이면 추가 예약하지 않습니다.
    /// </summary>
    static void ScheduleApplyLoadout(Player player)
    {
        if (player == null || !EchoInfo.PlayerEchoes.ContainsKey(player))
            return;

        if (!PendingApplyLoadout.Add(player))
            return;

        // Hurting 이벤트 중 즉시 재적용하면 핸들러 충돌 가능 → 다음 프레임에 1회만 적용
        MEC.Timing.CallDelayed(MEC.Timing.WaitForOneFrame, () =>
        {
            PendingApplyLoadout.Remove(player);

            if (player != null && player.IsAlive && EchoInfo.PlayerLoadouts.ContainsKey(player))
                EchoBattleCore.ApplyLoadout(player);
        });
    }

    public static void ClearPending(Player player)
    {
        if (player != null)
            PendingApplyLoadout.Remove(player);
    }

    /// <summary>
    /// 특정 Echo에 XP 추가. 레벨업 발생 시 true.
    /// </summary>
    public static bool AddExp(Player player, EchoLoadout loadout, EchoType type, int amount, out int newLevel)
    {
        newLevel = loadout.GetLevel(type);
        if (amount <= 0 || newLevel >= EchoInfo.MaxLevel)
            return false;

        var data = type.GetData();
        if (data == null)
            return false;

        if (!loadout.Experience.ContainsKey(type))
            loadout.Experience[type] = 0;

        loadout.Experience[type] += amount;
        bool leveled = false;

        while (newLevel < EchoInfo.MaxLevel)
        {
            int required = GetRequiredExp(data.Cost, newLevel);
            if (required <= 0 || loadout.Experience[type] < required)
                break;

            loadout.Experience[type] -= required;
            newLevel++;
            loadout.Levels[type] = newLevel;
            leveled = true;
        }

        if (newLevel >= EchoInfo.MaxLevel)
            loadout.Experience[type] = 0;

        return leveled;
    }

    public static int GetCurrentExp(EchoLoadout loadout, EchoType type)
    {
        return loadout.Experience.TryGetValue(type, out int exp) ? exp : 0;
    }

    public static string FormatProgress(EchoLoadout loadout, EchoType type)
    {
        int level = loadout.GetLevel(type);
        if (level >= EchoInfo.MaxLevel)
            return "MAX";

        var data = type.GetData();
        if (data == null)
            return "?";

        int current = GetCurrentExp(loadout, type);
        int required = GetRequiredExp(data.Cost, level);
        return $"{current}/{required}";
    }
}
