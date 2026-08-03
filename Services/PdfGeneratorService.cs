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
    private readonly IConfigurationService _configService;
    private readonly System.Threading.SemaphoreSlim _printSemaphore = new(1, 1);
    private WebView? _webView;

    public PdfGeneratorService(IConfigurationService configService)
    {
        _configService = configService;
    }

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

        // Cargar XML en XmlDocument para detectar tipo/complemento e inyectar el logotipo
        var xmlDoc = new XmlDocument();
        try
        {
            xmlDoc.LoadXml(xmlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al cargar el contenido XML: {ex.Message}", ex);
        }

        // Cargar configuración de la aplicación
        var config = await _configService.LoadConfigAsync();

        // 1. Determinar la plantilla XSLT a utilizar
        string xsltContent;
        string? resolvedXsltPath = customXsltPath;

        if (string.IsNullOrEmpty(resolvedXsltPath) && config != null)
        {
            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("cfdi", "http://www.sat.gob.mx/cfd/4");
            nsmgr.AddNamespace("cfdi3", "http://www.sat.gob.mx/cfd/3");
            nsmgr.AddNamespace("cce11", "http://www.sat.gob.mx/ComercioExterior11");
            nsmgr.AddNamespace("cce20", "http://www.sat.gob.mx/ComercioExterior20");
            nsmgr.AddNamespace("cartaporte20", "http://www.sat.gob.mx/CartaPorte20");
            nsmgr.AddNamespace("cartaporte30", "http://www.sat.gob.mx/CartaPorte30");
            nsmgr.AddNamespace("pago10", "http://www.sat.gob.mx/Pagos");
            nsmgr.AddNamespace("pago20", "http://www.sat.gob.mx/Pagos20");
            nsmgr.AddNamespace("nomina12", "http://www.sat.gob.mx/nomina12");

            if (xmlDoc.SelectSingleNode("//cce11:ComercioExterior", nsmgr) != null || xmlDoc.SelectSingleNode("//cce20:ComercioExterior", nsmgr) != null)
            {
                resolvedXsltPath = config.XsltComercioExteriorPath;
            }
            else if (xmlDoc.SelectSingleNode("//cartaporte20:CartaPorte", nsmgr) != null || xmlDoc.SelectSingleNode("//cartaporte30:CartaPorte", nsmgr) != null)
            {
                resolvedXsltPath = config.XsltCartaPortePath;
            }
            else if (xmlDoc.SelectSingleNode("//pago10:Pagos", nsmgr) != null || xmlDoc.SelectSingleNode("//pago20:Pagos", nsmgr) != null)
            {
                resolvedXsltPath = config.XsltPagoPath;
            }
            else if (xmlDoc.SelectSingleNode("//nomina12:Nomina", nsmgr) != null)
            {
                resolvedXsltPath = config.XsltNominaPath;
            }
            else
            {
                resolvedXsltPath = config.XsltIngresoPath;
            }

            // Fallback a Global
            if (string.IsNullOrEmpty(resolvedXsltPath))
            {
                resolvedXsltPath = config.CustomXsltPath;
            }
        }

        if (!string.IsNullOrEmpty(resolvedXsltPath) && File.Exists(resolvedXsltPath))
        {
            xsltContent = await File.ReadAllTextAsync(resolvedXsltPath);
        }
        else
        {
            // Cargamos la plantilla por defecto integrada en los recursos Raw de la app
            using var stream = await FileSystem.OpenAppPackageFileAsync("default_cfdi.xslt");
            using var reader = new StreamReader(stream);
            xsltContent = await reader.ReadToEndAsync();
        }

        // 2. Procesar logotipo a Base64 si está configurado
        string logoBase64 = string.Empty;
        if (config != null && !string.IsNullOrEmpty(config.LogoPath) && File.Exists(config.LogoPath))
        {
            try
            {
                byte[] imageBytes = await File.ReadAllBytesAsync(config.LogoPath);
                string mimeType = Path.GetExtension(config.LogoPath).ToLower() switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".svg" => "image/svg+xml",
                    _ => "image/png"
                };
                logoBase64 = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
            }
            catch (Exception)
            {
                // Grado Senior: Silenciamos errores de carga de logo para no romper el flujo principal
            }
        }

        // 3. Realizar la transformación XSLT (XML + XSLT -> HTML)
        string htmlContent;
        try
        {
            using var xmlStringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(xmlStringReader);

            using var xsltStringReader = new StringReader(xsltContent);
            using var xsltReader = XmlReader.Create(xsltStringReader);

            var transform = new XslCompiledTransform();
            transform.Load(xsltReader);

            var argsList = new XsltArgumentList();
            argsList.AddParam("logoBase64", "", logoBase64);

            using var htmlWriter = new StringWriter();
            transform.Transform(xmlReader, argsList, htmlWriter);

            htmlContent = htmlWriter.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al aplicar la transformación XSLT: {ex.Message}", ex);
        }

        // 3. Renderizar e Imprimir a PDF en el Hilo de la UI principal de forma serializada (Thread-Safe)
        await _printSemaphore.WaitAsync();
        try
        {
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
                await Task.Delay(100);

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
                    bool success = await coreWebView2.PrintToPdfAsync(outputPath, printSettings);
                    if (!success || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                    {
                        throw new FileNotFoundException("La generación del PDF nativo falló en el motor WebView2 o el archivo resultante está vacío.");
                    }
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
        finally
        {
            _printSemaphore.Release();
        }
    }
}
