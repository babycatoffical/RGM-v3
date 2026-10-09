using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Scp173;
using LabApi.Events.Arguments.Scp049Events;
using MEC;
using PlayerRoles;
using RGM.API.Features;
using UnityEngine;
using Scp049Role = Exiled.API.Features.Roles.Scp049Role;

namespace RGM.Modes;

public class ScpFeatures
{
    private const float WaitTime = .2f;
    private static bool _isRunning;

    private static CoroutineHandle _isRunning079;
    private static CoroutineHandle _isRunning173;

    /// <summary>
    /// 해당 로직이 가동 시 발동됩니다.
    /// </summary>
    public static event EventHandler Start;

    public void OnEnabled()
    {
        Timing.RunCoroutine(RegisterFeatures());
        Exiled.Events.Handlers.Scp173.Blinking += On173Blink;
        LabApi.Events.Handlers.Scp049Events.UsingSense += On049SenseUsing;
        LabApi.Events.Handlers.Scp049Events.SenseKilledTarget += On049SenseKilled;
        LabApi.Events.Handlers.Scp049Events.SenseLostTarget += On049SenseLost;
    }

    public void OnDisabled()
    {
        Exiled.Events.Handlers.Scp173.Blinking -= On173Blink;
        LabApi.Events.Handlers.Scp049Events.UsingSense -= On049SenseUsing;
        LabApi.Events.Handlers.Scp049Events.SenseKilledTarget -= On049SenseKilled;
        LabApi.Events.Handlers.Scp049Events.SenseLostTarget -= On049SenseLost;
        _isRunning = false;
        
        Timing.KillCoroutines(_isRunning079);
        Timing.KillCoroutines(_isRunning173);
    }

    private static IEnumerator<float> RegisterFeatures()
    {
        while (SpeedStore.IsEnabled)
        {
            if (!_isRunning)
            {
                yield return Timing.WaitForSeconds(30.5f);
                Tools.MessageTranslated(
                    "SCP move system has been Activated",
                    "SCP 움직임 시스템이 가동되었습니다.",
                    true);
                _isRunning = true;
                Start?.Invoke(nameof(RegisterFeatures), null);
            }
            else
            {
                RunEffect();
                Scp079Effect();
                Scp173Effect();
                yield return Timing.WaitForSeconds(SpeedStore.Sin(WaitTime));
            }
        }
    }

