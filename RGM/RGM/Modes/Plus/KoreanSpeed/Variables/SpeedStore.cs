using System.Collections.Generic;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace RGM.Modes;

public static class SpeedStore
{
    /// <summary>
    /// 모드 "한국인이 좋아하는 속도"에서 속도 관련 계산을 처리할 때 사용합니다.
    /// </summary>
    public static byte Count { get; set; }

    /// <summary>
    /// SCP의 쿨타임 감소 관련에 사용되는 상수입니다.
    /// </summary>
    public const float ScpMultiplier = 0.1f;

    /// <summary>
    /// SCP-049의 GoodSense 활성화 여부입니다.
    /// </summary>
    public static HashSet<Player> CurrentSensePlayers { get; } = [];

    /// <summary>
    /// 모드의 활성화 유무를 따질 때 사용합니다.
    /// </summary>
    public static bool IsEnabled { get; private set; }
    
    /// <summary>
    /// Count를 초기화합니다.
    /// </summary>
    public static void Clear() => Count = 0;

    /// <summary>
    /// SpeedStore내 변수를 초기화합니다. (활성화)
    /// </summary>
    public static void Activate()
    {
        Count = 0;
        IsEnabled = true;
    }

    /// <summary>
    /// SpeedStore내 변수를 초기화합니다. (비활성화)
    /// </summary>
    public static void Disable()
    {
        Count = 0;
        IsEnabled = false;
    }

    /// <summary>
    /// Count에서 특정 값만큼 제거합니다.
    /// </summary>
    /// <param name="value">빼려는 값입니다.</param>
    /// <param name="message">Message입니다. 오직 이 값을 저장할려는 변수에 넣으세요.</param>
    /// <returns>성공 시 true를, 실패 시 false를 반환합니다.</returns>
    public static bool TryRemove(byte value,
        out string message)
    {
            if (Count - value < 0)
            {
                message = $"값이 너무 큽니다. 현재 횟수 {Count}보다 작거나 같게 하세요.";
                return false;
            }

            Count -= value;
            message = "해당 삭제 명령이 성공적으로 처리되었습니다.";
            PlayerFeatures.AddEffects();
            return true;
    }
    
    /// <summary>
    /// Sin 값을 반환합니다.
    /// </summary>
    /// <returns>Sin값을 반환합니다.</returns>
    public static float Sin(float div = 2f) => Mathf.Abs(Mathf.Sin(Count / (Mathf.Approximately(div, 0) ? 2 : div)));

    /// <summary>
    /// Cos 값을 반환합니다.
    /// </summary>
    /// <returns>Cos값을 반환합니다.</returns>
    public static float Cos(float div = 2f) => Mathf.Abs(Mathf.Cos(Count / (Mathf.Approximately(div, 0) ? 2 : div)));
}