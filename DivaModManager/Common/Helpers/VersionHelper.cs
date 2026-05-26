using System;
using System.IO;
using System.Linq;

namespace DivaModManager.Common.Helpers
{
    public class VersionHelper
    {
        public enum Result
        {
            None = 0,
            VersionA_NOTHING,
            VersionB_NOTHING,
            VersionA_AS_LONGER,
            SAME,
            VersionB_AS_LONGER,
        }

        public static Result CompareVersions(string versionA, string versionB)
        {
            if (string.IsNullOrWhiteSpace(versionA)) { return Result.VersionA_NOTHING; }
            else if (string.IsNullOrWhiteSpace(versionB)) { return Result.VersionB_NOTHING; }

            var partsA = versionA.Split('.').Select(int.Parse).ToArray();
            var partsB = versionB.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
            {
                int valA = (i < partsA.Length) ? partsA[i] : 0;
                int valB = (i < partsB.Length) ? partsB[i] : 0;

                if (valA > valB) return Result.VersionA_AS_LONGER;  // Aが大きい
                if (valA < valB) return Result.VersionB_AS_LONGER;  // Aが小さい
            }
            return Result.SAME;
        }

        /// <summary>
        /// 最終更新日をチェックする(主にDML用)
        /// </summary>
        /// <param name="filePathA"></param>
        /// <param name="filePathB"></param>
        /// <returns></returns>
        public static Result CompareLastUpdate(string filePathA, string filePathB)
        {
            if (!File.Exists(filePathA)) { return Result.VersionA_NOTHING; }
            else if (!File.Exists(filePathB)) { return Result.VersionB_NOTHING; }

            var fileInfoA = new FileInfo(filePathA).LastWriteTime;
            var fileInfoB = new FileInfo(filePathB).LastWriteTime;

            if (fileInfoA > fileInfoB) return Result.VersionA_AS_LONGER;
            else if (fileInfoA == fileInfoB) return Result.SAME;
            else return Result.VersionB_AS_LONGER;
        }
    }
}