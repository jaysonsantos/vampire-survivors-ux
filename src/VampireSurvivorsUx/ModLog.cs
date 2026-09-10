using System;

namespace VampireSurvivorsUx
{
    /// <summary>Loader-independent log sink. The loader entry point sets the delegates.</summary>
    internal static class ModLog
    {
        public static Action<string> InfoSink = _ => { };
        public static Action<string> WarnSink = _ => { };
        public static Action<string> ErrorSink = _ => { };

        public static void Info(string message) => InfoSink(message);
        public static void Warn(string message) => WarnSink(message);
        public static void Error(string message) => ErrorSink(message);
        public static void Error(string context, Exception e) => ErrorSink(context + ": " + e);
    }
}
