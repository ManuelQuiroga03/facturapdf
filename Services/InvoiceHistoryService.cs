#nullable enable
using FacturaPDF.Models;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Implementación concreta del servicio de historial persistente basada en JSON.
/// Utiliza un SemaphoreSlim para evitar colisiones de concurrencia al procesar múltiples archivos.
/// </summary>
public class InvoiceHistoryService : IInvoiceHistoryService
{
    private static readonly string HistoryFilePath = Path.Combine(FileSystem.AppDataDirectory, "processed_history.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <inheritdoc />
    public async Task AddEntryAsync(ProcessedInvoiceEntry entry)
    {
        await _semaphore.WaitAsync();
        try
        {
            var history = await LoadHistoryInternalAsync();
            history.Add(entry);
            await SaveHistoryInternalAsync(history);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<ProcessedInvoiceEntry>> GetHistoryAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var history = await LoadHistoryInternalAsync();
            // Ordenar por fecha descendente (lo más nuevo al principio)
            history.Sort((a, b) => b.ProcessedAt.CompareTo(a.ProcessedAt));
            return history;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> GetLifetimeCountAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var history = await LoadHistoryInternalAsync();
            return history.Count;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearHistoryAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (File.Exists(HistoryFilePath))
            {
                File.Delete(HistoryFilePath);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Carga el historial desde el archivo local sin bloqueos de concurrencia externos.
    /// </summary>
    private async Task<List<ProcessedInvoiceEntry>> LoadHistoryInternalAsync()
    {
        if (!File.Exists(HistoryFilePath))
        {
            return new List<ProcessedInvoiceEntry>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(HistoryFilePath);
            var history = JsonSerializer.Deserialize<List<ProcessedInvoiceEntry>>(json, _jsonOptions);
            return history ?? new List<ProcessedInvoiceEntry>();
        }
        catch (Exception)
        {
            // En entornos reales se registraría en ILogger. Si está corrupto, empezamos de cero.
            return new List<ProcessedInvoiceEntry>();
        }
    }

    /// <summary>
    /// Guarda el historial en el archivo local sin bloqueos de concurrencia externos.
    /// </summary>
    private async Task SaveHistoryInternalAsync(List<ProcessedInvoiceEntry> history)
    {
        try
        {
            // Asegurar que el directorio AppData exista
            var directory = Path.GetDirectoryName(HistoryFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(history, _jsonOptions);
            await File.WriteAllTextAsync(HistoryFilePath, json);
        }
        catch (Exception)
        {
            // Ignorar para evitar detener el flujo de conversión principal si hay fallos de I/O de disco
        }
    }
}
