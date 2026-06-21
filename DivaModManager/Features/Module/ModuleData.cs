using DivaModManager.Common.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DivaModManager.Features.Module
{
    public class ModuleData
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

        private static List<string> ITM_TBL_EXTRACT_FOLDER_PATTERN = new()
        {
            "gm_module_tbl", "gm_customize_item_tbl",
            "mod_gm_module_tbl", "mod_gm_customize_item_tbl",
            "mdata_gm_module_tbl", "mdata_gm_customize_item_tbl",
        };

        private static List<string> ITM_TBL_NAME_PATTERN = new()
        {
            "gm_module_id.bin",
            "gm_customize_item_id.bin",
        };

        public List<ModuleTabView> moduleTabViewList;

        public ModuleData()
        {
            moduleTabViewList = new();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            moduleTabViewList.Clear();
        }

        /// <summary>
        /// (mod name)フォルダのpv_dbまたはmod_pv_dbを読み込む
        /// </summary>
        /// <param name="targetModPath">
        /// ブランクの場合は"(略)\実行ファイル\BASE\rom_steam_region\rom\gm_module_id.bin"など
        /// それ以外の場合は"(略)\Hatsune Miku Project DIVA Mega Mix Plus\mods\(mod name)\rom\gm_customize_item_id.bin"など
        /// </param>
        /// <returns></returns>
        public bool Load(string targetModPath = "")
        {
            bool ret = false;
            Clear();
            var basePath = "";          // "(mod name)"またはブランク("BASE"の場合)想定
            var modFolderPath = "";
            var appendFolderName = string.Empty;

            // todo: アルゴリズムがひどい
            foreach (var BASE_DIR in BASE_DIR_PATTERN)                                              // "diva_main"など
            {
                foreach (var BASE_REGION in BASE_REGION_PATTERN)                                    // "rom_ps4"など
                {
                    foreach (var BASE_DIR_ROM in BASE_DIR_ROM_PATTERN)                              // "rom"
                    {
                        foreach (var ITM_TBL_EXTRACT_FOLDER in ITM_TBL_EXTRACT_FOLDER_PATTERN)      // "gm_module_tbl"フォルダなど
                        {
                            foreach (var ITM_TBL_NAME in ITM_TBL_NAME_PATTERN)                      // "gm_module_id.bin"フォルダなど
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

                                relativePath = Path.Combine(BASE_REGION, appendFolderName, ITM_TBL_EXTRACT_FOLDER, ITM_TBL_NAME);
                                pvDbPath = Path.Combine(basePath, relativePath);

                                LoadDetail(pvDbPath, modFolderPath, relativePath);
                            }
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
                var moduleTabView = new ModuleTabView();
                moduleTabView.Set(ModuleTab.ViewKeys, Path.GetFileName(modFolderPath), relativePath, lineCnt, modFolderPath, key, value);
                if (moduleTabView.ViewFlg)
                {
                    moduleTabViewList.Add(moduleTabView);
                }
            }
            return ret;
        }
    }
}
