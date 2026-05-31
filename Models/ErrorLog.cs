#nullable enable
using System;

namespace FacturaPDF.Models;

/// <summary>
/// Modelo inmutable para estructurar la metadata de los archivos XML que fallaron durante la conversión.
/// </summary>
public record ErrorLog(
    string FileName,
    DateTime Timestamp,
    string ErrorMessage,
    string ExceptionType,
    string XsltApplied
);
