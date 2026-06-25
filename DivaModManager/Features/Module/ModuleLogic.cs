using System.Threading.Tasks;

namespace DivaModManager.Features.Module
{
    public static class ModuleLogic
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool Init(ModuleTab moduleTab)
        {
            bool ret = false;

            moduleTab.Clear();
            var loadPriority = 0;

            // BASE_GAME
            Global.GameBase.moduleData.Load(loadPriority);

            // mods
            foreach (var mod in Global.ModList_All)
            {
                if (mod.enabled)
                {
                    loadPriority++;
                    mod.moduleData.Load(loadPriority, mod.directory_path);
                }
            }

            moduleTab.View();

            ret = true;
            return ret;
        }

        public static async Task<bool> InitAsync(ModuleTab moduleTab)
        {
            moduleTab.Clear();

            await Task.Run(() =>
            {
                var loadPriority = 0;

                Global.GameBase.moduleData.Load(loadPriority);

                foreach (var mod in Global.ModList_All)
                {
                    if (mod.enabled)
                    {
                        loadPriority++;
                        mod.moduleData.Load(loadPriority, mod.directory_path);
                    }
                }
            });

            moduleTab.View();

            return true;
        }

        public static bool Clear(ModuleTab moduleTab)
        {
            bool ret = false;

            moduleTab.Clear();

            ret = true;
            return ret;
        }
    }
}
