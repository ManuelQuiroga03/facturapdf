#nullable enable
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Interfaz para el servicio de transformación de CFDI XML a PDF nativo usando XSLT 1.0 y WebView2.
/// </summary>
public interface IPdfGeneratorService
{
    /// <summary>
    /// Registra el control WebView enlazado a la UI principal para realizar el renderizado y la impresión nativa.
    /// </summary>
    /// <param name="webView">El control WebView de la interfaz.</param>
    void RegisterWebView(WebView webView);

    /// <summary>
    /// Transforma el contenido XML de un CFDI usando XSLT y lo guarda como un archivo PDF.
    /// </summary>
    /// <param name="xmlContent">Cadena de texto con el XML del CFDI.</param>
    /// <param name="customXsltPath">Ruta al archivo XSLT personalizado (si está configurada; nulo para usar la integrada).</param>
    /// <param name="outputPath">Ruta completa del archivo PDF a generar.</param>
    Task GeneratePdfAsync(string xmlContent, string? customXsltPath, string outputPath);
}
