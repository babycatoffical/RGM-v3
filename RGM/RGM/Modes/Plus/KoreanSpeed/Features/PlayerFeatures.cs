using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.MicroHID.Modules;
using MEC;
using RGM.API.Features;
using UnityEngine;

using PlayerHandler = Exiled.Events.Handlers.Player;

namespace RGM.Modes;

public static class PlayerFeatures
{
    private static CoroutineHandle _hidCoroutine;
    private static Mutex _unloadMutex;
    private static Mutex _loadMutex;

    public static void Activate()
    {
        PlayerHandler.ChangingMicroHIDState += OnChanging;
        PlayerHandler.Spawned += OnSpawn;
        PlayerHandler.Died += OnDied;
        PlayerHandler.SearchingPickup += OnSearchingPickup;
        PlayerHandler.ThrowingRequest += OnThrowingRequest;

        _loadMutex = new();
        _unloadMutex = new();
    }

    public static void DeActivate()
    {
        PlayerHandler.ChangingMicroHIDState -= OnChanging;
        PlayerHandler.Spawned -= OnSpawn;
        PlayerHandler.Died -= OnDied;
        PlayerHandler.SearchingPickup -= OnSearchingPickup;
        PlayerHandler.ThrowingRequest -= OnThrowingRequest;

        _unloadMutex.ReleaseMutex();
        UnloadEffects();
        
        _loadMutex.Dispose();
        _unloadMutex.Dispose();
    }

    internal static void AddEffects()
    {
        var mutexOnline = _loadMutex.WaitOne(3000);
        try
        {
            if (!mutexOnline) return;
            
            foreach (var player in PlayerManager.List.Where(player =>
                         player != null && !player.IsDead && !player.IsNPC))
            {
                UnloadEffects();
                Timing.CallDelayed(Timing.WaitForOneFrame, () =>
                {
                    player.AddEffect(EffectType.MovementBoost, (byte)(SpeedStore.Count * 2));
                    player.AddEffect(EffectType.Scp1853, Mathf.Min(SpeedStore.Count, 5));
                });
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error while adding effects, Deception: {e.Message}");
        }
        finally
        {
            _loadMutex.ReleaseMutex();
        }
    }

    internal static void UnloadEffects()
    {
        var mutexOnline = _unloadMutex.WaitOne(3000);
        try
        {
            if (!mutexOnline) return;
            
            foreach (var player in PlayerManager.List.Where(player =>
                         player != null && !player.IsDead && !player.IsNPC))
            {
                player.RemoveEffect(EffectType.MovementBoost, 255);
                player.RemoveEffect(EffectType.Scp1853, 5);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error while removing effects, Deception: {e.Message}");
        }
        finally
        {
            _unloadMutex.ReleaseMutex();
        }
    }

    private static void OnChanging(ChangingMicroHIDStateEventArgs ev)
    {
        if (!Timing.IsRunning(_hidCoroutine))
            _hidCoroutine = Timing.RunCoroutine(Run());
        return;

        IEnumerator<float> Run()
        {
            while (SpeedStore.IsEnabled)
            {
                foreach (var items in Item.List.Where(x =>
                             x.Type == ItemType.MicroHID))
                {
                    if (items is not MicroHid hid) continue;
                    if (hid.Owner.IsNPC || hid.WindUpProgress >= 1 || hid.State is not MicroHidPhase.WindingUp) continue;

                    hid.WindUpProgress += 0.1f;
                }
                
                foreach (var items in Item.List.Where(x =>
                             x.Type == ItemType.MicroHID))
                {
                    if (items is not MicroHid hid) continue;
                    if (hid.Owner.IsNPC || hid.State is not MicroHidPhase.WindingDown || hid.WindUpProgress <= 0) continue;

                    hid.WindUpProgress -= 0.1f;
                }

                yield return Timing.WaitForSeconds(SpeedStore.Sin(.1f));
            }
        }
    }

    private static void OnSearchingPickup(SearchingPickupEventArgs ev) => ev.SearchTime -= SpeedStore.Count * 0.1f;

    private static void OnThrowingRequest(ThrowingRequestEventArgs ev) => ev.Throwable.PinPullTime -= SpeedStore.Count * 0.1f;

    private static void OnDied(DiedEventArgs ev)
    {
        if (!(SpeedStore.Count > 125))
            SpeedStore.Count++;

        AddEffects();
    }

    private static void OnSpawn(SpawnedEventArgs ev)
    {
        Timing.CallDelayed(Timing.WaitForOneFrame, () =>
        {
            if (ev.Player == null || !ev.Player.IsAlive) return;
            AddEffects();
        });
    }
}