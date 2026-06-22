using DivaModManager.Common.Helpers;
using DivaModManager.Features.MML;

namespace DivaModManager.Features.MikuMikuLibrary
{
    internal static class MikuMikuLibraryHelper
    {
        public static string MIKUMIKULIBRARY_URL_GITHUB = "https://github.com/blueskythlikesclouds/MikuMikuLibrary/releases";

        public static void Test_Extract()
        {
            string mmlDllPath = @"G:\diva_tool\MikuMikuModel_release\MikuMikuLibrary.dll";
            string cpkPath = "diva_dlc00_region.cpk";

            using var extractor = new CpkExtractor(mmlDllPath, cpkPath);

            // エントリ一覧を確認する場合
            //foreach (string name in extractor.GetEntryNames())
            //    Console.WriteLine(name);

            extractor.ExtractFile(
                "rom_steam_region_dlc/rom/mdata_pv_db.txt",
                "mdata_pv_db.txt");

            // CPK 全体を展開する場合
            // extractor.ExtractAll(@"C:\output\diva_dlc00_region");

            var ret = WindowHelper.DMMWindowOpenAsync(52).Result;
        }
    }
}
