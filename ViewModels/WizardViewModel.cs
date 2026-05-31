#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturaPDF.Models;
using FacturaPDF.Services;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FacturaPDF.ViewModels;

/// <summary>
/// ViewModel de nivel Senior para gestionar el flujo secuencial del asistente de primera ejecución.
/// </summary>
public partial class WizardViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNextStep1))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNextStep1))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFinish))]
    private string _sourceFolderPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFinish))]
    private string _outputFolderPath = string.Empty;

    [ObservableProperty]
    private string _customXsltPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep2Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep3Visible))]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    private int _currentStep = 1;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public WizardViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    // Propiedades de visibilidad de pasos calculadas de forma reactiva
    public bool IsStep1Visible => CurrentStep == 1;
    public bool IsStep2Visible => CurrentStep == 2;
    public bool IsStep3Visible => CurrentStep == 3;

    public string StepTitle => CurrentStep switch
    {
        1 => "Paso 1: Autenticación del Desarrollador",
        2 => "Paso 2: Selección de Rutas de Trabajo",
        3 => "Paso 3: Revisión e Inicialización",
        _ => "Configuración"
    };

    // Validadores reactivos para habilitar botones en el UI
    public bool CanGoNextStep1 => 
        !string.IsNullOrWhiteSpace(Email) && 
        Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") && 
        !string.IsNullOrWhiteSpace(Password) && 
        Password.Length >= 6;

    public bool CanFinish => 
        !string.IsNullOrWhiteSpace(SourceFolderPath) && 
        !string.IsNullOrWhiteSpace(OutputFolderPath);

    [RelayCommand]
    private void NextStep()
    {
        ErrorMessage = string.Empty;

        if (CurrentStep == 1)
        {
            if (!CanGoNextStep1)
            {
                ErrorMessage = "Por favor, introduce un correo válido y una contraseña de al menos 6 caracteres.";
                return;
            }
            CurrentStep = 2;
        }
        else if (CurrentStep == 2)
        {
            if (!CanFinish)
            {
                ErrorMessage = "Debes seleccionar tanto la carpeta de entrada (XML) como la de salida (PDF).";
                return;
            }
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        ErrorMessage = string.Empty;
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    private async Task SelectSourceFolderAsync()
    {
        try
        {
            string? selectedFolder = await PickFolderNativeAsync();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                SourceFolderPath = selectedFolder;
                ErrorMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar carpeta: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectOutputFolderAsync()
    {
        try
        {
            string? selectedFolder = await PickFolderNativeAsync();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                OutputFolderPath = selectedFolder;
                ErrorMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar carpeta: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltFileAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Selecciona una plantilla XSLT 1.0",
                FileTypes = new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".xslt", ".xsl" } }
                })
            };

            var result = await FilePicker.Default.PickAsync(options);
            if (result != null)
            {
                CustomXsltPath = result.FullPath;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar archivo XSLT: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task FinishConfigurationAsync()
    {
        if (!CanFinish) return;

        try
        {
            var config = new AppConfig(
                Email,
                SourceFolderPath,
                OutputFolderPath,
                CustomXsltPath,
                IsAutoMonitorActive: true // Monitoreo activo por defecto
            );

            // Guardamos la configuración y encriptamos la contraseña de forma segura
            await _configService.SaveConfigAsync(config, Password);

            // Navegamos al Dashboard Principal (MainPage) re-estableciendo la raíz de la app
            if (Application.Current?.Windows?[0] != null)
            {
                var mainPage = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(MainPage)) as Page;
                if (mainPage != null)
                {
                    Application.Current.Windows[0].Page = new NavigationPage(mainPage);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al guardar configuración: {ex.Message}";
        }
    }

    /// <summary>
    /// Método auxiliar para invocar el selector de carpetas nativo de Windows (WinUI 3).
    /// </summary>
    private async Task<string?> PickFolderNativeAsync()
    {
#if WINDOWS
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        
        // Asociar la ventana activa (HWND) de WinUI 3
        var activeWindow = App.Current?.Windows?[0];
        if (activeWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUIWindow)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(winUIWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        }
        
        folderPicker.FileTypeFilter.Add("*");
        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
#else
        // Respaldo asíncrono para otras plataformas no soportadas directamente
        await Task.CompletedTask;
        return null;
#endif
    }
}
