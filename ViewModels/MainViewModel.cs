#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacturaPDF.Models;
using FacturaPDF.Services;
using FacturaPDF.Views;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace FacturaPDF.ViewModels;

/// <summary>
/// ViewModel de nivel Senior para administrar el Dashboard principal,
/// la monitorización en segundo plano y el histórico de observabilidad.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly IInvoiceProcessorService _invoiceProcessorService;
    private readonly IInvoiceMonitorService _invoiceMonitorService;
    private readonly IInvoiceHistoryService _invoiceHistoryService;
    private readonly IPdfGeneratorService _pdfGeneratorService;

    [ObservableProperty]
    private AppConfig? _config;

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InProgressEvents))]
    private bool _isProcessing;

    public int InProgressEvents => IsProcessing ? 1 : 0;

    [ObservableProperty]
    private string _statusMessage = "Sistema listo.";

    [ObservableProperty]
    private bool _isDashboardViewActive = true;

    [ObservableProperty]
    private bool _isLogViewActive = false;

    [ObservableProperty]
    private bool _isTestViewActive = false;

    [ObservableProperty]
    private int _totalEvents = 12482;

    [ObservableProperty]
    private string _uptime = "99.9%";

    [ObservableProperty]
    private string _testXmlPath = string.Empty;

    [ObservableProperty]
    private string _testXsltPath = string.Empty;

    [ObservableProperty]
    private string _testOutputPath = string.Empty;

    [ObservableProperty]
    private string _testLogoPath = string.Empty;

    [ObservableProperty]
    private string _testConsoleOutput = "Consola lista para depuración.";

    [ObservableProperty]
    private string _testConsoleStatus = "LISTO"; // LISTO, PROCESANDO, ÉXITO, ERROR

    [ObservableProperty]
    private bool _isTestConsoleSuccess = false;

    [ObservableProperty]
    private bool _isCustomPopupVisible = false;

    [ObservableProperty]
    private string _customPopupTitle = string.Empty;

    [ObservableProperty]
    private string _customPopupMessage = string.Empty;

    [ObservableProperty]
    private string _customPopupType = "INFO"; // SUCCESS, ERROR, INFO

    [ObservableProperty]
    private bool _isBatchActive = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchProgressText))]
    private int _batchTotalCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchProgressText))]
    private int _batchProcessedCount = 0;

    [ObservableProperty]
    private double _batchProgress = 0.0;

    public string BatchProgressText => $"{BatchProcessedCount} de {BatchTotalCount} procesados";

    public ObservableCollection<ProcessedInvoice> ProcessingHistory { get; } = new();
    public ObservableCollection<ErrorLog> RecentErrors { get; } = new();

    public MainViewModel(
        IConfigurationService configService,
        IInvoiceProcessorService invoiceProcessorService,
        IInvoiceMonitorService invoiceMonitorService,
        IInvoiceHistoryService invoiceHistoryService,
        IPdfGeneratorService pdfGeneratorService)
    {
        _configService = configService;
        _invoiceProcessorService = invoiceProcessorService;
        _invoiceMonitorService = invoiceMonitorService;
        _invoiceHistoryService = invoiceHistoryService;
        _pdfGeneratorService = pdfGeneratorService;

        // Suscribirse a eventos de observabilidad y estado
        _invoiceProcessorService.InvoiceProcessed += OnInvoiceProcessed;
        _invoiceProcessorService.BatchStarted += OnBatchStarted;
        _invoiceProcessorService.BatchCompleted += OnBatchCompleted;
        _invoiceMonitorService.MonitoringStateChanged += OnMonitoringStateChanged;
        ResetTestConsoleState();
    }

    partial void OnTestXmlPathChanged(string value) => ResetTestConsoleState();
    partial void OnTestXsltPathChanged(string value) => ResetTestConsoleState();
    partial void OnTestOutputPathChanged(string value) => ResetTestConsoleState();

    private void ResetTestConsoleState()
    {
        IsTestConsoleSuccess = false;
        TestConsoleStatus = "LISTO";
        TestConsoleOutput = "Consola lista para depuración. Selecciona los archivos y presiona 'Generar PDF de Prueba'.";
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        Config = await _configService.LoadConfigAsync();
        if (Config == null) return;

        IsMonitoring = _invoiceMonitorService.IsMonitoring;
        StatusMessage = IsMonitoring ? "Monitoreo automático activo." : "Sistema listo.";

        // Iniciar el monitoreo automáticamente si está guardado en la configuración
        if (Config.IsAutoMonitorActive && !IsMonitoring)
        {
            _invoiceMonitorService.StartMonitoring();
        }

        await LoadErrorsAsync();
        await RefreshDirectoryStatsAsync();
    }

    [RelayCommand]
    private async Task ProcessNowAsync()
    {
        if (IsProcessing) return;

        IsProcessing = true;
        StatusMessage = "Buscando y procesando facturas por lotes...";
        try
        {
            await _invoiceProcessorService.ProcessInvoicesAsync();
            StatusMessage = "Procesamiento por lotes finalizado con éxito.";
            await LoadErrorsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error durante el lote: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Método reactivo parcial de MvvmToolkit que responde de forma inmediata
    /// a los cambios del Switch en la interfaz de usuario.
    /// </summary>
    partial void OnIsMonitoringChanged(bool value)
    {
        // Evitar rebotes y bucles recursivos con el servicio de monitoreo
        if (_invoiceMonitorService.IsMonitoring != value)
        {
            if (value)
            {
                _invoiceMonitorService.StartMonitoring();
                StatusMessage = "Monitoreo automático iniciado.";
            }
            else
            {
                _invoiceMonitorService.StopMonitoring();
                StatusMessage = "Monitoreo en segundo plano desactivado.";
            }
        }

        // Guardar el estado preferido de monitoreo en el archivo de configuración del usuario
        if (Config != null && Config.IsAutoMonitorActive != value)
        {
            var updatedConfig = Config with { IsAutoMonitorActive = value };
            Config = updatedConfig; // Sincronizar localmente en memoria
            Task.Run(async () => await _configService.SaveConfigAsync(updatedConfig));
        }
    }

    [RelayCommand]
    private async Task RetryErrorAsync(ErrorLog error)
    {
        if (error == null) return;

        StatusMessage = $"Reintentando procesar {error.FileName}...";
        try
        {
            // Mover de vuelta a la raíz y borrar del historial anterior
            await _invoiceProcessorService.RetryFailedInvoiceAsync(error.FileName);
            
            // Forzar el escaneo
            await _invoiceProcessorService.ProcessInvoicesAsync();
            
            // Verificar si el archivo volvió a fallar
            var config = await _configService.LoadConfigAsync();
            if (config != null)
            {
                var errorFilePath = Path.Combine(config.SourceFolderPath, "Errores", error.FileName);
                if (File.Exists(errorFilePath))
                {
                    // Volvió a fallar, obtener detalles
                    var errorLogPath = errorFilePath + ".error.json";
                    string details = "El XML contiene errores de estructura o XSLT.";
                    if (File.Exists(errorLogPath))
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(errorLogPath);
                            var log = JsonSerializer.Deserialize<ErrorLog>(json);
                            if (log != null)
                            {
                                details = log.ErrorMessage;
                            }
                        }
                        catch {}
                    }
                    await ShowAlertAsync("Fallo al Reprocesar", $"No ha sido posible procesar '{error.FileName}' ya que contiene errores que debes revisar:\n\n{details}", "ERROR");
                }
                else
                {
                    StatusMessage = $"¡Reintento exitoso para {error.FileName}!";
                    await ShowAlertAsync("Reprocesamiento Exitoso", $"El archivo '{error.FileName}' ha sido procesado correctamente y su PDF ha sido generado.", "SUCCESS");
                }
            }

            await LoadErrorsAsync();
            await RefreshDirectoryStatsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fallo al reintentar: {ex.Message}";
            await ShowAlertAsync("Error de Reintento", $"Fallo al reprocesar '{error.FileName}': {ex.Message}", "ERROR");
        }
    }

    [RelayCommand]
    private async Task RetrySelectedErrorsAsync()
    {
        var selectedErrors = RecentErrors.Where(e => e.IsSelected).ToList();
        if (selectedErrors.Count == 0)
        {
            await ShowAlertAsync("Atención", "No hay facturas seleccionadas para reprocesar.", "INFO");
            return;
        }

        StatusMessage = $"Reintentando procesar {selectedErrors.Count} facturas seleccionadas...";
        try
        {
            foreach (var error in selectedErrors)
            {
                await _invoiceProcessorService.RetryFailedInvoiceAsync(error.FileName);
            }

            // Forzar el escaneo inmediato de todos los archivos retribuidos
            await _invoiceProcessorService.ProcessInvoicesAsync();

            // Verificar cuáles volvieron a fallar
            var config = await _configService.LoadConfigAsync();
            var failedAgain = new List<string>();
            if (config != null)
            {
                foreach (var error in selectedErrors)
                {
                    var errorFilePath = Path.Combine(config.SourceFolderPath, "Errores", error.FileName);
                    if (File.Exists(errorFilePath))
                    {
                        failedAgain.Add(error.FileName);
                    }
                }
            }

            if (failedAgain.Count > 0)
            {
                string list = string.Join("\n• ", failedAgain);
                await ShowAlertAsync("Reprocesamiento Incompleto", 
                    $"De las facturas reintentadas, las siguientes {failedAgain.Count} volvieron a fallar y requieren revisión:\n\n• {list}", "ERROR");
            }
            else
            {
                StatusMessage = $"Reintento masivo completado con éxito para {selectedErrors.Count} facturas.";
                await ShowAlertAsync("Reprocesamiento Masivo Exitoso", $"Todas las {selectedErrors.Count} facturas seleccionadas fueron procesadas y sus archivos PDF generados con éxito.", "SUCCESS");
            }

            await LoadErrorsAsync();
            await RefreshDirectoryStatsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error en reintento masivo: {ex.Message}";
            await ShowAlertAsync("Error de Sistema", $"Fallo en reintento masivo: {ex.Message}", "ERROR");
        }
    }

    [RelayCommand]
    private void CloseCustomPopup()
    {
        IsCustomPopupVisible = false;
    }

    public void ShowNotification(string title, string message, string type = "INFO")
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            CustomPopupTitle = title;
            CustomPopupMessage = message;
            CustomPopupType = type.ToUpper();
            IsCustomPopupVisible = true;
        });
    }

    private Task ShowAlertAsync(string title, string message, string type = "INFO")
    {
        ShowNotification(title, message, type);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenFolder(string type)
    {
        if (Config == null) return;

        string? folderPath = type switch
        {
            "Input" => Config.SourceFolderPath,
            "Output" => Config.OutputFolderPath,
            "Errors" => Path.Combine(Config.SourceFolderPath, "Errores"),
            _ => null
        };

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = "La carpeta seleccionada no existe o no ha sido creada.";
            return;
        }

        try
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
#endif
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo abrir el explorador: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetApplicationAsync()
    {
        bool confirm = false;
        var activePage = Application.Current?.Windows?[0]?.Page;
        if (activePage != null)
        {
            confirm = await activePage.DisplayAlert(
                "Reinicializar", 
                "¿Estás seguro de que deseas borrar toda la configuración y volver a iniciar el asistente?", 
                "Sí, Borrar todo", 
                "Cancelar");
        }

        if (confirm)
        {
            _invoiceMonitorService.StopMonitoring();
            await _configService.ClearConfigAsync();
            await _invoiceHistoryService.ClearHistoryAsync();

            if (Application.Current?.Windows?[0] != null)
            {
                // Instanciamos el Wizard de forma limpia resolviéndolo de DI
                var wizardPage = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(WizardPage)) as Page;
                if (wizardPage != null)
                {
                    Application.Current.Windows[0].Page = new NavigationPage(wizardPage);
                }
            }
        }
    }

    private async Task LoadErrorsAsync()
    {
        var errors = await _invoiceProcessorService.GetRecentErrorsAsync();
        
        // Ejecutamos en el hilo principal para actualizar la colección reactiva
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            RecentErrors.Clear();
            foreach (var err in errors)
            {
                RecentErrors.Add(err);
            }
        });
    }

    [RelayCommand]
    private async Task OpenPdfAsync(ProcessedInvoice invoice)
    {
        if (invoice == null || Config == null) return;

        try
        {
            if (File.Exists(invoice.PdfPath))
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(invoice.PdfPath)
                });
            }
            else
            {
                StatusMessage = $"El archivo PDF no se encuentra en: {invoice.PdfPath}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al abrir el PDF: {ex.Message}";
        }
    }

    private void OnInvoiceProcessed(string fileName, string errorMessage, bool isSuccess)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (IsBatchActive)
            {
                BatchProcessedCount++;
                if (BatchTotalCount > 0)
                {
                    BatchProgress = (double)BatchProcessedCount / BatchTotalCount;
                }
            }
            await RefreshDirectoryStatsAsync();
            await LoadErrorsAsync();
        });
    }

    private void OnBatchStarted(int totalCount)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            BatchTotalCount = totalCount;
            BatchProcessedCount = 0;
            BatchProgress = 0.0;
            IsBatchActive = true;
            StatusMessage = $"Procesando lote de {totalCount} facturas...";
        });
    }

    private void OnBatchCompleted()
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            IsBatchActive = false;
            BatchProgress = 1.0;
            StatusMessage = "Procesamiento de lote finalizado.";
            // Pequeña pausa de visualización y reset del progreso
            await Task.Delay(2000);
            if (!IsBatchActive)
            {
                BatchProgress = 0.0;
            }
        });
    }

    private void OnMonitoringStateChanged(bool active)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            IsMonitoring = active;
        });
    }

    [RelayCommand]
    private void SwitchView(string viewName)
    {
        IsDashboardViewActive = viewName == "Dashboard";
        IsLogViewActive = viewName == "Log";
        IsTestViewActive = viewName == "Test";
    }

    [RelayCommand]
    private async Task SelectTestXmlAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Selecciona un archivo XML CFDI de prueba",
                FileTypes = new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".xml" } }
                })
            };
            var result = await FilePicker.Default.PickAsync(options);
            if (result != null)
            {
                TestXmlPath = result.FullPath;
                StatusMessage = "Archivo XML de prueba seleccionado.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al seleccionar XML: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectTestXsltAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Selecciona una plantilla XSLT de prueba",
                FileTypes = new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".xslt", ".xsl" } }
                })
            };
            var result = await FilePicker.Default.PickAsync(options);
            if (result != null)
            {
                TestXsltPath = result.FullPath;
                StatusMessage = "Plantilla XSLT de prueba seleccionada.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al seleccionar XSLT: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectTestOutputAsync()
    {
        try
        {
            var folder = await PickFolderNativeAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                TestOutputPath = Path.Combine(folder, "Factura_Prueba.pdf");
                StatusMessage = "Destino del PDF de prueba seleccionado.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al seleccionar ruta de salida: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenTestPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(TestOutputPath) || !File.Exists(TestOutputPath))
        {
            StatusMessage = "No hay ningún PDF de prueba generado para abrir.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = TestOutputPath,
                UseShellExecute = true
            });
            StatusMessage = "Archivo PDF abierto con éxito.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo abrir el PDF: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateTestPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(TestXmlPath) || !File.Exists(TestXmlPath))
        {
            StatusMessage = "Debes seleccionar un archivo XML de prueba válido.";
            TestConsoleStatus = "ERROR";
            TestConsoleOutput = "❌ ERROR: Archivo XML de prueba no seleccionado o no encontrado.";
            IsTestConsoleSuccess = false;
            await ShowAlertAsync("Archivo XML Requerido", "Debes seleccionar un archivo XML de prueba válido para poder realizar la transformación.");
            return;
        }

        if (string.IsNullOrWhiteSpace(TestOutputPath))
        {
            StatusMessage = "Debes seleccionar una ruta de destino para el PDF de prueba.";
            TestConsoleStatus = "ERROR";
            TestConsoleOutput = "❌ ERROR: Carpeta o ruta de destino para el PDF no seleccionada.";
            IsTestConsoleSuccess = false;
            await ShowAlertAsync("Ruta de Destino Requerida", "Debes seleccionar una ruta o carpeta de destino donde guardar el archivo PDF de prueba.");
            return;
        }

        StatusMessage = "Generando PDF de prueba...";
        TestConsoleStatus = "PROCESANDO";
        TestConsoleOutput = $"Iniciando procesamiento de prueba...\n" +
                            $"XML: {TestXmlPath}\n" +
                            $"XSLT: {(string.IsNullOrWhiteSpace(TestXsltPath) ? "Enrutamiento Automático" : TestXsltPath)}\n" +
                            $"Destino: {TestOutputPath}\n\n" +
                            $"Compilando y transformando...";
        IsTestConsoleSuccess = false;

        try
        {
            string xmlContent = await File.ReadAllTextAsync(TestXmlPath);
            string? xsltPath = string.IsNullOrWhiteSpace(TestXsltPath) ? null : TestXsltPath;

            await _pdfGeneratorService.GeneratePdfAsync(xmlContent, xsltPath, TestOutputPath);

            StatusMessage = $"¡PDF de prueba generado con éxito! Guardado en: {TestOutputPath}";
            TestConsoleStatus = "ÉXITO";
            TestConsoleOutput = $"✅ ¡PROCESAMIENTO CONCLUIDO CON ÉXITO!\n\n" +
                                $"Archivo generado: {TestOutputPath}\n" +
                                $"Fecha/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                                $"El PDF ha sido creado correctamente y está listo para visualizar.";
            IsTestConsoleSuccess = true;
        }
        catch (System.Xml.XmlException xmlEx)
        {
            StatusMessage = "Error de sintaxis en el archivo XML.";
            TestConsoleStatus = "ERROR";
            TestConsoleOutput = $"❌ ERROR DE SINTAXIS XML\n\n" +
                                $"Línea del error: {xmlEx.LineNumber}\n" +
                                $"Posición del error: {xmlEx.LinePosition}\n" +
                                $"Descripción: {xmlEx.Message}\n\n" +
                                $"Revisa que el archivo XML no contenga etiquetas mal cerradas, caracteres huérfanos o saltos de línea inválidos en los tags.";
            IsTestConsoleSuccess = false;
        }
        catch (System.Xml.Xsl.XsltException xsltEx)
        {
            StatusMessage = "Error en la plantilla XSLT.";
            TestConsoleStatus = "ERROR";
            TestConsoleOutput = $"❌ ERROR DE COMPILACIÓN XSLT\n\n" +
                                $"Línea del error: {xsltEx.LineNumber}\n" +
                                $"Posición del error: {xsltEx.LinePosition}\n" +
                                $"Descripción: {xsltEx.Message}\n\n" +
                                $"Revisa la sintaxis XPath, coincidencia de etiquetas o funciones XSLT 1.0 no soportadas en tu plantilla.";
            IsTestConsoleSuccess = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fallo al generar PDF: {ex.Message}";
            TestConsoleStatus = "ERROR";
            TestConsoleOutput = $"❌ ERROR GENERAL DEL MOTOR\n\n" +
                                $"Mensaje: {ex.Message}\n\n" +
                                $"Detalle Técnico: {ex.StackTrace}";
            IsTestConsoleSuccess = false;
        }
    }

    [RelayCommand]
    private async Task SelectSourceFolderAsync()
    {
        if (Config == null) return;
        try
        {
            string? selectedFolder = await PickFolderNativeAsync();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                var updatedConfig = Config with { SourceFolderPath = selectedFolder };
                Config = updatedConfig;
                await _configService.SaveConfigAsync(updatedConfig);
                StatusMessage = "Carpeta de entrada actualizada con éxito.";

                // Reiniciar el monitoreo si está activo para actualizar la ruta del FileSystemWatcher
                if (IsMonitoring)
                {
                    _invoiceMonitorService.StopMonitoring();
                    _invoiceMonitorService.StartMonitoring();
                }

                // Recargar estadísticas leyendo las carpetas reales
                await RefreshDirectoryStatsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al seleccionar carpeta: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectOutputFolderAsync()
    {
        if (Config == null) return;
        try
        {
            string? selectedFolder = await PickFolderNativeAsync();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                var updatedConfig = Config with { OutputFolderPath = selectedFolder };
                Config = updatedConfig;
                await _configService.SaveConfigAsync(updatedConfig);
                StatusMessage = "Carpeta de salida actualizada con éxito.";

                // Recargar estadísticas leyendo las carpetas reales
                await RefreshDirectoryStatsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al seleccionar carpeta: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenXmlAsync(ProcessedInvoice invoice)
    {
        if (invoice == null || Config == null) return;
        try
        {
            var xmlPath = Path.Combine(Config.SourceFolderPath, "Procesados", invoice.FileName);
            if (File.Exists(xmlPath))
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(xmlPath)
                });
            }
            else
            {
                StatusMessage = $"El archivo XML no se encuentra en: {xmlPath}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al abrir el XML: {ex.Message}";
        }
    }

    private async Task<string?> PickFolderNativeAsync()
    {
#if WINDOWS
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        
        var activeWindow = Microsoft.Maui.Controls.Application.Current?.Windows?[0];
        if (activeWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUIWindow)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(winUIWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        }
        
        folderPicker.FileTypeFilter.Add("*");
        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    private async Task RefreshDirectoryStatsAsync()
    {
        if (Config == null) return;

        try
        {
            var processedDir = Path.Combine(Config.SourceFolderPath, "Procesados");
            var errorDir = Path.Combine(Config.SourceFolderPath, "Errores");

            // Obtener el historial completo y el total histórico
            var dbHistory = await _invoiceHistoryService.GetHistoryAsync();
            var lifetimeCount = dbHistory.Count;

            int failedCount = 0;
            if (Directory.Exists(errorDir))
            {
                failedCount = Directory.GetFiles(errorDir, "*.xml").Length;
            }

            var processedUIList = new List<ProcessedInvoice>();
            foreach (var entry in dbHistory.Where(e => e.Status == "Success"))
            {
                var xmlPath = Path.Combine(processedDir, entry.FileName);
                bool isXmlAvailable = File.Exists(xmlPath);
                bool isPdfAvailable = File.Exists(entry.PdfPath);

                processedUIList.Add(new ProcessedInvoice(
                    FileName: entry.FileName,
                    PdfPath: entry.PdfPath,
                    Timestamp: entry.ProcessedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsXmlAvailable: isXmlAvailable,
                    IsPdfAvailable: isPdfAvailable
                ));
            }

            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                TotalEvents = lifetimeCount;
                
                double uptimePct = 100.0;
                if (TotalEvents > 0)
                {
                    uptimePct = ((double)(TotalEvents - failedCount) / TotalEvents) * 100.0;
                    if (uptimePct < 0) uptimePct = 0;
                }
                Uptime = $"{uptimePct:F1}%";

                // Cargar el historial en la UI (limitado a 50 items)
                ProcessingHistory.Clear();
                foreach (var item in processedUIList.Take(50))
                {
                    ProcessingHistory.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al escanear historial y carpetas: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearLifetimeHistoryAsync()
    {
        bool confirm = false;
        var activePage = Application.Current?.Windows?[0]?.Page;
        if (activePage != null)
        {
            confirm = await activePage.DisplayAlert(
                "Borrar Historial Histórico", 
                "¿Estás seguro de que deseas vaciar de forma permanente el registro histórico de facturas procesadas de por vida? Los archivos físicos no serán eliminados.", 
                "Sí, Vaciar", 
                "Cancelar");
        }

        if (confirm)
        {
            await _invoiceHistoryService.ClearHistoryAsync();
            StatusMessage = "Historial histórico vaciado.";
            await RefreshDirectoryStatsAsync();
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        ApplyTheme(IsDarkMode);
    }

    public void ApplyTheme(bool isDark)
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;

        Action<ResourceDictionary> applyToDict = (dict) =>
        {
            if (isDark)
            {
                dict["PageBg"] = Color.FromArgb("#0B0F19");
                dict["SidebarBg"] = Color.FromArgb("#111625");
                dict["CardBg"] = Color.FromArgb("#111625");
                dict["BorderColor"] = Color.FromArgb("#222D42");
                dict["PrimaryText"] = Color.FromArgb("#FFFFFF");
                dict["SecondaryText"] = Color.FromArgb("#94A3B8");
                dict["MutedText"] = Color.FromArgb("#64748B");
                dict["PanelBg"] = Color.FromArgb("#0B0F19");
                dict["HoverBg"] = Color.FromArgb("#1E293B");
                dict["ErrorRowBg"] = Color.FromArgb("#241416");
            }
            else
            {
                dict["PageBg"] = Color.FromArgb("#F3F4F6");
                dict["SidebarBg"] = Color.FromArgb("#FFFFFF");
                dict["CardBg"] = Color.FromArgb("#FFFFFF");
                dict["BorderColor"] = Color.FromArgb("#D1D5DB");
                dict["PrimaryText"] = Color.FromArgb("#111827");
                dict["SecondaryText"] = Color.FromArgb("#4B5563");
                dict["MutedText"] = Color.FromArgb("#9CA3AF");
                dict["PanelBg"] = Color.FromArgb("#F9FAFB");
                dict["HoverBg"] = Color.FromArgb("#E5E7EB");
                dict["ErrorRowBg"] = Color.FromArgb("#FEE2E2");
            }
        };

        // Aplicar a los recursos globales de la aplicación
        applyToDict(resources);

        // Aplicar a los recursos locales de la página activa para forzar el redibujado en caliente
        var activePage = Application.Current?.Windows?[0]?.Page;
        if (activePage != null)
        {
            applyToDict(activePage.Resources);
            
            if (activePage is NavigationPage navPage && navPage.CurrentPage != null)
            {
                applyToDict(navPage.CurrentPage.Resources);
            }
            else if (activePage is Shell shell && shell.CurrentPage != null)
            {
                applyToDict(shell.CurrentPage.Resources);
            }
        }
    }

    public void Dispose()
    {
        _invoiceProcessorService.InvoiceProcessed -= OnInvoiceProcessed;
        _invoiceProcessorService.BatchStarted -= OnBatchStarted;
        _invoiceProcessorService.BatchCompleted -= OnBatchCompleted;
        _invoiceMonitorService.MonitoringStateChanged -= OnMonitoringStateChanged;
    }
}
