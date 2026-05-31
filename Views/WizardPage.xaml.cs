#nullable enable
using FacturaPDF.ViewModels;
using Microsoft.Maui.Controls;

namespace FacturaPDF.Views;

/// <summary>
/// Código subyacente para la vista WizardPage.xaml.
/// </summary>
public partial class WizardPage : ContentPage
{
    /// <summary>
    /// Constructor de la página del asistente inicial de configuración.
    /// Inyecta el ViewModel correspondiente.
    /// </summary>
    /// <param name="viewModel">Instancia inyectada de WizardViewModel.</param>
    public WizardPage(WizardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
