#nullable enable
using FacturaPDF.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Interfaz para el servicio gestor de procesamiento por lotes de archivos XML CFDI.
/// </summary>
public interface IInvoiceProcessorService
{
    /// <summary>
    /// Evento que se dispara cada vez que una factura termina de procesarse.
    /// Parámetros: Nombre del archivo, mensaje de error (vacío si tuvo éxito), y esExitoso.
    /// </summary>
    event Action<string, string, bool>? InvoiceProcessed;

    /// <summary>
    /// Evento que se dispara al iniciar un nuevo lote de procesamiento, indicando el número total de archivos XML a procesar.
    /// </summary>
    event Action<int>? BatchStarted;

    /// <summary>
    /// Evento que se dispara al concluir el lote de procesamiento completo.
    /// </summary>
    event Action? BatchCompleted;

    /// <summary>
    /// Procesa de forma asíncrona todos los archivos XML pendientes en la carpeta de entrada configurada.
    /// </summary>
    Task ProcessInvoicesAsync();

    /// <summary>
    /// Lee y deserializa todos los registros de errores recientes (.error.json) en la carpeta de errores.
    /// </summary>
    Task<List<ErrorLog>> GetRecentErrorsAsync();

    /// <summary>
    /// Mueve un archivo XML que falló de vuelta a la raíz de la carpeta de entrada para reintentar su procesamiento.
    /// </summary>
    /// <param name="fileName">Nombre del archivo XML (ej. "Factura_123.xml").</param>
    Task RetryFailedInvoiceAsync(string fileName);
}
