#nullable enable
using System;

namespace FacturaPDF.Models;

/// <summary>
/// Representa una entrada persistente en el historial global de facturas procesadas.
/// </summary>
public record ProcessedInvoiceEntry(
    string FileName,
    string SourcePath,
    string PdfPath,
    DateTime ProcessedAt,
    string Status,
    string? ErrorMessage,
    long FileSize
);
