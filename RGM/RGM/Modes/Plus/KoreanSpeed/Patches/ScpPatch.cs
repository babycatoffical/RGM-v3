using System;
using Exiled.API.Features;
using UnityEngine;

namespace RGM.Modes.Patches;

public static class ScpPatch
{
    private const float Scp049ResurrectDuration = 7;

    public static void Scp049Postfix(ref float __result)
    {
        try
        {
            __result = Mathf.Max(0.2f, Scp049ResurrectDuration - SpeedStore.Count * .1f);
        }
        catch (Exception e)
        {
            Log.Error($"[KoreanSpeed/Scp049Postfix] Exception: {e}");
        }
    }
}