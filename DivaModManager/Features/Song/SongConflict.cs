using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DivaModManager.Features.Conflict
{
    internal class SongConflict
    {
        private static string BASE_TEMP = "BASE_DATA";
        private static string PV_DB_BASE = "rom_steam_region";
        private static string PV_DB_BASE_NAME = "pv_db.txt";

        private static string PV_DB_DIR = "rom";
        private static string PV_DB_NAME = "mod_pv_db.txt";

        private static string RESULT_DIR_BASE = "Conflict";
        private static string RESULT_DIR_SONG = "Song";
        private static string RESULT_FILENAME = "SongConflict.txt";

        private static string PV_DB_DATA_ADD = "song_";
        private static string PV_DB_DATA_ANOTHER = "another_song";
        private static string PV_DB_DATA_DISP = "vocal_disp_name";
        private static string PV_DB_DATA_LENGTH = "length";

        public Dictionary<string, Dictionary<List<string>, string>> AnotherSongList;    // AnotherSong競合結果
        public List<string> pvDbAllLineList;    // 対象Modのpv_db情報
        public List<string> addSongList;        // AddSong競合結果

        // 最終チェック日
        public DateTime lastCheckdt { get; set; }

        public SongConflict()
        {
            AnotherSongList = new();
            pvDbAllLineList = new();
            addSongList = new();
            lastCheckdt = DateTime.Now;
        }

        /// <summary>
        /// (mod name)フォルダのpv_dbまたはmod_pv_dbを読み込み、AnotherSongListに追加する
        /// </summary>
        /// <param name="targetModPath">
        /// ブランクの場合は"(略)\実行ファイル\MMP_DATA\rom_steam_region\rom\pv_db.txt
        /// それ以外の場合は"(略)\Hatsune Miku Project DIVA Mega Mix Plus\mods\(mod name)\rom\mod_pv_db.txt"
        /// </param>
        /// <returns></returns>
        public bool Load(string targetModPath = "")
        {
            bool ret = false;
            var pvDbPath = "";
            var pvDbName = "";
            var modFolderName = "";
            var modFolderPath = "";
            this.pvDbAllLineList = new();
            this.addSongList = new();

            if (string.IsNullOrEmpty(targetModPath))
            {
                // AnotherSongListを初期化する
                AnotherSongList = new();

                pvDbPath = Path.Combine(Global.assemblyLocation, BASE_TEMP, PV_DB_BASE, PV_DB_DIR, PV_DB_BASE_NAME);
                modFolderName = BASE_TEMP;
                modFolderPath = "";
            }
            else
            {
                // AnotherSongListを初期化しない

                pvDbPath = Path.Combine(Global.ConfigJson.GetModsLocation(), targetModPath, PV_DB_DIR, PV_DB_NAME);
                modFolderName = Path.GetFileName(targetModPath);
                modFolderPath = targetModPath;
            }
            pvDbName = Path.GetFileName(pvDbPath);
            if (!FileHelper.FileExists(pvDbPath))
            {
                return ret;
            }
            var pvDbAllLineList = FileHelper.TryReadAllText(pvDbPath).Replace("\r", "").Split("\n").ToList();
            var lineCnt = 0;
            Dictionary<List<string>, string> modSongData = new();
            foreach (string line in pvDbAllLineList)
            {
                lineCnt++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var i = line.IndexOf('=');
                if (i == -1) continue;
                var key = line.Substring(0, i).Split(".").ToList();

                // Insertしているので以降の処理でのデータ抽出時は添字注意
                key.Insert(0, lineCnt.ToString());
                key.Insert(0, pvDbName);
                key.Insert(0, targetModPath);

                var value = line.Substring(i + 1);
                modSongData.Add(key, value);
            }
            this.AnotherSongList.Add(modFolderName, modSongData);

            ret = true;
            return ret;
        }

        /// <summary>
        /// DataGridに表示するならこのチェック不要では？
        /// </summary>
        /// <returns></returns>
        public bool ConflictCheck()
        {
            bool ret = false;


            //// IDの重複チェック


            //// 結果出力
            //var resultPath = Path.Combine(Global.assemblyLocation, RESULT_DIR_BASE, RESULT_DIR_SONG, RESULT_FILENAME);
            //FileHelper.TryWriteAllText(resultPath, anotherSongList);

            ret = true;
            return ret;
        }
    }
}
