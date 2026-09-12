using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Cajolote.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cajolote.Services;

public class SalesReportGenerator
{
    public SalesReportGenerator()
    {
        // QuestPDF requires setting the license type
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateSalesReportPdf(IEnumerable<Sale> sales, StoreProfile profile, string filterDescription)
    {
        var document = CreateReportDocument(sales.ToList(), profile, filterDescription);
        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }

    private Document CreateReportDocument(List<Sale> sales, StoreProfile profile, string filterDescription)
    {
        var mainColor = Color.FromHex("#FF94A2");
        var alternateColor = Color.FromHex("#FBF6F6");
        var darkTextColor = Color.FromHex("#4A3B47");
        var lightBorderColor = Color.FromHex("#E8C4D8");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor(darkTextColor));

                page.Header().Element(x => ComposeHeader(x, profile, filterDescription, mainColor));
                page.Content().Element(x => ComposeContent(x, sales, mainColor, alternateColor, lightBorderColor));
                page.Footer().Element(x => ComposeFooter(x, mainColor));
            });
        });
    }

    private void ComposeHeader(IContainer container, StoreProfile profile, string filterDescription, Color mainColor)
    {
        container.PaddingBottom(15).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(infoColumn =>
                {
                    string storeName = !string.IsNullOrWhiteSpace(profile?.StoreName) ? profile.StoreName : "CAJOLOTE";
                    infoColumn.Item().Text(storeName).FontSize(18).Bold().FontColor(mainColor);

                    if (!string.IsNullOrWhiteSpace(profile?.OwnerName))
                        infoColumn.Item().Text($"Propietario: {profile.OwnerName}").FontSize(9);

                    if (!string.IsNullOrWhiteSpace(profile?.Address))
                        infoColumn.Item().Text(profile.Address).FontSize(9);

                    if (!string.IsNullOrWhiteSpace(profile?.Rfc))
                        infoColumn.Item().Text($"RFC: {profile.Rfc}").FontSize(9);

                    if (!string.IsNullOrWhiteSpace(profile?.Phone))
                        infoColumn.Item().Text($"Tel: {profile.Phone}").FontSize(9);
                });

                row.ConstantItem(200).AlignRight().Column(reportColumn =>
                {
                    reportColumn.Item().Text("REPORTE DE VENTAS").FontSize(14).Bold().FontColor(mainColor).AlignRight();
                    reportColumn.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).AlignRight();
                    if (!string.IsNullOrWhiteSpace(filterDescription))
                    {
                        reportColumn.Item().Text(filterDescription).FontSize(9).Italic().AlignRight();
                    }
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(mainColor);
        });
    }

    private void ComposeContent(IContainer container, List<Sale> sales, Color mainColor, Color alternateColor, Color borderColor)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f); // Folio
                    columns.RelativeColumn(3);    // Fecha/Hora
                    columns.RelativeColumn(2.5f); // Cantidad de Productos
                    columns.RelativeColumn(3);    // Total
                });

                // Table Header
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Background(mainColor).Text("Folio").Bold().FontColor(Colors.White);
                    header.Cell().Element(CellStyle).Background(mainColor).Text("Fecha").Bold().FontColor(Colors.White);
                    header.Cell().Element(CellStyle).Background(mainColor).Text("Productos").Bold().FontColor(Colors.White);
                    header.Cell().Element(CellStyle).Background(mainColor).AlignRight().Text("Total").Bold().FontColor(Colors.White);

                    static IContainer CellStyle(IContainer container) =>
                        container.Border(1).BorderColor(Colors.White).Padding(6).AlignMiddle();
                });

                // Table Rows
                for (int i = 0; i < sales.Count; i++)
                {
                    var sale = sales[i];
                    var rowBg = i % 2 == 0 ? Colors.White : alternateColor;

                    table.Cell().Element(CellStyle).Background(rowBg).Text($"#{sale.Id:D5}");
                    table.Cell().Element(CellStyle).Background(rowBg).Text(sale.Date.ToString("dd/MM/yyyy HH:mm"));
                    table.Cell().Element(CellStyle).Background(rowBg).Text(sale.TotalProductsCount.ToString());
                    table.Cell().Element(CellStyle).Background(rowBg).AlignRight().Text(sale.Total.ToString("C"));

                    IContainer CellStyle(IContainer container) =>
                        container.BorderBottom(0.5f).BorderColor(borderColor).Padding(6).AlignMiddle();
                }
            });

            // Summary Section
            decimal totalAmount = sales.Sum(s => s.Total);
            int totalSales = sales.Count;
            decimal averageSale = totalSales > 0 ? totalAmount / totalSales : 0;

            column.Item().PaddingTop(20).AlignRight().Width(250).Border(1).BorderColor(borderColor).Background(alternateColor).Padding(10).Column(sumColumn =>
            {
                sumColumn.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total Transacciones:").Bold().FontSize(9);
                    row.ConstantItem(80).AlignRight().Text(totalSales.ToString()).FontSize(9);
                });
                sumColumn.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(borderColor);
                sumColumn.Item().Row(row =>
                {
                    row.RelativeItem().Text("Promedio de Venta:").Bold().FontSize(9);
                    row.ConstantItem(80).AlignRight().Text(averageSale.ToString("C")).FontSize(9);
                });
                sumColumn.Item().PaddingVertical(2).LineHorizontal(1f).LineColor(mainColor);
                sumColumn.Item().Row(row =>
                {
                    row.RelativeItem().Text("MONTO TOTAL:").Bold().FontColor(mainColor).FontSize(11);
                    row.ConstantItem(80).AlignRight().Text(totalAmount.ToString("C")).Bold().FontColor(mainColor).FontSize(11);
                });
            });
        });
    }

    private void ComposeFooter(IContainer container, Color mainColor)
    {
        container.PaddingTop(15).Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(mainColor);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text("Cajolote - Sistema de Punto de Venta").FontSize(8).Italic();
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Página ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" de ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });
            });
        });
    }
}
