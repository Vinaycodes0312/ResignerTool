using System.Diagnostics;
using System.IO.Compression;


namespace ResignerTool.Services;

public class ApkSignerService
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "apk-resign");
    private readonly ResignerTool.Services.ILogService _logger;

    public ApkSignerService()
    {
        _logger = new FileLoggerService();
        Directory.CreateDirectory(_tempPath);
    }

    public Task<bool> ValidateKeystoreAsync(string keystorePath, string password)
    {
        if (!File.Exists(keystorePath))
        {
            _logger.LogError($"Keystore file not found: {keystorePath}");
            return Task.FromResult(false);
        }

        try
        {
#if !(IOS || MACCATALYST)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "keytool",
                    Arguments = $"-list -v -keystore \"{keystorePath}\" -storepass {password}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            RunProcessAsync(process.StartInfo).Wait();
#endif
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to validate keystore", ex);
            return Task.FromResult(false);
        }
    }

    public Task<string> CreateKeystoreAsync(string password)
    {
        var keystorePath = Path.Combine(_tempPath, $"keystore_{DateTime.Now:yyyyMMddHHmmss}.keystore");
        
        try
        {
#if !(IOS || MACCATALYST)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "keytool",
                    Arguments = $"-genkey -v -keystore \"{keystorePath}\" -alias app_key -keyalg RSA -keysize 2048 -validity 10000 -storepass {password} -keypass {password} -dname \"CN=Unknown,OU=Unknown,O=Unknown,L=Unknown,ST=Unknown,C=US\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            RunProcessAsync(process.StartInfo).Wait();
#endif
            return Task.FromResult(keystorePath);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create keystore", ex);
            if (File.Exists(keystorePath))
            {
                try { File.Delete(keystorePath); } catch { }
            }
            throw;
        }
    }

    public async Task<string> ResignApkAsync(string apkPath, string keystorePath, string password)
    {
        if (!File.Exists(apkPath))
            throw new FileNotFoundException("APK file not found", apkPath);
        
        if (!File.Exists(keystorePath))
            throw new FileNotFoundException("Keystore file not found", keystorePath);

        var workingDir = Path.Combine(_tempPath, Path.GetFileNameWithoutExtension(apkPath));
        var outputPath = Path.Combine(
            Path.GetDirectoryName(apkPath)!,
            $"{Path.GetFileNameWithoutExtension(apkPath)}_signed.apk");

        try
        {
            // Clean up any existing working directory
            if (Directory.Exists(workingDir))
                Directory.Delete(workingDir, true);
            Directory.CreateDirectory(workingDir);

            // Copy APK to working directory
            var workingApk = Path.Combine(workingDir, Path.GetFileName(apkPath));
            File.Copy(apkPath, workingApk);

            // Remove existing signature if present
            if (await IsApkSignedAsync(workingApk))
            {
                await RemoveExistingSignatureAsync(workingApk);
            }

            // Sign APK
#if !(IOS || MACCATALYST)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "jarsigner",
                    Arguments = $"-verbose -sigalg SHA1withRSA -digestalg SHA1 -keystore \"{keystorePath}\" -storepass {password} \"{workingApk}\" app_key",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            RunProcessAsync(process.StartInfo).Wait();
#endif

            // Move signed APK to output location
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            File.Move(workingApk, outputPath);

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to resign APK", ex);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workingDir))
                    Directory.Delete(workingDir, true);
            }
            catch { }
        }
    }

    private Task<bool> IsApkSignedAsync(string apkPath)
    {
        try
        {
#if !(IOS || MACCATALYST)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "jarsigner",
                    Arguments = $"-verify \"{apkPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            RunProcessAsync(process.StartInfo).Wait();
#endif
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private async Task RemoveExistingSignatureAsync(string apkPath)
    {
        var tempDir = Path.Combine(Path.GetDirectoryName(apkPath)!, "temp");
        try
        {
            Directory.CreateDirectory(tempDir);

            // Extract APK
            await Task.Run(() => ZipFile.ExtractToDirectory(apkPath, tempDir));

            // Remove signature files
            var metaInf = Path.Combine(tempDir, "META-INF");
            if (Directory.Exists(metaInf))
            {
                await Task.Run(() => Directory.Delete(metaInf, true));
            }

            // Delete original APK and create new one without signature
            await Task.Run(() =>
            {
                File.Delete(apkPath);
                ZipFile.CreateFromDirectory(tempDir, apkPath);
            });
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    await Task.Run(() => Directory.Delete(tempDir, true));
            }
            catch { }
        }
    }

    private Task RunProcessAsync(ProcessStartInfo startInfo)
    {
#if !(IOS || MACCATALYST)
        using var process = new Process { StartInfo = startInfo };
        
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Process failed with exit code {process.ExitCode}. Output:\n{output}");
        }
#endif
        return Task.CompletedTask;
    }
}
