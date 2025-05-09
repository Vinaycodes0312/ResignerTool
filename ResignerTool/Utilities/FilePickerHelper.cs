using Microsoft.Maui.Storage;
using ResignerTool.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResignerTool.Utilities;

public static class FilePickerHelper
{
    public static async Task<FileResult?> PickFileAsync(string title, params string[] fileTypes)
    {
#if MACCATALYST
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.MacCatalyst, fileTypes }
                })
        };
#else
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, fileTypes },
                    { DevicePlatform.Android, fileTypes },
                    { DevicePlatform.WinUI, fileTypes }
                })
        };
#endif
        try
        {
            var result = await FilePicker.Default.PickAsync(options);
            if (result != null)
            {
#if !MACCATALYST
                await SecureStorageService.StoreFileBookmark(result.FullPath);
#endif
                return result;
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
        }
        return null;
    }

    public static async Task<IEnumerable<FileResult>> PickMultipleFilesAsync(string title, params string[] fileTypes)
    {
#if MACCATALYST
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.MacCatalyst, fileTypes }
                })
        };
#else
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, fileTypes },
                    { DevicePlatform.Android, fileTypes },
                    { DevicePlatform.WinUI, fileTypes }
                })
        };
#endif
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(options);
            if (results != null && results.Any())
            {
#if !MACCATALYST
                foreach (var result in results)
                {
                    await SecureStorageService.StoreFileBookmark(result.FullPath);
                }
#endif
                return results;
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
        }
        return Array.Empty<FileResult>();
    }

    public static async Task<FileResult> PickFile(params string[] fileTypes)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.MacCatalyst, fileTypes },
                    { DevicePlatform.iOS, fileTypes }
                })
            };

            var result = await FilePicker.Default.PickAsync(options);
            return result!; // null-forgiving operator to suppress warning
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"File picking failed: {ex.Message}");
            return null!; // null-forgiving operator to suppress warning
        }
    }

    public static string? PickFolder()
    {
#if WINDOWS
        try
        {
            var result = FolderPicker.Default.PickAsync().GetAwaiter().GetResult();
            return result?.Folder?.Path;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Folder picking failed: {ex.Message}");
            return null;
        }
#else
        // Folder picking is not supported on this platform
        return null;
#endif
    }

    public static async Task<IEnumerable<FileResult>> PickMultipleFiles(params string[] fileTypes)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select Files",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.MacCatalyst, fileTypes },
                    { DevicePlatform.iOS, fileTypes }
                })
            };

            var result = await FilePicker.Default.PickMultipleAsync(options);
            return result ?? Array.Empty<FileResult>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Multiple file picking failed: {ex.Message}");
            return Array.Empty<FileResult>();
        }
    }
}