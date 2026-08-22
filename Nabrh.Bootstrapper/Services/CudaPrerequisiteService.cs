using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Nabrh.Bootstrapper.Services
{
    public sealed record CudaPrerequisiteResult(
        bool IsAvailable,
        string Details,
        bool DriverAvailable = false,
        bool RuntimeAvailable = false);

    /// <summary>
    /// Validates the native requirements of the CUDA build before Burn plans an install.
    /// Checking CUDA_PATH alone is not sufficient: Nabrh links against the CUDA 12 runtime and
    /// cuBLAS DLLs, and it also needs a working NVIDIA display driver and a visible CUDA device.
    /// </summary>
    public static class CudaPrerequisiteService
    {
        private const int MinimumCudaDriverApi = 12000;

        private static readonly string[] RequiredRuntimeLibraries =
        {
            "cudart64_12.dll",
            "cublas64_12.dll",
            "cublasLt64_12.dll"
        };

        public static CudaPrerequisiteResult Check()
        {
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
                return new(false, "يتطلب CUDA نظام Windows بمعمارية 64-بت");

            try
            {
                int initStatus = cuInit(0);
                if (initStatus != 0)
                    return new(false, $"تعذّر تهيئة تعريف NVIDIA — رمز CUDA {Ltr(initStatus.ToString())}");

                int countStatus = cuDeviceGetCount(out int deviceCount);
                if (countStatus != 0 || deviceCount < 1)
                    return new(false, "لم يتم العثور على بطاقة NVIDIA تدعم CUDA");

                int versionStatus = cuDriverGetVersion(out int driverVersion);
                if (versionStatus != 0)
                    return new(false, $"تعذّر قراءة إصدار تعريف CUDA — الرمز {Ltr(versionStatus.ToString())}");

                if (driverVersion < MinimumCudaDriverApi)
                    return new(false, $"تعريف NVIDIA قديم — قدرته {Ltr(FormatCudaVersion(driverVersion))} ويتطلب نبرة CUDA {Ltr("12.x")}");

                string gpuName = GetPrimaryGpuName();
                var resolvedLibraries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var missingLibraries = new List<string>();
                foreach (string library in RequiredRuntimeLibraries)
                {
                    if (TryResolveRuntimeLibrary(library, out string resolvedPath))
                        resolvedLibraries[library] = resolvedPath;
                    else
                        missingLibraries.Add(library);
                }

                if (missingLibraries.Count > 0)
                {
                    string missing = string.Join("، ", missingLibraries.Select(Ltr));
                    return new(
                        false,
                        $"مكتبات تشغيل CUDA 12 مفقودة: {missing}",
                        DriverAvailable: true,
                        RuntimeAvailable: false);
                }

                string runtimeVersion = GetRuntimeVersion(resolvedLibraries["cudart64_12.dll"]);
                if (!runtimeVersion.StartsWith("12.", StringComparison.Ordinal))
                    return new(
                        false,
                        $"إصدار CUDA Runtime غير متوافق: {Ltr(runtimeVersion)}؛ يتطلب نبرة {Ltr("12.x")}",
                        DriverAvailable: true,
                        RuntimeAvailable: false);

                return new(
                    true,
                    $"جاهز — {Ltr(gpuName)}، {Ltr($"CUDA Runtime {runtimeVersion}")} وتعريف NVIDIA متوافق",
                    DriverAvailable: true,
                    RuntimeAvailable: true);
            }
            catch (DllNotFoundException)
            {
                return new(false, "تعريف NVIDIA غير مثبت أو لا يوفّر nvcuda.dll");
            }
            catch (EntryPointNotFoundException)
            {
                return new(false, "تعريف NVIDIA غير متوافق مع واجهة CUDA المطلوبة");
            }
            catch (BadImageFormatException)
            {
                return new(false, "تم العثور على مكتبات CUDA بمعمارية غير متوافقة؛ المطلوب x64");
            }
            catch (Exception ex)
            {
                InstallerLogService.LogError("CUDA prerequisite check failed unexpectedly.", ex, "Prerequisites");
                return new(false, "تعذّر التحقق من متطلبات NVIDIA CUDA؛ تم منع التثبيت احترازياً");
            }
        }

        private static string GetPrimaryGpuName()
        {
            if (cuDeviceGet(out int device, 0) != 0)
                return "NVIDIA GPU";

            var name = new StringBuilder(256);
            return cuDeviceGetName(name, name.Capacity, device) == 0 && name.Length > 0
                ? name.ToString()
                : "NVIDIA GPU";
        }

        private static bool TryResolveRuntimeLibrary(string fileName, out string resolvedPath)
        {
            resolvedPath = string.Empty;

            // First use the normal Windows loader path. This covers a redistributable installed
            // beside the BA, System32 and CUDA's bin directory when the toolkit updated PATH.
            if (TryLoadAndFree(fileName, out resolvedPath))
                return true;

            foreach (string directory in GetCudaRuntimeDirectories())
            {
                string fullPath = Path.Combine(directory, fileName);
                if (File.Exists(fullPath) && TryLoadAndFree(fullPath, out resolvedPath))
                    return true;
            }

            return false;
        }

        private static bool TryLoadAndFree(string path, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            try
            {
                if (!NativeLibrary.TryLoad(path, out IntPtr handle))
                    return false;

                var buffer = new StringBuilder(32768);
                resolvedPath = GetModuleFileName(handle, buffer, buffer.Capacity) > 0
                    ? buffer.ToString()
                    : Path.GetFullPath(path);
                NativeLibrary.Free(handle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetRuntimeVersion(string cudartPath)
        {
            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(cudartPath);
                // NVIDIA uses a Windows resource version such as 6.14.11.12010 for cudart 12.1,
                // so FileMajorPart/FileMinorPart are OS resource values, not CUDA's version.
                string metadata = $"{version.FileDescription} {version.ProductName}";
                Match metadataVersion = Regex.Match(
                    metadata,
                    @"(?:Version|CUDA)\s+(?<version>12\.\d+)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (metadataVersion.Success)
                    return metadataVersion.Groups["version"].Value;

                Match toolkitDirectory = Regex.Match(
                    cudartPath,
                    @"[\\/]v(?<version>12\.\d+)[\\/]bin[\\/]",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (toolkitDirectory.Success)
                    return toolkitDirectory.Groups["version"].Value;
            }
            catch
            {
                // The DLL name itself still proves the CUDA 12 ABI family below.
            }

            return "12.x";
        }

        private static IEnumerable<string> GetCudaRuntimeDirectories()
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppContext.BaseDirectory
            };

            foreach (string variable in new[] { "CUDA_PATH", "CUDA_PATH_V12_0", "CUDA_PATH_V12_1", "CUDA_PATH_V12_2", "CUDA_PATH_V12_3", "CUDA_PATH_V12_4", "CUDA_PATH_V12_5", "CUDA_PATH_V12_6", "CUDA_PATH_V12_8" })
            {
                string? value = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(value))
                    candidates.Add(Path.Combine(value, "bin"));
            }

            foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                         .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(entry.Trim('"'));
            }

            string toolkitRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA GPU Computing Toolkit", "CUDA");

            if (Directory.Exists(toolkitRoot))
            {
                foreach (string versionDirectory in Directory.EnumerateDirectories(toolkitRoot, "v12.*"))
                    candidates.Add(Path.Combine(versionDirectory, "bin"));
            }

            return candidates.Where(Directory.Exists);
        }

        private static string FormatCudaVersion(int rawVersion)
        {
            int major = rawVersion / 1000;
            int minor = (rawVersion % 1000) / 10;
            return $"{major}.{minor}";
        }

        private static string Ltr(string value) => $"\u202A{value}\u202C";

        [DllImport("nvcuda.dll")]
        private static extern int cuInit(uint flags);

        [DllImport("nvcuda.dll")]
        private static extern int cuDeviceGetCount(out int count);

        [DllImport("nvcuda.dll")]
        private static extern int cuDriverGetVersion(out int driverVersion);

        [DllImport("nvcuda.dll")]
        private static extern int cuDeviceGet(out int device, int ordinal);

        [DllImport("nvcuda.dll", CharSet = CharSet.Ansi)]
        private static extern int cuDeviceGetName(StringBuilder name, int length, int device);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetModuleFileName(IntPtr module, StringBuilder fileName, int size);
    }
}
