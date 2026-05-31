<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" 
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:cfdi="http://www.sat.gob.mx/cfd/4"
                xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital"
                exclude-result-prefixes="cfdi tfd">
  
  <xsl:output method="html" encoding="utf-8" indent="yes"/>

  <xsl:template match="/">
    <html>
      <head>
        <meta charset="utf-8"/>
        <title>Factura Digital CFDI</title>
        <style>
          @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;600;700&amp;display=swap');
          
          body {
            font-family: 'Inter', sans-serif;
            color: #1F2937;
            background-color: #FFFFFF;
            margin: 0;
            padding: 40px;
            font-size: 12px;
            line-height: 1.5;
          }

          .invoice-card {
            border: 1px solid #E5E7EB;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);
            max-width: 800px;
            margin: 0 auto;
          }

          /* Header Section */
          .header-grid {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            border-bottom: 2px solid #52B788;
            padding-bottom: 20px;
            margin-bottom: 25px;
          }

          .brand-title {
            font-size: 24px;
            font-weight: 700;
            color: #52B788;
            margin: 0;
          }

          .brand-subtitle {
            font-size: 12px;
            color: #6B7280;
            margin: 2px 0 0 0;
          }

          .invoice-details {
            text-align: right;
          }

          .invoice-details h2 {
            font-size: 18px;
            font-weight: 700;
            color: #111827;
            margin: 0 0 5px 0;
          }

          .invoice-details p {
            margin: 2px 0;
            color: #4B5563;
          }

          /* Emisor / Receptor */
          .parties-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
            margin-bottom: 25px;
          }

          .party-box {
            background-color: #F9FAFB;
            border: 1px solid #F3F4F6;
            border-radius: 8px;
            padding: 15px;
          }

          .party-box h3 {
            font-size: 13px;
            font-weight: 600;
            color: #111827;
            border-bottom: 1px solid #E5E7EB;
            padding-bottom: 5px;
            margin: 0 0 10px 0;
            text-transform: uppercase;
            letter-spacing: 0.5px;
          }

          .party-box p {
            margin: 3px 0;
            color: #4B5563;
          }

          .label {
            font-weight: 600;
            color: #374151;
          }

          /* Table of Concepts */
          .concepts-table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 25px;
          }

          .concepts-table th {
            background-color: #F3F4F6;
            color: #374151;
            font-weight: 600;
            text-align: left;
            padding: 10px;
            font-size: 11px;
            text-transform: uppercase;
            border-bottom: 1px solid #E5E7EB;
          }

          .concepts-table td {
            padding: 12px 10px;
            border-bottom: 1px solid #F3F4F6;
            color: #4B5563;
            vertical-align: top;
          }

          /* Totals Section */
          .totals-container {
            display: flex;
            justify-content: flex-end;
            margin-bottom: 30px;
          }

          .totals-box {
            width: 250px;
          }

          .total-row {
            display: flex;
            justify-content: space-between;
            padding: 6px 0;
            border-bottom: 1px solid #F3F4F6;
          }

          .total-row.grand-total {
            border-top: 2px solid #52B788;
            border-bottom: none;
            padding-top: 10px;
            font-size: 14px;
            font-weight: 700;
            color: #111827;
          }

          /* Footer / Timbre */
          .footer-section {
            border-top: 1px solid #E5E7EB;
            padding-top: 20px;
            font-size: 10px;
            color: #6B7280;
          }

          .uuid-text {
            font-family: monospace;
            font-size: 11px;
            color: #374151;
            background-color: #F3F4F6;
            padding: 3px 6px;
            border-radius: 4px;
          }
        </style>
      </head>
      <body>
        <div class="invoice-card">
          
          <!-- Encabezado -->
          <div class="header-grid">
            <div>
              <h1 class="brand-title">
                <xsl:value-of select="/cfdi:Comprobante/cfdi:Emisor/@Nombre"/>
              </h1>
              <p class="brand-subtitle">RFC: <xsl:value-of select="/cfdi:Comprobante/cfdi:Emisor/@Rfc"/></p>
              <p class="brand-subtitle">Régimen Fiscal: <xsl:value-of select="/cfdi:Comprobante/cfdi:Emisor/@RegimenFiscal"/></p>
            </div>
            
            <div class="invoice-details">
              <h2>FACTURA</h2>
              <p><span class="label">Folio:</span> <xsl:value-of select="/cfdi:Comprobante/@Serie"/><xsl:value-of select="/cfdi:Comprobante/@Folio"/></p>
              <p><span class="label">Fecha:</span> <xsl:value-of select="/cfdi:Comprobante/@Fecha"/></p>
              <p><span class="label">Tipo de Comprobante:</span> <xsl:value-of select="/cfdi:Comprobante/@TipoDeComprobante"/></p>
            </div>
          </div>

          <!-- Emisor y Receptor -->
          <div class="parties-grid">
            <div class="party-box">
              <h3>Emisor</h3>
              <p><span class="label">Nombre:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Emisor/@Nombre"/></p>
              <p><span class="label">RFC:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Emisor/@Rfc"/></p>
              <p><span class="label">Régimen Fiscal:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Emisor/@RegimenFiscal"/></p>
              <p><span class="label">Lugar de Expedición:</span> <xsl:value-of select="/cfdi:Comprobante/@LugarExpedicion"/></p>
            </div>
            
            <div class="party-box">
              <h3>Receptor</h3>
              <p><span class="label">Nombre:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Receptor/@Nombre"/></p>
              <p><span class="label">RFC:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Receptor/@Rfc"/></p>
              <p><span class="label">Uso CFDI:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Receptor/@UsoCFDI"/></p>
              <p><span class="label">Domicilio Fiscal:</span> <xsl:value-of select="/cfdi:Comprobante/cfdi:Receptor/@DomicilioFiscalReceptor"/></p>
            </div>
          </div>

          <!-- Tabla de Conceptos -->
          <table class="concepts-table">
            <thead>
              <tr>
                <th style="width: 10%">Cant</th>
                <th style="width: 15%">Clave</th>
                <th style="width: 45%">Descripción</th>
                <th style="width: 15%">P. Unitario</th>
                <th style="width: 15%">Importe</th>
              </tr>
            </thead>
            <tbody>
              <xsl:for-each select="/cfdi:Comprobante/cfdi:Conceptos/cfdi:Concepto">
                <tr>
                  <td><xsl:value-of select="@Cantidad"/></td>
                  <td><xsl:value-of select="@ClaveProdServ"/></td>
                  <td>
                    <xsl:value-of select="@Descripcion"/>
                    <xsl:if test="@ObjetoImp = '02'">
                      <div style="font-size: 9px; color: #9CA3AF; margin-top: 4px;">Sujeto a impuesto</div>
                    </xsl:if>
                  </td>
                  <td>$<xsl:value-of select="format-number(@ValorUnitario, '#,##0.00')"/></td>
                  <td>$<xsl:value-of select="format-number(@Importe, '#,##0.00')"/></td>
                </tr>
              </xsl:for-each>
            </tbody>
          </table>

          <!-- Totales -->
          <div class="totals-container">
            <div class="totals-box">
              <div class="total-row">
                <span class="label">Subtotal:</span>
                <span>$<xsl:value-of select="format-number(/cfdi:Comprobante/@SubTotal, '#,##0.00')"/></span>
              </div>
              
              <!-- Impuestos Trasladados -->
              <xsl:if test="/cfdi:Comprobante/cfdi:Impuestos/@TotalImpuestosTrasladados">
                <div class="total-row">
                  <span class="label">IVA Trasladado:</span>
                  <span>$<xsl:value-of select="format-number(/cfdi:Comprobante/cfdi:Impuestos/@TotalImpuestosTrasladados, '#,##0.00')"/></span>
                </div>
              </xsl:if>
              
              <!-- Impuestos Retenidos (Si existen) -->
              <xsl:if test="/cfdi:Comprobante/cfdi:Impuestos/@TotalImpuestosRetenidos">
                <div class="total-row">
                  <span class="label">Impuestos Retenidos:</span>
                  <span>-$<xsl:value-of select="format-number(/cfdi:Comprobante/cfdi:Impuestos/@TotalImpuestosRetenidos, '#,##0.00')"/></span>
                </div>
              </xsl:if>

              <!-- Total General -->
              <div class="total-row grand-total">
                <span>Total:</span>
                <span>$<xsl:value-of select="format-number(/cfdi:Comprobante/@Total, '#,##0.00')"/></span>
              </div>
            </div>
          </div>

          <!-- Sello y Timbre -->
          <div class="footer-section">
            <p>
              <span class="label">UUID (Folio Fiscal):</span><br/>
              <span class="uuid-text">
                <xsl:choose>
                  <xsl:when test="//*[local-name()='TimbreFiscalDigital']/@UUID">
                    <xsl:value-of select="//*[local-name()='TimbreFiscalDigital']/@UUID"/>
                  </xsl:when>
                  <xsl:otherwise>
                    DOCUMENTO NO TIMBRADO / DEMOSTRACIÓN
                  </xsl:otherwise>
                </xsl:choose>
              </span>
            </p>
            <p style="margin-top: 15px; font-size: 9px; text-align: center; color: #9CA3AF;">
              Representación impresa digital de un CFDI Versión 4.0 - Generado de forma local por FacturaPDF.
            </p>
          </div>

        </div>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>
