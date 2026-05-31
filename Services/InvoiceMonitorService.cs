#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Implementación concreta del servicio de monitoreo en segundo plano. 
/// Combina FileSystemWatcher para respuesta inmediata en tiempo real y PeriodicTimer 
/// para redundancia periódica cada 5 minutos de forma segura y sin bloqueos de hilos.
/// </summary>
public class InvoiceMonitorService : IInvoiceMonitorService, IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly IInvoiceProcessorService _invoiceProcessorService;
    
    private FileSystemWatcher? _fileWatcher;
    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private bool _isProcessing; // Evita ejecuciones concurrentes encabalgadas
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <inheritdoc />
    public event Action<bool>? MonitoringStateChanged;

    /// <inheritdoc />
    public bool IsMonitoring { get; private set; }

    public InvoiceMonitorService(IConfigurationService configService, IInvoiceProcessorService invoiceProcessorService)
    {
        _configService = configService;
        _invoiceProcessorService = invoiceProcessorService;
    }

    /// <inheritdoc />
    public void StartMonitoring()
    {
        if (IsMonitoring) return;

        // Ejecución asíncrona segura
        Task.Run(async () =>
        {
            try
            {
                var config = await _configService.LoadConfigAsync();
                if (config == null || !Directory.Exists(config.SourceFolderPath)) return;

                _cts = new CancellationTokenSource();

                // 1. Configurar FileSystemWatcher para tiempo real
                _fileWatcher = new FileSystemWatcher
                {
                    Path = config.SourceFolderPath,
                    Filter = "*.xml",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += OnNewFileDetected;
                _fileWatcher.Changed += OnNewFileDetected;

                // 2. Configurar PeriodicTimer para redundancia de 5 minutos
                _periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
                _ = StartPeriodicTimerLoopAsync(_cts.Token);

                IsMonitoring = true;
                MonitoringStateChanged?.Invoke(true);

                // Ejecutamos un primer escaneo manual para procesar acumulados
                _ = TriggerProcessingAsync();
            }
            catch (Exception)
            {
                StopMonitoring();
            }
        });
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        if (!IsMonitoring) return;

        IsMonitoring = false;
        MonitoringStateChanged?.Invoke(false);

        // Cancelar el token del timer
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // Limpiar el FileSystemWatcher
        if (_fileWatcher != null)
        {
            _fileWatcher.Created -= OnNewFileDetected;
            _fileWatcher.Changed -= OnNewFileDetected;
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        // Limpiar el Timer
        _periodicTimer?.Dispose();
        _periodicTimer = null;
    }

    /// <summary>
    /// Bucle en segundo plano del PeriodicTimer de .NET.
    /// </summary>
    private async Task StartPeriodicTimerLoopAsync(CancellationToken cancellationToken)
    {
        while (_periodicTimer != null && await _periodicTimer.WaitForNextTickAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            await TriggerProcessingAsync();
        }
    }

    /// <summary>
    /// Evento disparado cuando FileSystemWatcher detecta actividad en la carpeta de entrada.
    /// </summary>
    private void OnNewFileDetected(object sender, FileSystemEventArgs e)
    {
        // Nota Senior: Cuando se crea un archivo, el SO a veces lo mantiene bloqueado por unos milisegundos.
        // Esperamos 1 segundo antes de disparar el procesamiento para evitar excepciones de bloqueo ("File In Use").
        Task.Run(async () =>
        {
            await Task.Delay(1000);
            await TriggerProcessingAsync();
        });
    }

    /// <summary>
    /// Método centralizado para disparar el procesamiento de facturas con control de concurrencia.
    /// </summary>
    private async Task TriggerProcessingAsync()
    {
        if (_isProcessing) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_isProcessing) return;
            _isProcessing = true;

            // Invocar al procesador por lotes
            await _invoiceProcessorService.ProcessInvoicesAsync();
        }
        catch (Exception)
        {
            // Nota: El procesador ya maneja internamente sus excepciones y las guarda en JSON
        }
        finally
        {
            _isProcessing = false;
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        _semaphore.Dispose();
    }
}
