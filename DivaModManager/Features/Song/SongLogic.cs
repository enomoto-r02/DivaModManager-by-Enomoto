using System.Threading.Tasks;

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
            foreach (var mod in Global.ModList_All)
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

        public static async Task<bool> InitAsync(SongTab songTab)
        {
            songTab.Clear();

            await Task.Run(() =>
            {
                Global.GameBase.songData.Load();

                foreach (var mod in Global.ModList_All)
                {
                    if (mod.enabled)
                    {
                        mod.songData.Load(mod.directory_path);
                    }
                }
            });

            songTab.View();

            return true;
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
