using System.Diagnostics;
using System.IO.Compression;


namespace ResignerTool.Services;

public class IpaSignerService
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "ipa-resign");
    private readonly ResignerTool.Services.ILogService _logger;

    public IpaSignerService()
    {
        _logger = new FileLoggerService();
        Directory.CreateDirectory(_tempPath);
    }

    public Task<string?> ValidateSigningCertificateAsync()
    {
#if !(IOS || MACCATALYST)
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "security",
                    Arguments = "find-identity -v -p codesigning",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var output = RunProcessAsync(process.StartInfo).Result;
            var lines = output.Split('\n');
            var certLine = lines.FirstOrDefault(l => l.Contains("iPhone Developer") || l.Contains("Apple Development"));
            
            if (certLine != null)
            {
                var parts = certLine.Split('"');
                return Task.FromResult(parts.Length >= 2 ? parts[1] : null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to validate signing certificate", ex);
        }
#endif
        return Task.FromResult<string?>(null);
    }

    public async Task<string?> ResignIpaAsync(string ipaPath, string provisioningProfilePath)
    {
        if (!File.Exists(ipaPath))
            throw new FileNotFoundException("IPA file not found", ipaPath);
        
        if (!File.Exists(provisioningProfilePath))
            throw new FileNotFoundException("Provisioning profile not found", provisioningProfilePath);

        var workingDir = Path.Combine(_tempPath, Path.GetFileNameWithoutExtension(ipaPath));
        var unzipDir = Path.Combine(workingDir, "unzip");
        var outputPath = Path.Combine(
            Path.GetDirectoryName(ipaPath)!,
            $"{Path.GetFileNameWithoutExtension(ipaPath)}_resigned.ipa");

        try
        {
            // Clean up any existing directories
            if (Directory.Exists(workingDir))
                Directory.Delete(workingDir, true);
            Directory.CreateDirectory(workingDir);
            Directory.CreateDirectory(unzipDir);

            // Unzip IPA
            using (var archive = ZipFile.OpenRead(ipaPath))
            {
                archive.ExtractToDirectory(unzipDir);
            }

            // Find .app directory
            var appPath = Directory.GetDirectories(unzipDir)
                .FirstOrDefault(d => d.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) 
                ?? throw new InvalidOperationException("Could not find .app directory in IPA");

            // Copy provisioning profile
            var embeddedProvisioningPath = Path.Combine(appPath, "embedded.mobileprovision");
            File.Copy(provisioningProfilePath, embeddedProvisioningPath, true);

            // Extract signing identity from provisioning profile
            var signingIdentity = await GetSigningIdentityFromProfileAsync(provisioningProfilePath);
            if (string.IsNullOrEmpty(signingIdentity))
                throw new InvalidOperationException("Could not extract signing identity from provisioning profile");

            // Sign the app
#if !(IOS || MACCATALYST)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "codesign",
                    Arguments = $"--force --sign \"{signingIdentity}\" --entitlements \"{embeddedProvisioningPath}\" \"{appPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            await RunProcessAsync(process.StartInfo);
#endif

            // Create new IPA
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            ZipFile.CreateFromDirectory(unzipDir, outputPath);

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to resign IPA", ex);
            return null;
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

    private Task<string?> GetSigningIdentityFromProfileAsync(string profilePath)
    {
#if !(IOS || MACCATALYST)
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "security",
                    Arguments = $"cms -D -i \"{profilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var output = RunProcessAsync(process.StartInfo).Result;
            // Extract signing identity from plist output
            // This is a simplified example - in practice you would want to properly parse the plist
            var lines = output.Split('\n');
            var certLine = lines.FirstOrDefault(l => l.Contains("<key>TeamIdentifier</key>"));
            if (certLine != null)
            {
                var valueIndex = Array.IndexOf(lines, certLine) + 1;
                if (valueIndex < lines.Length)
                {
                    var value = lines[valueIndex].Trim();
                    return Task.FromResult(value.Replace("<string>", "").Replace("</string>", ""));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to extract signing identity from profile", ex);
        }
#endif
        return Task.FromResult<string?>(null);
    }

    private Task<string> RunProcessAsync(ProcessStartInfo startInfo)
    {
#if !(IOS || MACCATALYST)
        try
        {
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

            return Task.FromResult(output.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to run process", ex);
        }
#endif
        return Task.FromResult(string.Empty);
    }
}
