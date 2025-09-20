using Microsoft.VisualBasic.FileIO;
using Onova.Services;
using SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager
{
    public class ZipExtractor : IPackageExtractor
    {
        public async Task ExtractPackageAsync(string sourceFilePath, string destDirPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                //if (Path.GetExtension(sourceFilePath).Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                //{
                //    using (var archive = new ArchiveFile(sourceFilePath))
                //    {
                //        archive.Extract(destDirPath);
                //    }
                //}
                string extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
                // アーカイブ内のファイル数をカウント
                if (extension == ".7z")
                {
                    if (!Global.SevenZipDlllExist)
                    {
                        Global.logger.WriteLine($"Extraction failed because 7z.dll does not exist. Please re-download DivaModManager by Enomoto.,", LoggerType.Error);
                        return;
                    }
                    // 展開(処理速度向上のためSevenZipSharp.Interopを使用)
                    using var extractor = new SevenZipExtractor(sourceFilePath);
                    extractor.ExtractArchive(destDirPath);
                }
                else
                {
                    using (Stream stream = File.OpenRead(sourceFilePath))
                    using (var reader = ReaderFactory.Open(stream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                            {
                                reader.WriteEntryToDirectory(destDirPath, new ExtractionOptions()
                                {
                                    ExtractFullPath = true,
                                    Overwrite = true
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                Global.logger.WriteLine("Failed to extract update", LoggerType.Error);
            }
            FileSystem.DeleteFile(@$"{sourceFilePath}", UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
        }

    }
}
