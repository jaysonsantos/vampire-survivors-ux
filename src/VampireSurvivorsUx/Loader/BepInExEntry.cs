#if BEPINEX
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace VampireSurvivorsUx
{
    [BepInPlugin("dev.jayson.vampiresurvivorsux", "VampireSurvivorsUx", QuickRetryCore.Version)]
    public class VampireSurvivorsUxPlugin : BasePlugin
    {
        public override void Load()
        {
            ModLog.InfoSink = m => Log.LogInfo(m);
            ModLog.WarnSink = m => Log.LogWarning(m);
            ModLog.ErrorSink = m => Log.LogError(m);
            QuickRetryCore.Initialize(new HarmonyLib.Harmony("dev.jayson.vampiresurvivorsux"));
            AddComponent<VampireSurvivorsUxTicker>();
        }
    }

    /// <summary>Drives <see cref="FrameScheduler"/>. BepInEx registers this type in IL2CPP on AddComponent.</summary>
    public class VampireSurvivorsUxTicker : MonoBehaviour
    {
        public VampireSurvivorsUxTicker(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            FrameScheduler.Tick();
            QuickRetryCore.OnFrame();
        }
    }
}
#endif
