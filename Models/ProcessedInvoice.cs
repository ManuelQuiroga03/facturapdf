#nullable enable
using System;

namespace FacturaPDF.Models;

/// <summary>
/// Modelo para representar una factura XML procesada con éxito y su PDF resultante.
/// </summary>
public record ProcessedInvoice(
    string FileName,
    string PdfPath,
    string Timestamp,
    bool IsXmlAvailable = true,
    bool IsPdfAvailable = true
)
{
    public string XmlTooltip => IsXmlAvailable 
        ? "Abrir archivo XML procesado en el editor local" 
        : "El XML original ya no existe en la carpeta procesados (Eliminado o Archivado)";

    public string PdfTooltip => IsPdfAvailable 
        ? "Ver el archivo PDF generado en el visor local del sistema" 
        : "El PDF generado ya no existe en la carpeta de destino (Eliminado o Archivado)";

    // Colores optimizados y legibles para máxima visibilidad en temas claro/oscuro
    public string XmlIconColor => IsXmlAvailable ? "#3B82F6" : "#94A3B8";
    public string PdfIconColor => "#10B981"; // Mantener siempre el verde descriptivo

    // Opacidades adaptativas: 100% activo, 35% inactivo (visible pero deshabilitado)
    public double XmlIconOpacity => IsXmlAvailable ? 1.0 : 0.35;
    public double PdfIconOpacity => IsPdfAvailable ? 1.0 : 0.35;
}
