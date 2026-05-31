#nullable enable
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace FacturaPDF.Converters;

/// <summary>
/// Convertidor para cambiar dinámicamente el color del paso del stepper en base al paso activo actual.
/// </summary>
public class StepColorConverter : IValueConverter
{
    /// <summary>
    /// El número de paso objetivo que este convertidor está evaluando.
    /// </summary>
    public int TargetStep { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep)
        {
            // Si el paso actual ya alcanzó o pasó el objetivo, lo pintamos de verde brillante
            if (currentStep >= TargetStep)
            {
                return Color.FromArgb("#52B788");
            }
        }
        
        // De lo contrario, se queda con el color inactivo oscuro
        return Color.FromArgb("#21262D");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convertidor que retorna True si la cadena de texto no está vacía o nula.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string text && !string.IsNullOrWhiteSpace(text);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convertidor que retorna True si el paso actual es mayor que 1 (Muestra el botón Atrás).
/// </summary>
public class StepGreaterThanOneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int step && step > 1;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convertidor que retorna True si el paso actual es menor que 3 (Muestra el botón Siguiente).
/// </summary>
public class StepLessThanThreeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int step && step < 3;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convertidor que invierte un valor booleano (útil para enlazar propiedades IsEnabled).
/// </summary>
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean && !boolean;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean && !boolean;
    }
}
