#if BEPINEX
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace QuickRetry
{
    [BepInPlugin("dev.jayson.quickretry", "QuickRetry", QuickRetryCore.Version)]
    public class QuickRetryPlugin : BasePlugin
    {
        public override void Load()
        {
            ModLog.InfoSink = m => Log.LogInfo(m);
            ModLog.WarnSink = m => Log.LogWarning(m);
            ModLog.ErrorSink = m => Log.LogError(m);
            QuickRetryCore.Initialize(new HarmonyLib.Harmony("dev.jayson.quickretry"));
            AddComponent<QuickRetryTicker>();
        }
    }

    /// <summary>Drives <see cref="FrameScheduler"/>. BepInEx registers this type in IL2CPP on AddComponent.</summary>
    public class QuickRetryTicker : MonoBehaviour
    {
        public QuickRetryTicker(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            FrameScheduler.Tick();
            QuickRetryCore.OnFrame();
        }
    }
}
#endif
