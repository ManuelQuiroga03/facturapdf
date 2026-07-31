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
    private const string KeyLogo = "LogoPath";
    private const string KeyUseTemplatePerType = "UseTemplatePerType";
    private const string KeyXsltIngreso = "XsltIngresoPath";
    private const string KeyXsltCartaPorte = "XsltCartaPortePath";
    private const string KeyXsltPago = "XsltPagoPath";
    private const string KeyXsltNomina = "XsltNominaPath";
    private const string KeyXsltComercioExterior = "XsltComercioExteriorPath";

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
            var logo = Preferences.Default.Get<string?>(KeyLogo, null) ?? string.Empty;
            var useTemplatePerType = Preferences.Default.Get<bool>(KeyUseTemplatePerType, false);
            var xsltIngreso = Preferences.Default.Get<string?>(KeyXsltIngreso, null) ?? string.Empty;
            var xsltCartaPorte = Preferences.Default.Get<string?>(KeyXsltCartaPorte, null) ?? string.Empty;
            var xsltPago = Preferences.Default.Get<string?>(KeyXsltPago, null) ?? string.Empty;
            var xsltNomina = Preferences.Default.Get<string?>(KeyXsltNomina, null) ?? string.Empty;
            var xsltComercioExterior = Preferences.Default.Get<string?>(KeyXsltComercioExterior, null) ?? string.Empty;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(outputFolder))
            {
                return Task.FromResult<AppConfig?>(null);
            }

            return Task.FromResult<AppConfig?>(new AppConfig(
                email, 
                sourceFolder, 
                outputFolder, 
                customXslt ?? string.Empty, 
                autoMonitor,
                logo,
                useTemplatePerType,
                xsltIngreso,
                xsltCartaPorte,
                xsltPago,
                xsltNomina,
                xsltComercioExterior
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
        Preferences.Default.Set(KeyLogo, config.LogoPath);
        Preferences.Default.Set(KeyUseTemplatePerType, config.UseTemplatePerType);
        Preferences.Default.Set(KeyXsltIngreso, config.XsltIngresoPath);
        Preferences.Default.Set(KeyXsltCartaPorte, config.XsltCartaPortePath);
        Preferences.Default.Set(KeyXsltPago, config.XsltPagoPath);
        Preferences.Default.Set(KeyXsltNomina, config.XsltNominaPath);
        Preferences.Default.Set(KeyXsltComercioExterior, config.XsltComercioExteriorPath);

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
        Preferences.Default.Remove(KeyLogo);
        Preferences.Default.Remove(KeyUseTemplatePerType);
        Preferences.Default.Remove(KeyXsltIngreso);
        Preferences.Default.Remove(KeyXsltCartaPorte);
        Preferences.Default.Remove(KeyXsltPago);
        Preferences.Default.Remove(KeyXsltNomina);
        Preferences.Default.Remove(KeyXsltComercioExterior);

        SecureStorage.Default.Remove(KeySecurePassword);
        await Task.CompletedTask;
    }
}
