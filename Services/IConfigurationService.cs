#nullable enable
using FacturaPDF.Models;
using System.Threading.Tasks;

namespace FacturaPDF.Services;

/// <summary>
/// Interfaz para el servicio de gestión de configuraciones y credenciales del sistema.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Carga la configuración de la aplicación de forma asíncrona desde las preferencias locales.
    /// </summary>
    Task<AppConfig?> LoadConfigAsync();

    /// <summary>
    /// Guarda la configuración de la aplicación y, de manera opcional, encripta la contraseña.
    /// </summary>
    /// <param name="config">El objeto con las rutas y correo.</param>
    /// <param name="password">La contraseña a encriptar (opcional).</param>
    Task SaveConfigAsync(AppConfig config, string? password = null);

    /// <summary>
    /// Obtiene la contraseña desencriptada de forma segura desde el almacenamiento seguro del sistema operativo.
    /// </summary>
    Task<string?> GetPasswordAsync();

    /// <summary>
    /// Verifica de forma rápida si la aplicación cuenta con una configuración inicial completa de primera ejecución.
    /// </summary>
    Task<bool> IsConfiguredAsync();

    /// <summary>
    /// Borra toda la configuración y contraseñas de la aplicación.
    /// </summary>
    Task ClearConfigAsync();
}
