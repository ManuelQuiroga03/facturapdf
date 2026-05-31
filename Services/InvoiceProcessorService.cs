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

        foreach (var filePath in xmlFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var pdfFileName = Path.ChangeExtension(fileName, ".pdf");
            var pdfOutputPath = Path.Combine(config.OutputFolderPath, pdfFileName);

            // ESTRATEGIA B: Si el archivo PDF ya existe en la salida, lo omitimos para evitar re-procesamiento
            if (File.Exists(pdfOutputPath))
            {
                continue;
            }

            try
            {
                var xmlContent = await File.ReadAllTextAsync(filePath);

                // Obtener tamaño antes de mover
                long fileSize = 0;
                try
                {
                    fileSize = new FileInfo(filePath).Length;
                }
                catch { }

                // Generar PDF usando el motor WebView2
                await _pdfGeneratorService.GeneratePdfAsync(xmlContent, config.CustomXsltPath, pdfOutputPath);

                // ÉXITO: MOVER el XML original a la carpeta de Procesados
                var destPath = Path.Combine(processedDir, fileName);
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Move(filePath, destPath);

                // Registrar en el historial de forma persistente
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
                // Obtener tamaño antes de mover
                long fileSize = 0;
                try
                {
                    if (File.Exists(filePath))
                    {
                        fileSize = new FileInfo(filePath).Length;
                    }
                }
                catch { }

                // FALLO: Mover el XML original a la carpeta de Errores
                var destPath = Path.Combine(errorDir, fileName);
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Move(filePath, destPath);

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
                await File.WriteAllTextAsync(jsonPath, jsonContent);

                // Registrar el error en el historial de forma persistente
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
            // Mover el XML fallido de vuelta a la raíz de entrada para su reprocesamiento
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            File.Move(sourcePath, destPath);

            // Eliminar el archivo de log JSON asociado
            var jsonPath = sourcePath + ".error.json";
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }
        await Task.CompletedTask;
    }
}
