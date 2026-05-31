#nullable enable

namespace FacturaPDF.Models;

/// <summary>
/// Modelo inmutable de transferencia de datos para representar la configuración de la aplicación.
/// </summary>
public record AppConfig(
    string Email,
    string SourceFolderPath,
    string OutputFolderPath,
    string CustomXsltPath,
    bool IsAutoMonitorActive
);
