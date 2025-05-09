using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResignerTool.Services;
using FilePicker = Microsoft.Maui.Storage.FilePicker;
using FileResult = Microsoft.Maui.Storage.FileResult;

namespace ResignerTool.ViewModels;

public partial class ApkResignViewModel : ObservableObject
{
    private readonly ApkSignerService _signerService;
    private readonly SecurePromptService _securePrompt;
    private readonly ILogService _logger;

    [ObservableProperty] private string? apkFilePath;
    [ObservableProperty] private string? keystorePath;
    [ObservableProperty] private string? keystorePassword;
    [ObservableProperty] private string? keystoreAlias;
    [ObservableProperty] private string? keystoreCN;
    [ObservableProperty] private string? keystoreOrg;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isCreatingKeystore;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isReadyToResign;
    [ObservableProperty] private bool canGenerateKeystore;

    public ApkResignViewModel()
    {
        _signerService = new ApkSignerService();
        _securePrompt = new SecurePromptService();
        _logger = new FileLoggerService();
        IsReadyToResign = false;
        IsCreatingKeystore = false;
    }

    [RelayCommand]
    private async Task SelectApkAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select APK file",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "com.android.package-archive" } },
                        { DevicePlatform.Android, new[] { "application/vnd.android.package-archive" } },
                        { DevicePlatform.macOS, new[] { "apk" } },
                        { DevicePlatform.WinUI, new[] { ".apk" } }
                    })
            });

            if (result != null)
            {
                ApkFilePath = result.FullPath;
                StatusMessage = $"Selected APK: {Path.GetFileName(ApkFilePath)}";
                UpdateReadiness();
                _logger.LogInfo($"Selected APK: {ApkFilePath}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error selecting APK file.";
            _logger.LogError("Error selecting APK", ex);
        }
    }

    [RelayCommand]
    private async Task SelectKeystoreAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Keystore file",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "application/x-java-keystore" } },
                        { DevicePlatform.Android, new[] { "application/x-java-keystore" } },
                        { DevicePlatform.macOS, new[] { "keystore", "jks" } },
                        { DevicePlatform.WinUI, new[] { ".keystore", ".jks" } }
                    })
            });

            if (result != null)
            {
                KeystorePath = result.FullPath;

                var password = await _securePrompt.PromptForPassword(
                    "Keystore Password", 
                    "Enter the keystore password:");

                if (!string.IsNullOrEmpty(password))
                {
                    KeystorePassword = password;
                    if (await _signerService.ValidateKeystoreAsync(KeystorePath, KeystorePassword))
                    {
                        StatusMessage = "Keystore validated successfully.";
                        await SecureStorageService.SaveKeystorePasswordAsync(KeystorePath, KeystorePassword);
                        UpdateReadiness();
                    }
                    else
                    {
                        await _securePrompt.ShowError("Validation Failed", "Invalid keystore or password.");
                        KeystorePath = null;
                        KeystorePassword = null;
                    }
                }
                else
                {
                    KeystorePath = null;
                    StatusMessage = "Keystore password is required.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error selecting keystore.";
            _logger.LogError("Error selecting keystore", ex);
            await _securePrompt.ShowError("Error", "Failed to select or validate keystore.");
        }
    }

    [RelayCommand]
    private void CreateKeystore()
    {
        IsCreatingKeystore = true;
        KeystoreAlias = "app_signing_key";
        KeystoreCN = "Unknown";
        KeystoreOrg = "Organization";
        StatusMessage = "Please fill in the keystore details.";
        UpdateCanGenerateKeystore();
    }

    [RelayCommand]
    private async Task GenerateKeystoreAsync()
    {
        if (!CanGenerateKeystore) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Generating keystore...";

            var saveDialog = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Save Keystore As",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.macOS, new[] { "keystore" } },
                        { DevicePlatform.WinUI, new[] { ".keystore" } }
                    })
            });

            if (saveDialog != null)
            {
                var keystoreFile = await _signerService.CreateKeystoreAsync(KeystorePassword!);
                KeystorePath = keystoreFile;
                await SecureStorageService.SaveKeystorePasswordAsync(KeystorePath, KeystorePassword!);
                
                StatusMessage = "Keystore generated successfully.";
                IsCreatingKeystore = false;
                UpdateReadiness();
                
                await _securePrompt.ShowConfirmation(
                    "Success",
                    "Would you like to proceed with signing the APK?");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error generating keystore.";
            _logger.LogError("Error generating keystore", ex);
            await _securePrompt.ShowError("Error", "Failed to generate keystore.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResignApkAsync()
    {
        if (!IsReadyToResign) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Signing APK...";

            string outputPath = await _signerService.ResignApkAsync(
                ApkFilePath!,
                KeystorePath!,
                KeystorePassword!);

            StatusMessage = $"APK signed successfully: {Path.GetFileName(outputPath)}";
            _logger.LogInfo($"APK resigned to: {outputPath}");

            var openFolder = await _securePrompt.ShowConfirmation(
                "Success",
                "APK signed successfully. Would you like to open the containing folder?");

            if (openFolder)
            {
#if !(IOS || MACCATALYST)
                try
                {
                    Process.Start("open", $"\"{Path.GetDirectoryName(outputPath)}\"");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to open output folder", ex);
                }
#endif
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error signing APK.";
            _logger.LogError("Signing failed", ex);
            await _securePrompt.ShowError("Error", $"Failed to resign APK: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateReadiness()
    {
        IsReadyToResign = !string.IsNullOrWhiteSpace(ApkFilePath) &&
                         !string.IsNullOrWhiteSpace(KeystorePath) &&
                         !string.IsNullOrWhiteSpace(KeystorePassword);
    }

    private void UpdateCanGenerateKeystore()
    {
        CanGenerateKeystore = !string.IsNullOrWhiteSpace(KeystoreAlias) &&
                             !string.IsNullOrWhiteSpace(KeystorePassword) &&
                             !string.IsNullOrWhiteSpace(KeystoreCN) &&
                             !string.IsNullOrWhiteSpace(KeystoreOrg);
    }

    partial void OnKeystoreAliasChanged(string? value) => UpdateCanGenerateKeystore();
    partial void OnKeystoreCNChanged(string? value) => UpdateCanGenerateKeystore();
    partial void OnKeystoreOrgChanged(string? value) => UpdateCanGenerateKeystore();
    partial void OnKeystorePasswordChanged(string? value) => UpdateCanGenerateKeystore();
}
