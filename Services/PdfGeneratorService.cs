#nullable enable
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace FacturaPDF.Services;

/// <summary>
/// Implementación concreta de IPdfGeneratorService usando XslCompiledTransform para
/// procesamiento XSLT 1.0 y el control nativo WebView2 en Windows para la generación de PDF.
/// </summary>
public class PdfGeneratorService : IPdfGeneratorService
{
    private WebView? _webView;

    /// <inheritdoc />
    public void RegisterWebView(WebView webView)
    {
        _webView = webView;
    }

    /// <inheritdoc />
    public async Task GeneratePdfAsync(string xmlContent, string? customXsltPath, string outputPath)
    {
        if (_webView == null)
        {
            throw new InvalidOperationException("El control WebView de renderizado no ha sido registrado en el servicio.");
        }

        // 1. Obtener la plantilla XSLT 1.0
        string xsltContent;
        if (!string.IsNullOrEmpty(customXsltPath) && File.Exists(customXsltPath))
        {
            xsltContent = await File.ReadAllTextAsync(customXsltPath);
        }
        else
        {
            // Cargamos la plantilla por defecto integrada en los recursos Raw de la app
            using var stream = await FileSystem.OpenAppPackageFileAsync("default_cfdi.xslt");
            using var reader = new StreamReader(stream);
            xsltContent = await reader.ReadToEndAsync();
        }

        // 2. Realizar la transformación XSLT (XML + XSLT -> HTML)
        string htmlContent;
        try
        {
            using var xmlStringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(xmlStringReader);

            using var xsltStringReader = new StringReader(xsltContent);
            using var xsltReader = XmlReader.Create(xsltStringReader);

            var transform = new XslCompiledTransform();
            transform.Load(xsltReader);

            using var htmlWriter = new StringWriter();
            transform.Transform(xmlReader, null, htmlWriter);

            htmlContent = htmlWriter.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al aplicar la transformación XSLT: {ex.Message}", ex);
        }

        // 3. Renderizar e Imprimir a PDF en el Hilo de la UI principal
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();

            // Declaramos el manejador para saber cuándo finaliza la carga del HTML en WebView
            EventHandler<WebNavigatedEventArgs>? handler = null;
            handler = (sender, e) =>
            {
                _webView.Navigated -= handler;
                tcs.SetResult(true);
            };
            
            _webView.Navigated += handler;

            // Asignamos el código HTML al origen de datos del WebView
            _webView.Source = new HtmlWebViewSource { Html = htmlContent };

            // Esperamos a que finalice la carga
            await tcs.Task;

            // Margen de delay adicional para que WebView2 renderice fuentes externas (Google Fonts) y aplique CSS
            await Task.Delay(500);

            // Invocamos la API nativa de impresión off-screen de Windows WebView2
#if WINDOWS
            if (_webView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 nativeWebView)
            {
                // Aseguramos que el motor CoreWebView2 esté inicializado
                await nativeWebView.EnsureCoreWebView2Async();
                
                var coreWebView2 = nativeWebView.CoreWebView2;
                if (coreWebView2 == null)
                {
                    throw new InvalidOperationException("El motor CoreWebView2 nativo no pudo inicializarse.");
                }

                // Configuración de impresión nativa a PDF
                var printSettings = coreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true; // Mantiene la estética de fondo y tarjetas CSS
                printSettings.HeaderTitle = string.Empty;
                printSettings.FooterUri = string.Empty;
                printSettings.MarginTop = 0.4; // 1 cm aprox
                printSettings.MarginBottom = 0.4;
                printSettings.MarginLeft = 0.4;
                printSettings.MarginRight = 0.4;

                // Asegurar que la carpeta de destino exista
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Generación nativa del archivo PDF
                await coreWebView2.PrintToPdfAsync(outputPath, printSettings);
            }
            else
            {
                throw new InvalidOperationException("El control PlatformView de Windows no es del tipo esperado WebView2.");
            }
#else
            throw new PlatformNotSupportedException("La conversión nativa HTML a PDF vía WebView2 solo está implementada para Windows.");
#endif
        });
    }
}
