#if MELONLOADER
using HarmonyLib;
using MelonLoader;
using VampireSurvivorsUx;

[assembly: MelonInfo(typeof(VampireSurvivorsUxMelon), "VampireSurvivorsUx", QuickRetryCore.Version, "jayson")]
[assembly: MelonGame("poncle", "Vampire Survivors")]
[assembly: HarmonyDontPatchAll]

namespace VampireSurvivorsUx
{
    public class VampireSurvivorsUxMelon : MelonMod
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
