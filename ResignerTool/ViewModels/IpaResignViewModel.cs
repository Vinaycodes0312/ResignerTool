#if !(IOS || MACCATALYST)
using System.Diagnostics;
#endif
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResignerTool.Services;
using FilePicker = Microsoft.Maui.Storage.FilePicker;

namespace ResignerTool.ViewModels;

public partial class IpaResignViewModel : ObservableObject
{
    private readonly IpaSignerService _signerService;
    private readonly ILogService _logger;

    [ObservableProperty] private string? ipaFilePath;
    [ObservableProperty] private string? certificateName;
    [ObservableProperty] private string? certificateStatus;
    [ObservableProperty] private string? provisioningProfilePath;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isReadyToResign;

    public IpaResignViewModel()
    {
        _signerService = new IpaSignerService();
        _logger = new FileLoggerService();
        IsReadyToResign = false;
    }

    [RelayCommand]
    private async Task SelectIpaAsync()
    {
        if (IsBusy) return;

        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select IPA file",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "com.apple.ios-app" } },
                        { DevicePlatform.macOS, new[] { "ipa" } },
                        { DevicePlatform.WinUI, new[] { ".ipa" } }
                    })
            });

            if (result != null)
            {
                IpaFilePath = result.FullPath;
                StatusMessage = $"Selected IPA: {Path.GetFileName(IpaFilePath)}";
                UpdateReadiness();
                _logger.LogInfo($"Selected IPA: {IpaFilePath}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error selecting IPA file.";
            _logger.LogError("Error selecting IPA", ex);
        }
    }

    [RelayCommand]
    private async Task ValidateCertificateAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            var result = await _signerService.ValidateSigningCertificateAsync();
            if (result != null)
            {
                CertificateName = result;
                CertificateStatus = "Valid";
                StatusMessage = $"Found valid certificate: {CertificateName}";
                _logger.LogInfo($"Validated certificate: {CertificateName}");
                UpdateReadiness();
            }
            else
            {
                CertificateName = null;
                CertificateStatus = "Not Found";
                StatusMessage = "No valid signing certificate found";
                _logger.LogWarning("No valid signing certificate found");
            }
        }
        catch (Exception ex)
        {
            CertificateName = null;
            CertificateStatus = "Error";
            StatusMessage = "Error validating certificate";
            _logger.LogError("Certificate validation failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectProvisioningProfileAsync()
    {
        if (IsBusy) return;

        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Provisioning Profile",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "com.apple.mobileprovision" } },
                        { DevicePlatform.macOS, new[] { "mobileprovision" } },
                        { DevicePlatform.WinUI, new[] { ".mobileprovision" } }
                    })
            });

            if (result != null)
            {
                ProvisioningProfilePath = result.FullPath;
                StatusMessage = $"Selected profile: {Path.GetFileName(ProvisioningProfilePath)}";
                _logger.LogInfo($"Selected provisioning profile: {ProvisioningProfilePath}");
                UpdateReadiness();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error selecting provisioning profile";
            _logger.LogError("Error selecting provisioning profile", ex);
        }
    }

    [RelayCommand]
    private async Task ResignIpaAsync()
    {
        if (!IsReadyToResign || IsBusy) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Resigning IPA...";

            var outputPath = await _signerService.ResignIpaAsync(
                IpaFilePath ?? throw new InvalidOperationException("IPA file path is null"),
                ProvisioningProfilePath ?? throw new InvalidOperationException("Provisioning profile path is null"));

            if (outputPath != null)
            {
                StatusMessage = $"IPA resigned successfully: {Path.GetFileName(outputPath)}";
                _logger.LogInfo($"IPA resigned successfully: {outputPath}");

                bool openFolder = false;
                if (Application.Current?.MainPage != null)
                {
                    openFolder = await Application.Current.MainPage.DisplayAlert(
                        "Success",
                        "IPA resigned successfully. Would you like to open the containing folder?",
                        "Yes",
                        "No");
                }

                if (openFolder)
                {
#if !(IOS || MACCATALYST)
                    try
                    {
                        var folderPath = Path.GetDirectoryName(outputPath);
                        if (folderPath != null)
                        {
                            Process.Start("open", $"\"{folderPath}\"");
                            _logger.LogInfo($"Opened output folder: {folderPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to open output folder", ex);
                    }
#endif
                }
            }
            else
            {
                StatusMessage = "Failed to resign IPA";
                _logger.LogError("Resign operation failed - no output file produced");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resigning IPA: {ex.Message}";
            _logger.LogError("IPA resign failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateReadiness()
    {
        IsReadyToResign = !string.IsNullOrWhiteSpace(IpaFilePath) &&
                         !string.IsNullOrWhiteSpace(CertificateName) &&
                         !string.IsNullOrWhiteSpace(ProvisioningProfilePath);
    }

    partial void OnCertificateNameChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            CertificateStatus = "Click Validate to verify certificate";
        }
        UpdateReadiness();
    }
}