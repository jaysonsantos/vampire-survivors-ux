#if BEPINEX_MONO
using BepInEx;
using HarmonyLib;

namespace VampireSurvivorsUx
{
    /// <summary>
    /// Entry point for BepInEx 5 on a Unity Mono build (macOS and Linux native builds).
    /// A Mono plugin is a MonoBehaviour, so the same class drives <see cref="FrameScheduler"/>.
    /// </summary>
    [BepInPlugin("dev.jayson.vampiresurvivorsux", "VampireSurvivorsUx", QuickRetryCore.Version)]
    public class VampireSurvivorsUxMonoPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ModLog.InfoSink = m => Logger.LogInfo(m);
            ModLog.WarnSink = m => Logger.LogWarning(m);
            ModLog.ErrorSink = m => Logger.LogError(m);
            QuickRetryCore.Initialize(new Harmony("dev.jayson.vampiresurvivorsux"));
        }

        private void Update()
        {
            FrameScheduler.Tick();
            QuickRetryCore.OnFrame();
        }
    }
}
#endif
