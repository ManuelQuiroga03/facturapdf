#nullable enable
using FacturaPDF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Implementación concreta del gestor de facturación por lotes con carpetas segregadas,
/// persistencia de logs en JSON y gestión de reintentos.
/// </summary>
public class InvoiceProcessorService : IInvoiceProcessorService
{
    private readonly IConfigurationService _configService;
    private readonly IPdfGeneratorService _pdfGeneratorService;
    private readonly IInvoiceHistoryService _historyService;

    /// <inheritdoc />
    public event Action<string, string, bool>? InvoiceProcessed;

    /// <inheritdoc />
    public event Action<int>? BatchStarted;

    /// <inheritdoc />
    public event Action? BatchCompleted;

    public InvoiceProcessorService(
        IConfigurationService configService, 
        IPdfGeneratorService pdfGeneratorService,
        IInvoiceHistoryService historyService)
    {
        _configService = configService;
        _pdfGeneratorService = pdfGeneratorService;
        _historyService = historyService;
    }

    /// <inheritdoc />
    public async Task ProcessInvoicesAsync()
    {
        var config = await _configService.LoadConfigAsync();
        if (config == null) return;

        // Validar que la carpeta de entrada exista
        if (!Directory.Exists(config.SourceFolderPath))
        {
            throw new DirectoryNotFoundException($"La carpeta de entrada no existe: {config.SourceFolderPath}");
        }

        // Crear las carpetas automatizadas internas
        var processedDir = Path.Combine(config.SourceFolderPath, "Procesados");
        var errorDir = Path.Combine(config.SourceFolderPath, "Errores");
        Directory.CreateDirectory(processedDir);
        Directory.CreateDirectory(errorDir);

        // Obtener todos los archivos XML en el directorio raíz (evitando carpetas internas)
        var xmlFiles = Directory.GetFiles(config.SourceFolderPath, "*.xml", SearchOption.TopDirectoryOnly);
        
        // Filtrar archivos que ya tienen su archivo PDF generado en la salida (evitar reprocesamientos innecesarios)
        var filteredFiles = new List<string>();
        foreach (var filePath in xmlFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var pdfFileName = Path.ChangeExtension(fileName, ".pdf");
            var pdfOutputPath = Path.Combine(config.OutputFolderPath, pdfFileName);
            if (!File.Exists(pdfOutputPath))
            {
                filteredFiles.Add(filePath);
            }
        }

        if (filteredFiles.Count == 0) return;

        // Disparar inicio del lote con el conteo de archivos a procesar
        BatchStarted?.Invoke(filteredFiles.Count);

        var tasks = new List<Task>();
        // SemaphoreSlim controlado a 4 hilos paralelos para no sobrecargar el renderizado WebView2
        using var semaphore = new System.Threading.SemaphoreSlim(4, 4);

        foreach (var filePath in filteredFiles)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await ProcessSingleInvoiceWithRetryAsync(filePath, processedDir, errorDir, config);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        
        // Disparar conclusión del lote
        BatchCompleted?.Invoke();
    }

    /// <summary>
    /// Procesa un único comprobante de manera aislada y robusta.
    /// </summary>
    private async Task ProcessSingleInvoiceWithRetryAsync(string filePath, string processedDir, string errorDir, AppConfig config)
    {
        var fileName = Path.GetFileName(filePath);
        var pdfFileName = Path.ChangeExtension(fileName, ".pdf");
        var pdfOutputPath = Path.Combine(config.OutputFolderPath, pdfFileName);

        long fileSize = 0;
        try
        {
            // Intentar leer el XML con reintentos para evitar colisiones de bloqueo
            string xmlContent = await ReadAllTextWithRetryAsync(filePath);

            try
            {
                if (File.Exists(filePath))
                {
                    fileSize = new FileInfo(filePath).Length;
                }
            }
            catch { }

            // Generar PDF usando el motor WebView2
            await _pdfGeneratorService.GeneratePdfAsync(xmlContent, config.CustomXsltPath, pdfOutputPath);

            // Mover el XML original a la carpeta de Procesados de forma segura (sincronizada localmente)
            var destPath = Path.Combine(processedDir, fileName);
            lock (this)
            {
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                if (File.Exists(filePath))
                {
                    File.Move(filePath, destPath);
                }
            }

            // Registrar en el historial persistente
            await _historyService.AddEntryAsync(new ProcessedInvoiceEntry(
                FileName: fileName,
                SourcePath: filePath,
                PdfPath: pdfOutputPath,
                ProcessedAt: DateTime.Now,
                Status: "Success",
                ErrorMessage: null,
                FileSize: fileSize
            ));

            // Disparar evento de éxito para la interfaz
            InvoiceProcessed?.Invoke(fileName, string.Empty, true);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    fileSize = new FileInfo(filePath).Length;
                }
            }
            catch { }

