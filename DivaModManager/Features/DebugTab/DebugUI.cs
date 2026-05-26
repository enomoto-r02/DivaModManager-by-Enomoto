using System.Linq;
using System.Windows;

// MainWindowクラスに合わせる
namespace DivaModManager.Features.Debug
{
    /// <summary>
    /// Debug
    /// </summary>
    public static class DebugUI
    {
        public static void Init(StartupEventArgs e)
        {
            if (e.Args.ToList().Contains("-debug")) Logger.Mode = Logger.DEBUG_MODE.DEBUG;
            else if (e.Args.ToList().Contains("-developer")) Logger.Mode = Logger.DEBUG_MODE.DEVELOPER;
        }

        /// <summary>
        /// 実装したが不要かもしれない
        /// </summary>
        /// <returns></returns>
        public static string SetLastStartUpModeRegistry()
        {
            var ret = string.Empty;
            if (Logger.Mode == Logger.DEBUG_MODE.DEBUG)
            {
                ret = " -debug";
            }
            else if (Logger.Mode == Logger.DEBUG_MODE.DEVELOPER)
            {
                ret = " -developer";
            }
            return ret;
        }
    }
}
