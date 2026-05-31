#nullable enable
using FacturaPDF.Services;
using FacturaPDF.ViewModels;
using Microsoft.Maui.Controls;
using System;

namespace FacturaPDF;

/// <summary>
/// Código subyacente de la página principal (Dashboard). 
/// Enlaza el ViewModel y registra el control WebView invisible para la renderización de PDF.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IPdfGeneratorService _pdfGeneratorService;

    /// <summary>
    /// Constructor principal inyectado con dependencias.
    /// </summary>
    /// <param name="viewModel">El ViewModel del Dashboard.</param>
    /// <param name="pdfGeneratorService">El generador de PDF para registrar la WebView nativa.</param>
    public MainPage(MainViewModel viewModel, IPdfGeneratorService pdfGeneratorService)
    {
        InitializeComponent();
        
        _pdfGeneratorService = pdfGeneratorService;
        BindingContext = viewModel;

        // REGISTRO CLAVE DE ARQUITECTURA SENIOR: 
        // Vinculamos la WebView invisible de la interfaz con el motor de generación.
        // Esto garantiza que la WebView esté en el visual tree de Windows y cargue con éxito.
        _pdfGeneratorService.RegisterWebView(pdfWebView);
    }

    /// <summary>
    /// Disparado automáticamente cuando la página es visible para inicializar la carga asíncrona de datos.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