            // Mover el XML original a la carpeta de Errores
            var destPath = Path.Combine(errorDir, fileName);
            lock (this)
            {
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                if (File.Exists(filePath))
                {
                    File.Move(filePath, destPath);
                }
            }

            // Crear Log Detallado del Error en Formato JSON
            var errorLog = new ErrorLog(
                FileName: fileName,
                Timestamp: DateTime.Now,
                ErrorMessage: ex.Message,
                ExceptionType: ex.GetType().Name,
                XsltApplied: string.IsNullOrEmpty(config.CustomXsltPath) ? "default_cfdi.xslt" : Path.GetFileName(config.CustomXsltPath)
            );

            var jsonPath = Path.Combine(errorDir, fileName + ".error.json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var jsonContent = JsonSerializer.Serialize(errorLog, jsonOptions);
            try
            {
                await File.WriteAllTextAsync(jsonPath, jsonContent);
            }
            catch { }

            // Registrar el error en el historial
            await _historyService.AddEntryAsync(new ProcessedInvoiceEntry(
                FileName: fileName,
                SourcePath: filePath,
                PdfPath: pdfOutputPath,
                ProcessedAt: DateTime.Now,
                Status: "Error",
                ErrorMessage: ex.Message,
                FileSize: fileSize
            ));

            // Disparar evento de error para actualizar la UI en tiempo real
            InvoiceProcessed?.Invoke(fileName, ex.Message, false);
        }
    }

    /// <summary>
    /// Intenta leer un archivo de texto con reintentos utilizando retraso exponencial si el archivo está bloqueado.
    /// </summary>
    private async Task<string> ReadAllTextWithRetryAsync(string filePath, int maxRetries = 5, int initialDelayMs = 100)
    {
        int retries = 0;
        while (true)
        {
            try
            {
                // Intentamos abrir el archivo con acceso exclusivo de lectura para verificar que se terminó de escribir
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                return await reader.ReadToEndAsync();
            }
            catch (IOException) when (retries < maxRetries)
            {
                retries++;
                // Retraso exponencial: 100ms, 200ms, 400ms, 800ms, 1600ms
                int delay = initialDelayMs * (int)Math.Pow(2, retries - 1);
                await Task.Delay(delay);
            }
        }
    }

    /// <inheritdoc />
    public async Task<List<ErrorLog>> GetRecentErrorsAsync()
    {
        var errorLogs = new List<ErrorLog>();
        
        var config = await _configService.LoadConfigAsync();
        if (config == null) return errorLogs;

        var errorDir = Path.Combine(config.SourceFolderPath, "Errores");
        if (!Directory.Exists(errorDir)) return errorLogs;

        // Leer todos los archivos .error.json
        var jsonFiles = Directory.GetFiles(errorDir, "*.error.json");
        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(jsonFile);
                var log = JsonSerializer.Deserialize<ErrorLog>(jsonContent);
                if (log != null)
                {
                    errorLogs.Add(log);
                }
            }
            catch (Exception)
            {
                // Si un log individual está corrupto, se omite silenciosamente para no bloquear la app
            }
        }

        return errorLogs;
    }

    /// <inheritdoc />
    public async Task RetryFailedInvoiceAsync(string fileName)
    {
        var config = await _configService.LoadConfigAsync();
        if (config == null) return;

        var sourcePath = Path.Combine(config.SourceFolderPath, "Errores", fileName);
        var destPath = Path.Combine(config.SourceFolderPath, fileName);

        if (File.Exists(sourcePath))
        {
            // 1. Eliminar el PDF correspondiente si ya existe en la salida, para forzar su regeneración
            var pdfFileName = Path.ChangeExtension(fileName, ".pdf");
            var pdfOutputPath = Path.Combine(config.OutputFolderPath, pdfFileName);
            if (File.Exists(pdfOutputPath))
            {
                try
                {
                    File.Delete(pdfOutputPath);
                }
                catch { }
            }

            // 2. Mover el XML fallido de vuelta a la raíz de entrada para su reprocesamiento
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            File.Move(sourcePath, destPath);

            // 3. Eliminar el archivo de log JSON asociado
            var jsonPath = sourcePath + ".error.json";
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }

            // 4. Limpiar la entrada anterior de error en la bitácora histórica
            await _historyService.RemoveEntryAsync(fileName);
        }
    }
}
