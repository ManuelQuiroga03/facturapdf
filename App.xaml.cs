#nullable enable
using FacturaPDF.Services;
using FacturaPDF.Views;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace FacturaPDF;

public partial class App : Application
{
	private readonly IConfigurationService _configService;
	private readonly IServiceProvider _serviceProvider;

	public App(IConfigurationService configService, IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_configService = configService;
		_serviceProvider = serviceProvider;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Comprobamos de forma segura si la aplicación ya está configurada
		bool isConfigured = _configService.IsConfiguredAsync().GetAwaiter().GetResult();

		Page initialPage;
		if (isConfigured)
		{
			initialPage = new NavigationPage(_serviceProvider.GetRequiredService<MainPage>());
		}
		else
		{
			// Cargamos el Asistente de Configuración Inicial desde el contenedor de DI
			initialPage = new NavigationPage(_serviceProvider.GetRequiredService<WizardPage>());
		}

		return new Window(initialPage);
	}
}