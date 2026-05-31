using Microsoft.Extensions.Logging;
using FacturaPDF.Services;

namespace FacturaPDF;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Registro de Servicios Globales (Patrón de Arquitectura Limpia)
		builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
		builder.Services.AddSingleton<IInvoiceHistoryService, InvoiceHistoryService>();
		builder.Services.AddSingleton<IPdfGeneratorService, PdfGeneratorService>();
		builder.Services.AddSingleton<IInvoiceProcessorService, InvoiceProcessorService>();
		builder.Services.AddSingleton<IInvoiceMonitorService, InvoiceMonitorService>();

		// Registro de Vistas y ViewModels (MVVM)
		builder.Services.AddTransient<FacturaPDF.Views.WizardPage>();
		builder.Services.AddTransient<FacturaPDF.ViewModels.WizardViewModel>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddTransient<FacturaPDF.ViewModels.MainViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
