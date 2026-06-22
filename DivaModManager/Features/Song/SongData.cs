using DivaModManager.Common.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DivaModManager.Features.Song
{
    public class SongData
    {
        public static string BASE_FOLDER_NAME = "BASE";

        private static List<string> BASE_DIR_PATTERN = new()
        {
            "diva_main", "diva_main_region", "diva_dlc00", "diva_dlc00_region"
        };

        private static List<string> BASE_REGION_PATTERN = new()
        {
            "rom", "rom_ps4", "rom_ps4_dlc", "rom_ps4_fix_en", "rom_ps4_patch",
            "rom_steam", "rom_steam_cn", "rom_steam_en", "rom_steam_fr", "rom_steam_ge", "rom_steam_it", "rom_steam_kr", "rom_steam_sp", "rom_steam_tw",
            "rom_switch", "rom_switch_cn", "rom_switch_en", "rom_switch_kr", "rom_switch_tw",
            "rom_steam_region", "rom_steam_region_dlc", "rom_steam_dlc"
        };

        private static List<string> BASE_DIR_ROM_PATTERN = new()
        {
            "rom"
        };

        private static List<string> PV_DB_NAME_PATTERN = new()
        {
            "pv_db.txt", "mod_pv_db.txt", "mdata_pv_db.txt"
        };

        public List<SongTabView> songTabViewList;

        public SongData()
        {
            songTabViewList = new();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            songTabViewList.Clear();
        }

        /// <summary>
        /// (mod name)フォルダのpv_dbまたはmod_pv_dbを読み込む
        /// </summary>
        /// <param name="targetModPath">
        /// ブランクの場合は"(略)\実行ファイル\BASE\rom_steam_region\rom\pv_db.txt"など
        /// それ以外の場合は"(略)\Hatsune Miku Project DIVA Mega Mix Plus\mods\(mod name)\rom\mod_pv_db.txt"など
        /// </param>
        /// <returns></returns>
        public bool Load(string targetModPath = "")
        {
            bool ret = false;
            Clear();
            var basePath = string.Empty;            // "(mod name)"またはブランク("BASE"の場合)想定
            var modFolderPath = string.Empty;
            var appendFolderName = string.Empty;

            // todo: アルゴリズムがひどい
            foreach (var BASE_DIR in BASE_DIR_PATTERN)                          // "diva_main"など
            {
                foreach (var BASE_REGION in BASE_REGION_PATTERN)                // "rom_ps4"など
                {
                    foreach (var BASE_DIR_ROM in BASE_DIR_ROM_PATTERN)          // "rom"
                    {
                        foreach (var PV_DB_BASE_NAME in PV_DB_NAME_PATTERN)     // "pv_db.txt"など
                        {
                            var pvDbPath = string.Empty;
                            if (string.IsNullOrEmpty(targetModPath))
                            {
                                basePath = Path.Combine(Global.assemblyLocation, BASE_FOLDER_NAME, BASE_DIR);
                                modFolderPath = BASE_FOLDER_NAME;
                            }
                            else
                            {
                                basePath = Path.GetFullPath(targetModPath);
                                modFolderPath = targetModPath;
                            }

                            // "diva_main\rom"の場合のみ直下、それ以外は"rom"フォルダはネストされる("rom_ps4\rom"など)
                            var relativePath = string.Empty;
                            if (BASE_REGION != BASE_DIR_ROM)
                            {
                                appendFolderName = BASE_DIR_ROM;
                            }

                            relativePath = Path.Combine(BASE_REGION, appendFolderName, PV_DB_BASE_NAME);
                            pvDbPath = Path.Combine(basePath, relativePath);

                            LoadDetail(pvDbPath, modFolderPath, relativePath);
                        }
                    }
                }
            }

            ret = true;
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pvDbPath"></param>
        /// <param name="modFolderPath"></param>
        /// <returns></returns>
        private bool LoadDetail(string pvDbPath, string modFolderPath, string relativePath)
        {
            bool ret = false;
            if (!FileHelper.FileExists(pvDbPath))
            {
                //Logger.WriteLine($"'{pvDbPath}' is not Found.", LoggerType.Debug);
                return ret;
            }
            var pvDbAllLineList = FileHelper.TryReadAllText(pvDbPath).Replace("\r", "").Split("\n").ToList();
            var lineCnt = 0;
            foreach (string line in pvDbAllLineList)
            {
                lineCnt++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Trim().StartsWith("#")) continue;
                var i = line.IndexOf('=');
                if (i == -1) continue;
                var key = line.Substring(0, i).Split(".").ToList();
                var value = line.Substring(i + 1);
                var songTabView = new SongTabView();
                songTabView.Set(SongTab.ViewKeys, Path.GetFileName(modFolderPath), relativePath, lineCnt, modFolderPath, key, value);
                if (songTabView.ViewFlg)
                {
                    songTabViewList.Add(songTabView);
                }
            }
            return ret;
        }
    }
}
