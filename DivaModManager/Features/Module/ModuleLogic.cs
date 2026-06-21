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

            // BASE_GAME
            Global.GameBase.moduleData.Load();

            // mods
            foreach (var mod in Global.ModList_All)
            {
                if (mod.enabled)
                {
                    mod.moduleData.Load(mod.directory_path);
                }
            }

            moduleTab.View();

            ret = true;
            return ret;
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
