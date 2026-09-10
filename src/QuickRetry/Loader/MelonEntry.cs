#if MELONLOADER
using HarmonyLib;
using MelonLoader;
using QuickRetry;

[assembly: MelonInfo(typeof(QuickRetryMelon), "QuickRetry", QuickRetryCore.Version, "jayson")]
[assembly: MelonGame("poncle", "Vampire Survivors")]
[assembly: HarmonyDontPatchAll]

namespace QuickRetry
{
    public class QuickRetryMelon : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ModLog.InfoSink = m => LoggerInstance.Msg(m);
            ModLog.WarnSink = m => LoggerInstance.Warning(m);
            ModLog.ErrorSink = m => LoggerInstance.Error(m);
            QuickRetryCore.Initialize(HarmonyInstance);
        }

        public override void OnUpdate()
        {
            FrameScheduler.Tick();
            QuickRetryCore.OnFrame();
        }
    }
}
#endif
