#nullable enable
using System;

namespace FacturaPDF.Services;

/// <summary>
/// Interfaz para el servicio de monitoreo automático de carpetas en segundo plano.
/// </summary>
public interface IInvoiceMonitorService
{
    /// <summary>
    /// Evento lanzado cuando el estado de monitoreo cambia (Activo/Inactivo).
    /// </summary>
    event Action<bool>? MonitoringStateChanged;

    /// <summary>
    /// Indica si el servicio de monitoreo en segundo plano está actualmente activo.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Inicia el monitoreo automático en tiempo real (FileSystemWatcher) y redundante (PeriodicTimer).
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Detiene de forma limpia los temporizadores y observadores del sistema de archivos.
    /// </summary>
    void StopMonitoring();
}