    // NOTE: 대부분의 SCP 기능은 이곳에 있습니다 (특수 기능이 있어야 할 경우 분리)
    private static void RunEffect()
    {
        // SCP-096
        foreach (var player in PlayerManager.List.Where(target =>
                     target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp096 && !target.IsNPC ))
        {
            if (player.Role is not Scp096Role scp096) continue;

            if (scp096.ChargeCooldown != 0)
                scp096.ChargeCooldown =
                    Mathf.Max(0.0f, scp096.ChargeCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
        }
        
        // SCP-106
        foreach (var player in PlayerManager.List.Where(target =>
                     target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp106 && !target.IsNPC))
        {
            if (player.Role is not Scp106Role scp106) continue;

            if (scp106.RemainingSinkholeCooldown != 0)
                scp106.RemainingSinkholeCooldown = Mathf.Max(0.0f,
                    scp106.RemainingSinkholeCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
        }
        
        // SCP-939
        foreach (var player in PlayerManager.List.Where(target =>
                     target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp939 && !target.IsNPC))
        {
            if (player.Role is not Scp939Role scp939) continue;


            Timing.CallDelayed(.3f, () =>
            {
                if (scp939.AmnesticCloudCooldown != 0 && scp939.AmnesticCloudDuration == 0.0f)
                    scp939.AmnesticCloudCooldown = Mathf.Max(0.0f,
                        scp939.AmnesticCloudCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
            });

            if (scp939.MimicryCooldown != 0)
                scp939.MimicryCooldown =
                    Mathf.Max(0.0f, scp939.MimicryCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
        }
        
        // SCP-049
        foreach (var player in PlayerManager.List.Where(target =>
                     target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp049 && !target.IsNPC))
        {
            if (player.Role is not Scp049Role scp049) continue;

            if (scp049.CallCooldown != 0)
                scp049.CallCooldown = Mathf.Max(0.0f, scp049.CallCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);

            if (scp049.GoodSenseCooldown != 0)
                scp049.GoodSenseCooldown =
                    Mathf.Max(0.0f, scp049.GoodSenseCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
            
            if (scp049.RemainingAttackCooldown != 0)
            {
                scp049.RemainingAttackCooldown = Mathf.Max(0.0f,
                    scp049.RemainingAttackCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
            }
        }
        
        // SCP-3114
        foreach (var player in
                 PlayerManager.List.Where(target =>
                     target != null && target.IsScp && target.Role.Type == RoleTypeId.Scp3114 && !target.IsNPC))
        {
            if (player.Role is not Scp3114Role scp3114) continue;

            scp3114.StaminaRegenMultiplier += SpeedStore.Count * SpeedStore.ScpMultiplier;
        }
    }

    private static void Scp079Effect()
    {
        if (!Timing.IsRunning(_isRunning079))
            _isRunning079 = Timing.RunCoroutine(EnergyCreator());
        // BUG: SCP:SL 자체 버그로 인해 추후 복구 예정, 에너지 지급 기능만 추가
        return;

        IEnumerator<float> EnergyCreator()
        {
            while (SpeedStore.IsEnabled)
            {
                foreach (var player in PlayerManager.List.Where(target =>
                             target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp079 && !target.IsNPC))
                {
                    if (player.Role is not Scp079Role scp079) continue;

                    if (Mathf.Approximately(scp079.MaxEnergy, scp079.Energy))
                        continue;

                    scp079.Energy += 1;
                }

                yield return Timing.WaitForSeconds(SpeedStore.Cos(.1f));
            }
        }
    }

    private static void Scp173Effect()
    {
        foreach (var player in PlayerManager.List.Where(target =>
                     target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp173 && !target.IsNPC))
        {
            if (player.Role is not Scp173Role scp173) continue;
            
            if (scp173.RemainingBreakneckCooldown != 0 && !(scp173.RemainingBreakneckCooldown <= .3f))
                scp173.RemainingBreakneckCooldown = Mathf.Max(0.0f,
                    scp173.RemainingBreakneckCooldown - SpeedStore.Count * 0.1f);

            if (scp173.RemainingTantrumCooldown != 0)
                scp173.RemainingTantrumCooldown = Mathf.Max(0.0f,
                    scp173.RemainingTantrumCooldown - SpeedStore.Count * SpeedStore.ScpMultiplier);
        }

        if (!Timing.IsRunning(_isRunning173))
            _isRunning173 = Timing.RunCoroutine(BlinkCreator());
        return;

        IEnumerator<float> BlinkCreator()
        {
            while (SpeedStore.IsEnabled)
            {
                foreach (var player in PlayerManager.List.Where(target =>
                             target != null && target.IsScpRole() && target.Role.Type == RoleTypeId.Scp173 && !target.IsNPC))
                {
                    if (player.Role is not Scp173Role scp173) continue;
                    if (scp173.BreakneckActive) continue;
                   
                    if (scp173.BlinkCooldown != 0 && !(scp173.BlinkCooldown <= 1f))
                        scp173.BlinkCooldown = Mathf.Max(1f,
                            scp173.BlinkCooldown - SpeedStore.Count * .05f);
                }

                yield return Timing.WaitForSeconds(SpeedStore.Cos(3f));
            }
        }
    }

    private static void On173Blink(BlinkingEventArgs e)
    {
        // 버그 해결용 쿨타임 추가 장치
        if (SpeedStore.Count > 15)
            e.Scp173.BlinkCooldown = 5.0f;
    }

    private static void On049SenseLost(Scp049SenseLostTargetEventArgs e)
    {
        Player player = e.Player;
        if (player.Role != RoleTypeId.Scp049 || player.IsNPC || !player.IsAlive || player.IsHost) return;
        if (!SpeedStore.CurrentSensePlayers.Contains(player)) return;
        if (player.Role is not Scp049Role scp049) return;
        if (scp049.RemainingGoodSenseDuration <= 0f) return;

        scp049.RemainingGoodSenseDuration = 0.0f;
        SpeedStore.CurrentSensePlayers.Remove(player);
    }
    
    private static void On049SenseKilled(Scp049SenseKilledTargetEventArgs e)
    {
        Player player = e.Player;
        if (player.Role != RoleTypeId.Scp049 || player.IsNPC || !player.IsAlive || player.IsHost) return;
        if (!SpeedStore.CurrentSensePlayers.Contains(player)) return;
        if (player.Role is not Scp049Role scp049) return;
        if (scp049.RemainingGoodSenseDuration <= 0f) return;
        
        scp049.RemainingGoodSenseDuration = 0.0f;
        SpeedStore.CurrentSensePlayers.Remove(player);
    }
    
    private static void On049SenseUsing(Scp049UsingSenseEventArgs e)
    {
        var player = e.Player;
        
        if (player.Role == RoleTypeId.Scp049 && player.IsAlive && player.IsPlayer) 
            SpeedStore.CurrentSensePlayers.Add(player);
    }
}