#nullable enable
using FacturaPDF.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Interfaz para el servicio de historial persistente de facturas.
/// </summary>
public interface IInvoiceHistoryService
{
    /// <summary>
    /// Agrega un nuevo registro al historial persistente.
    /// </summary>
    Task AddEntryAsync(ProcessedInvoiceEntry entry);

    /// <summary>
    /// Obtiene todos los registros del historial, ordenados por fecha descendente.
    /// </summary>
    Task<List<ProcessedInvoiceEntry>> GetHistoryAsync();

    /// <summary>
    /// Obtiene el número total de facturas procesadas de por vida.
    /// </summary>
    Task<int> GetLifetimeCountAsync();

    /// <summary>
    /// Limpia completamente el historial de por vida.
    /// </summary>
    Task ClearHistoryAsync();
}
