using DivaModManager.Features.Module;

namespace DivaModManager.Features.Song
{
    public static class SongLogic
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool Init(SongTab songTab)
        {
            bool ret = false;

            songTab.Clear();

            // BASE_GAME
            Global.GameBase.songData.Load();

            // mods
            foreach (var mod in Global.ModList)
            {
                if (mod.enabled)
                {
                    mod.songData.Load(mod.directory_path);
                }
            }

            songTab.View();

            ret = true;
            return ret;
        }

        public static bool Clear(SongTab songTab)
        {
            bool ret = false;

            songTab.Clear();

            ret = true;
            return ret;
        }
    }
}
