using System.Security.Cryptography;
#if MACCATALYST
using Foundation;
using Security;
#endif

namespace ResignerTool.Services;

public static class SecureStorageService
{
    private const string BookmarkPrefix = "file_bookmark_";

#if !MACCATALYST
    private static readonly Dictionary<string, NSUrl> ActiveBookmarks = new();

    public static async Task StoreFileBookmark(string filePath)
    {
        var url = NSUrl.FromFilename(filePath);
        if (url == null) return;

        var bookmarkKey = GetBookmarkKey(filePath);
        
        try
        {
            NSError error;
#pragma warning disable CA1416
            var bookmarkData = url.CreateBookmarkData(NSUrlBookmarkCreationOptions.WithSecurityScope, 
                Array.Empty<string>(), null, out error);
#pragma warning restore CA1416

            if (error != null)
            {
                Console.WriteLine($"Error creating bookmark: {error.LocalizedDescription}");
                return;
            }

            await SecureStorage.Default.SetAsync(bookmarkKey, 
                Convert.ToBase64String(bookmarkData.ToArray()));
            
            // Keep the URL in memory while the app is running
            ActiveBookmarks[filePath] = url;
            
            // Start accessing the security-scoped resource
            url.StartAccessingSecurityScopedResource();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing bookmark: {ex}");
        }
    }

    public static async Task<bool> AccessSecureFile(string filePath)
    {
        if (ActiveBookmarks.ContainsKey(filePath))
        {
            return ActiveBookmarks[filePath].StartAccessingSecurityScopedResource();
        }

        var bookmarkKey = GetBookmarkKey(filePath);
        var bookmarkBase64 = await SecureStorage.Default.GetAsync(bookmarkKey);
        
        if (string.IsNullOrEmpty(bookmarkBase64))
            return false;

        try
        {
            var bookmarkData = NSData.FromArray(Convert.FromBase64String(bookmarkBase64));
            NSError error;
            bool isStale;
            
#pragma warning disable CA1416
            var url = new NSUrl(bookmarkData, NSUrlBookmarkResolutionOptions.WithSecurityScope,
                null, out isStale, out error);
#pragma warning restore CA1416

            if (error != null || url == null)
            {
                Console.WriteLine($"Error resolving bookmark: {error?.LocalizedDescription}");
                return false;
            }

            if (isStale)
            {
                await StoreFileBookmark(filePath);
            }

            ActiveBookmarks[filePath] = url;
            return url.StartAccessingSecurityScopedResource();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accessing secure file: {ex}");
            return false;
        }
    }

    public static void StopAccessingSecureFile(string filePath)
    {
        if (ActiveBookmarks.TryGetValue(filePath, out var url))
        {
            url.StopAccessingSecurityScopedResource();
        }
    }

    private static string GetBookmarkKey(string filePath)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(filePath));
        return BookmarkPrefix + Convert.ToBase64String(hashBytes);
    }
#endif

    public static async Task StoreSecureValue(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to store secure value: {ex.Message}", "OK");
        }
    }

    public static async Task<string?> GetSecureValue(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task SaveKeystorePasswordAsync(string keystorePath, string password)
    {
        await StoreSecureValue($"keystore_password_{keystorePath}", password);
    }
}