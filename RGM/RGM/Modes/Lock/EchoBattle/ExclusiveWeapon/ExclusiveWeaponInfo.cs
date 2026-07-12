using Exiled.API.Features;
using System.Collections.Generic;

namespace RGM.Modes;

public static class ExclusiveWeaponInfo
{
    public const int MaxLevel = 90;
    public const int MaxResonance = 5;
    public const int BaseExp = 20;
    public const float LevelExpMultiplier = 1.02f;
    public const float LevelExpAdd = 6f;

    public static Dictionary<ExclusiveWeaponType, ExclusiveWeaponData> Weapons = new();
    public static Dictionary<Player, ExcWeapon> PlayerWeapons = new();
    public static Dictionary<Player, ExclusiveWeaponProgress> PlayerProgress = new();

    public static ExclusiveWeaponProgress GetOrCreateProgress(Player player)
    {
        if (!PlayerProgress.TryGetValue(player, out var progress) || progress == null)
        {
            progress = new ExclusiveWeaponProgress();
            PlayerProgress[player] = progress;
        }

        return progress;
    }

    public static void ClearPlayer(Player player)
    {
        PlayerWeapons.Remove(player);
        PlayerProgress.Remove(player);
    }
}

/// <summary>플레이어별 전용무기 성장/공진 진행도.</summary>
public class ExclusiveWeaponProgress
{
    public Dictionary<ExclusiveWeaponType, int> Levels { get; set; } = new();
    public Dictionary<ExclusiveWeaponType, int> Experience { get; set; } = new();
    public Dictionary<ExclusiveWeaponType, int> Resonance { get; set; } = new();
    public Dictionary<ExclusiveWeaponType, ResonanceQuestState> Quests { get; set; } = new();

    public int GetLevel(ExclusiveWeaponType type)
    {
        if (Levels.TryGetValue(type, out int level))
            return ExclusiveWeaponStats.Clamp(level, 1, ExclusiveWeaponInfo.MaxLevel);
        return 1;
    }

    public int GetResonance(ExclusiveWeaponType type)
    {
        if (Resonance.TryGetValue(type, out int res))
            return ExclusiveWeaponStats.Clamp(res, 1, ExclusiveWeaponInfo.MaxResonance);
        return 1;
    }

    public int GetExperience(ExclusiveWeaponType type)
    {
        return Experience.TryGetValue(type, out int exp) ? exp : 0;
    }

    public ResonanceQuestState GetOrCreateQuest(ExclusiveWeaponType type)
    {
        if (!Quests.TryGetValue(type, out var state) || state == null)
        {
            state = new ResonanceQuestState();
            Quests[type] = state;
        }

        return state;
    }
}

/// <summary>
/// 공진 승급용 1회성 퀘스트 진행도.
/// 생존 시간은 사망해도 누적됩니다.
/// </summary>
public class ResonanceQuestState
{
    public int Kills;
    public float SurviveSeconds;
    public float DamageDealt;
    public float DamageTaken;
    public float HealingDone;
    public float HsRecovered;

    /// <summary>공진 1→2 ~ 4→5 에 대응하는 4개 퀘스트 완료(청구) 여부.</summary>
    public bool[] Claimed { get; set; } = new bool[4];
}
