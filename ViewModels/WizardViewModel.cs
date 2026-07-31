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
    private string _logoPath = string.Empty;

    [ObservableProperty]
    private bool _useTemplatePerType = false;

    [ObservableProperty]
    private string _xsltIngresoPath = string.Empty;

    [ObservableProperty]
    private string _xsltCartaPortePath = string.Empty;

    [ObservableProperty]
    private string _xsltPagoPath = string.Empty;

    [ObservableProperty]
    private string _xsltNominaPath = string.Empty;

    [ObservableProperty]
    private string _xsltComercioExteriorPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep2Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep3Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep4Visible))]
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
    public bool IsStep4Visible => CurrentStep == 4;

    public string StepTitle => CurrentStep switch
    {
        1 => "Paso 1: Autenticación del Desarrollador",
        2 => "Paso 2: Rutas y Logotipo de la Empresa",
        3 => "Paso 3: Configuración de Plantilla PDF",
        4 => "Paso 4: Revisión e Inicialización",
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
    private async Task NextStepAsync()
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
        else if (CurrentStep == 3)
        {
            // Validar si el usuario no ha especificado ninguna ruta de plantilla (Global ni por Comprobante)
            bool hasAnyTemplate = !string.IsNullOrWhiteSpace(CustomXsltPath) ||
                                  !string.IsNullOrWhiteSpace(XsltIngresoPath) ||
                                  !string.IsNullOrWhiteSpace(XsltCartaPortePath) ||
                                  !string.IsNullOrWhiteSpace(XsltPagoPath) ||
                                  !string.IsNullOrWhiteSpace(XsltNominaPath) ||
                                  !string.IsNullOrWhiteSpace(XsltComercioExteriorPath);

            if (!hasAnyTemplate)
            {
                bool proceed = false;
                var activePage = Application.Current?.Windows?[0]?.Page;
                if (activePage != null)
                {
                    proceed = await activePage.DisplayAlert(
                        "Aviso de Plantilla Predeterminada",
                        "No has seleccionado ninguna plantilla personalizada. Se utilizará la plantilla predeterminada de la aplicación (default_cfdi.xslt) para procesar todos los comprobantes. ¿Deseas continuar?",
                        "Continuar",
                        "Configurar Plantilla");
                }

                if (!proceed)
                {
                    return; // Permanecer en el Paso 3
                }
            }
            CurrentStep = 4;
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
    private async Task SelectLogoAsync()
    {
        try
        {
            var result = await PickImageFileAsync("Selecciona la imagen del logotipo");
            if (result != null)
            {
                LogoPath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar logotipo: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltFileAsync()
    {
        try
        {
            var result = await PickXsltFileAsync("Selecciona una plantilla XSLT 1.0 Global");
            if (result != null)
            {
                CustomXsltPath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar plantilla global: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltIngresoAsync()
    {
        try
        {
            var result = await PickXsltFileAsync("Selecciona plantilla para Facturas de Ingreso");
            if (result != null)
            {
                XsltIngresoPath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar plantilla: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltCartaPorteAsync()
    {
        try
        {
            var result = await PickXsltFileAsync("Selecciona plantilla para Carta Porte / Traslado");
            if (result != null)
            {
                XsltCartaPortePath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar plantilla: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltPagoAsync()
    {
        try
        {
            var result = await PickXsltFileAsync("Selecciona plantilla para Complementos de Pago");
            if (result != null)
            {
                XsltPagoPath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar plantilla: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltNominaAsync()
    {
        try
        {
            var result = await PickXsltFileAsync("Selecciona plantilla para Recibos de Nómina");
            if (result != null)
            {
                XsltNominaPath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar plantilla: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectXsltComercioExteriorAsync()
    {
        try
        {
            var result = await PickXsltFileAsync("Selecciona plantilla para Comercio Exterior");
            if (result != null)
            {
                XsltComercioExteriorPath = result;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al seleccionar plantilla: {ex.Message}";
        }
    }

    private async Task<string?> PickXsltFileAsync(string title)
    {
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".xslt", ".xsl" } }
            })
        };
        var result = await FilePicker.Default.PickAsync(options);
        return result?.FullPath;
    }

    private async Task<string?> PickImageFileAsync(string title)
    {
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = FilePickerFileType.Images
        };
        var result = await FilePicker.Default.PickAsync(options);
        return result?.FullPath;
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
                IsAutoMonitorActive: true, // Monitoreo activo por defecto
                LogoPath,
                UseTemplatePerType,
                XsltIngresoPath,
                XsltCartaPortePath,
                XsltPagoPath,
                XsltNominaPath,
                XsltComercioExteriorPath
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
