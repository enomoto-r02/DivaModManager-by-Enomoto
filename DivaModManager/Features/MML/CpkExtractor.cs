using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace DivaModManager.Features.MML
{
    public class CpkExtractor : IDisposable
    {
        private readonly SafeLoadContext _loadContext;
        private readonly Assembly _assembly;
        private readonly Type _cpkArchiveType;
        private readonly Type _entryStreamModeType;
        private readonly object _cpk;
        private bool _disposed;

        public CpkExtractor(string mmlDllPath, string cpkPath)
        {
            string dllDir = Path.GetDirectoryName(Path.GetFullPath(mmlDllPath));
            _loadContext = new SafeLoadContext(dllDir);
            _assembly = _loadContext.LoadFromAssembly(Path.GetFullPath(mmlDllPath));

            _cpkArchiveType = _assembly.GetType("MikuMikuLibrary.Archives.CriMw.CpkArchive");
            _entryStreamModeType = _assembly.GetType("MikuMikuLibrary.Archives.EntryStreamMode");

            var binaryFileType = _assembly.GetType("MikuMikuLibrary.IO.BinaryFile");
            var loadMethod = binaryFileType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Load" && m.IsGenericMethod && m.GetParameters().Length == 1)
                .MakeGenericMethod(_cpkArchiveType);

            _cpk = loadMethod.Invoke(null, [cpkPath]);
        }

        public IEnumerable<string> GetEntryNames()
        {
            return (IEnumerable<string>)_cpkArchiveType
                .GetProperty("FileNames")!.GetValue(_cpk)!;
        }

        public void ExtractFile(string entryPath, string outputPath)
        {
            var openMethod = _cpkArchiveType.GetMethod("Open",
                [typeof(string), _entryStreamModeType])!;
            var originalStream = Enum.Parse(_entryStreamModeType, "OriginalStream");

            using var source = (Stream)openMethod.Invoke(_cpk, [entryPath, originalStream])!;
            using var dest = File.Create(outputPath);
            source.CopyTo(dest);
        }

        public void ExtractAll(string outputDir)
        {
            _cpkArchiveType.GetMethod("Extract", [typeof(string)])!
                .Invoke(_cpk, [outputDir]);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                (_cpk as IDisposable)?.Dispose();
                _disposed = true;
            }
        }

        // ──────────────────────────────────────────────────
        // SafeLoadContext: kernel32 / Native DLL をガード
        // ──────────────────────────────────────────────────
        private sealed class SafeLoadContext : AssemblyLoadContext
        {
            private readonly string _dllDir;

            public SafeLoadContext(string dllDir) : base(isCollectible: false)
                => _dllDir = dllDir;

            public Assembly LoadFromAssembly(string path)
                => LoadFromAssemblyPath(path);

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (assemblyName.Name == "MikuMikuLibrary.Native")
                    return null;

                string path = Path.Combine(_dllDir, $"{assemblyName.Name}.dll");
                if (File.Exists(path))
                    return LoadFromAssemblyPath(path);

                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                if (unmanagedDllName.StartsWith("kernel32",
                        StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                string path = Path.Combine(_dllDir, $"{unmanagedDllName}");
                return File.Exists(path)
                    ? LoadUnmanagedDllFromPath(path)
                    : IntPtr.Zero;
            }
        }
    }
}
