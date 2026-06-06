using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace DivaModManager.Features.DMM
{
    public static class ZipExtractor
    {
        public static async Task ExtractAsync(string sourceFilePath, string destDirPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Directory.CreateDirectory(destDirPath);
                using (Stream stream = File.OpenRead(sourceFilePath))
                using (var reader = ReaderFactory.OpenReader(stream))
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
            catch
            {
                Logger.WriteLine("Failed to extract update", LoggerType.Error);
            }
            File.Delete(sourceFilePath);
        }
    }
}
