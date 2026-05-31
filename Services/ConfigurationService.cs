#nullable enable
using FacturaPDF.Models;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Implementación concreta del servicio de configuración utilizando las APIs nativas 
/// Preferences y SecureStorage de .NET MAUI.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private const string KeyEmail = "UserEmail";
    private const string KeySourceFolder = "SourceFolderPath";
    private const string KeyOutputFolder = "OutputFolderPath";
    private const string KeyCustomXslt = "CustomXsltPath";
    private const string KeyAutoMonitor = "IsAutoMonitorActive";
    private const string KeySecurePassword = "SecureUserPassword";

    /// <inheritdoc />
    public Task<AppConfig?> LoadConfigAsync()
    {
        try
        {
            var email = Preferences.Default.Get<string?>(KeyEmail, null);
            var sourceFolder = Preferences.Default.Get<string?>(KeySourceFolder, null);
            var outputFolder = Preferences.Default.Get<string?>(KeyOutputFolder, null);
            var customXslt = Preferences.Default.Get<string?>(KeyCustomXslt, null);
            var autoMonitor = Preferences.Default.Get<bool>(KeyAutoMonitor, false);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(outputFolder))
            {
                return Task.FromResult<AppConfig?>(null);
            }

            return Task.FromResult<AppConfig?>(new AppConfig(
                email, 
                sourceFolder, 
                outputFolder, 
                customXslt ?? string.Empty, 
                autoMonitor
            ));
        }
        catch (Exception)
        {
            // Nota de Grado Senior: En entornos productivos registraríamos la excepción mediante ILogger.
            return Task.FromResult<AppConfig?>(null);
        }
    }

    /// <inheritdoc />
    public async Task SaveConfigAsync(AppConfig config, string? password = null)
    {
        Preferences.Default.Set(KeyEmail, config.Email);
        Preferences.Default.Set(KeySourceFolder, config.SourceFolderPath);
        Preferences.Default.Set(KeyOutputFolder, config.OutputFolderPath);
        Preferences.Default.Set(KeyCustomXslt, config.CustomXsltPath);
        Preferences.Default.Set(KeyAutoMonitor, config.IsAutoMonitorActive);

        if (!string.IsNullOrEmpty(password))
        {
            await SecureStorage.Default.SetAsync(KeySecurePassword, password);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetPasswordAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(KeySecurePassword);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Task<bool> IsConfiguredAsync()
    {
        var hasEmail = Preferences.Default.ContainsKey(KeyEmail);
        var hasSource = Preferences.Default.ContainsKey(KeySourceFolder);
        var hasOutput = Preferences.Default.ContainsKey(KeyOutputFolder);

        return Task.FromResult(hasEmail && hasSource && hasOutput);
    }

    /// <inheritdoc />
    public async Task ClearConfigAsync()
    {
        Preferences.Default.Remove(KeyEmail);
        Preferences.Default.Remove(KeySourceFolder);
        Preferences.Default.Remove(KeyOutputFolder);
        Preferences.Default.Remove(KeyCustomXslt);
        Preferences.Default.Remove(KeyAutoMonitor);

        SecureStorage.Default.Remove(KeySecurePassword);
        await Task.CompletedTask;
    }
}
