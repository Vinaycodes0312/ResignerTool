using Microsoft.Maui.Controls;
using ResignerTool.Services;

namespace ResignerTool.Services;

public class SecurePromptService
{
    private readonly ILogService _logger;

    public SecurePromptService()
    {
        _logger = new FileLoggerService();
    }

    public async Task<string?> PromptForPassword(string title, string message = "Enter password:")
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null)
            {
                _logger.LogError("Cannot show password prompt - no active page");
                return null;
            }

            return await page.DisplayPromptAsync(
                title,
                message,
                "OK",
                "Cancel",
                "Password",
                maxLength: 50,
                keyboard: Keyboard.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to show password prompt", ex);
            return null;
        }
    }

    public async Task<bool> ShowConfirmation(string title, string message)
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null)
            {
                _logger.LogError("Cannot show confirmation - no active page");
                return false;
            }

            return await page.DisplayAlert(
                title,
                message,
                "Yes",
                "No");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to show confirmation dialog", ex);
            return false;
        }
    }

    public async Task<bool> ConfirmIteration()
    {
        return await ShowConfirmation("Continue", "Continue to iterate?");
    }

    public async Task ShowError(string title, string message)
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null)
            {
                _logger.LogError("Cannot show error - no active page");
                return;
            }

            await page.DisplayAlert(
                title,
                message,
                "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to show error dialog", ex);
        }
    }
}
