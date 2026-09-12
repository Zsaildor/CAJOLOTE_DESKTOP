using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Cajolote.Models;
using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;

using System.Printing;

namespace Cajolote.Services;

public class NoteReceiptGenerator
{
    private readonly SettingsService _settingsService;

    public NoteReceiptGenerator(SettingsService settingsService)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _settingsService = settingsService;
    }

    public byte[] GeneratePdfReceipt(Note note, StoreProfile profile)
    {
        var document = CreateReceiptDocument(note, profile);

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }

    public System.Collections.Generic.IEnumerable<byte[]> GenerateImageReceipt(Note note, StoreProfile profile)
    {
        var document = CreateReceiptDocument(note, profile);
        return document.GenerateImages();
    }

    private Document CreateReceiptDocument(Note note, StoreProfile profile)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // 58mm roll width matches ~48mm print area (136 points)
                page.ContinuousSize(136); 
                page.Margin(2);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(x => ComposeHeader(x, profile));
                page.Content().Element(x => ComposeContent(x, note));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    private void ComposeHeader(IContainer container, StoreProfile profile)
    {
        container.Column(column =>
        {
            string storeName = !string.IsNullOrWhiteSpace(profile?.StoreName) ? profile.StoreName : "CAJOLOTE";
            column.Item().AlignCenter().Text(storeName).FontSize(11).SemiBold();
                
            if (!string.IsNullOrWhiteSpace(profile?.Address))
                column.Item().AlignCenter().Text(profile.Address);

            if (!string.IsNullOrWhiteSpace(profile?.Rfc))
                column.Item().AlignCenter().Text($"RFC: {profile.Rfc}");

            if (!string.IsNullOrWhiteSpace(profile?.Phone))
                column.Item().AlignCenter().Text($"Tel: {profile.Phone}");
            column.Item().PaddingTop(5).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Black);
        });
    }

    private void ComposeContent(IContainer container, Note note)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Item().AlignCenter().Text("COMPROBANTE DE LIQUIDACION").FontSize(8).Bold();
            column.Item().AlignCenter().Text("DE CUENTA / DEUDA").FontSize(8).Bold();
            column.Item().PaddingVertical(2).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

            column.Item().Text($"Cliente: {note.CustomerName}").SemiBold();
            column.Item().Text($"Nota #: {note.Id:D5}");
            column.Item().Text($"Apertura: {note.CreatedAt:dd/MM/yyyy}");
            column.Item().Text($"Liquidacion: {note.PaidAt?.ToString("dd/MM/yyyy - HH:mm") ?? System.DateTime.Now.ToString("dd/MM/yyyy - HH:mm")}");

            column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

            column.Item().PaddingVertical(5).Text("DESGLOSE DE DEUDA:").SemiBold().FontSize(8);

            // Deudas Manuales
            if (note.ManualDebts != null && note.ManualDebts.Any())
            {
                foreach (var md in note.ManualDebts)
                {
                    column.Item().PaddingVertical(2).Row(row =>
                    {
                        row.RelativeItem(3).Text($"D. Anotada ({md.Date:dd/MM/yyyy}): {md.Details}").FontSize(7);
                        row.RelativeItem(2).AlignRight().Text(md.Amount.ToString("C")).FontSize(7);
                    });
                    column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten3);
                }
            }

            // Ventas vinculadas
            if (note.Sales != null && note.Sales.Any())
            {
                foreach (var sale in note.Sales)
                {
                    column.Item().PaddingVertical(2).Row(row =>
                    {
                        row.RelativeItem(3).Text($"Venta #{sale.Id:D5} ({sale.Date:dd/MM/yyyy})").FontSize(8).Bold();
                        row.RelativeItem(2).AlignRight().Text(sale.Total.ToString("C")).FontSize(8).Bold();
                    });

                    // Sub-tabla con detalles de productos de la venta (art. cant. imp.)
                    column.Item().PaddingLeft(5).PaddingVertical(2).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Art.
                            columns.RelativeColumn(1); // Cant.
                            columns.RelativeColumn(2); // Imp.
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Art.").FontSize(8).SemiBold();
                            header.Cell().AlignCenter().Text("Cant.").FontSize(8).SemiBold();
                            header.Cell().AlignRight().Text("Imp.").FontSize(8).SemiBold();
                        });

                        foreach (var detail in sale.Details)
                        {
                            table.Cell().Text(detail.Product?.Name ?? "Producto").FontSize(8);
                            table.Cell().AlignCenter().Text(detail.Product?.IsBulk == true ? $"{detail.Quantity:0.000}kg" : detail.Quantity.ToString("0")).FontSize(8);
                            table.Cell().AlignRight().Text(detail.Subtotal.ToString("C")).FontSize(8);
                        }
                    });

                    column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten3);
                }
            }

            // Ventas históricas vinculadas
            if (note.HistoricalSales != null && note.HistoricalSales.Any())
            {
                foreach (var sale in note.HistoricalSales)
                {
                    column.Item().PaddingVertical(2).Row(row =>
                    {
                        row.RelativeItem(3).Text($"Venta #{sale.Id:D5} ({sale.Date:dd/MM/yyyy})").FontSize(8).Bold();
                        row.RelativeItem(2).AlignRight().Text(sale.Total.ToString("C")).FontSize(8).Bold();
                    });

                    // Sub-tabla con detalles de productos de la venta (art. cant. imp.)
                    column.Item().PaddingLeft(5).PaddingVertical(2).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Art.
                            columns.RelativeColumn(1); // Cant.
                            columns.RelativeColumn(2); // Imp.
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Art.").FontSize(8).SemiBold();
                            header.Cell().AlignCenter().Text("Cant.").FontSize(8).SemiBold();
                            header.Cell().AlignRight().Text("Imp.").FontSize(8).SemiBold();
                        });

                        foreach (var detail in sale.Details)
                        {
                            table.Cell().Text(detail.Product?.Name ?? "Producto").FontSize(8);
                            table.Cell().AlignCenter().Text(detail.Product?.IsBulk == true ? $"{detail.Quantity:0.000}kg" : detail.Quantity.ToString("0")).FontSize(8);
                            table.Cell().AlignRight().Text(detail.Subtotal.ToString("C")).FontSize(8);
                        }
                    });

                    column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten3);
                }
            }

            column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

            column.Item().AlignRight().Text($"DEUDA TOTAL PAGADA: {note.Amount:C}").FontSize(9).Bold();
            column.Item().AlignRight().Text($"RESTANTE: $0.00").FontSize(8).SemiBold();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Column(column =>
        {
            column.Item().Text("¡Cuenta liquidada correctamente!").FontSize(7).Bold();
            column.Item().PaddingTop(2).Text("Este documento no es comprobante fiscal").FontSize(7).SemiBold();
            column.Item().PaddingTop(5).Text("Cajolote vBeta 0.9.5 | © Zsaildor").FontSize(7).Light();
        });
    }

    public void PrintReceipt(Note note, StoreProfile profile, bool doPrint, bool showPrintDialog)
    {
        // 1. Guardar PDF de respaldo en el escritorio
        var pdfBytes = GeneratePdfReceipt(note, profile);
        if (pdfBytes != null)
        {
            var desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(desktopPath, $"ComprobanteDeuda_{note.Id}.pdf"), pdfBytes);
        }

        // 2. Mostrar el diálogo de impresión SI el usuario lo solicitó
        if (doPrint)
        {
            var printDialog = new PrintDialog();

            // Si el usuario configuró una impresora específica, la asignamos al diálogo de impresión
            if (_settingsService != null && !string.IsNullOrEmpty(_settingsService.Settings.PrinterName))
            {
                try
                {
                    using var printServer = new LocalPrintServer();
                    printDialog.PrintQueue = new PrintQueue(printServer, _settingsService.Settings.PrinterName);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al asignar la impresora configurada: {ex.Message}");
                }
            }

            bool shouldPrint = true;

            if (showPrintDialog)
            {
                shouldPrint = printDialog.ShowDialog() == true;
            }

            if (shouldPrint)
            {
                var imagesBytes = GenerateImageReceipt(note, profile);
                var fixedDoc = new FixedDocument();

                foreach (var imageBytes in imagesBytes)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = new MemoryStream(imageBytes);
                    bitmap.EndInit();

                    // Resolve extreme zoom issue: force physical width to 48mm (96 DPI in WPF)
                    double exactWidth = (48.0 / 25.4) * 96.0;
                    double aspectRatio = (double)bitmap.PixelHeight / (double)bitmap.PixelWidth;
                    double exactHeight = exactWidth * aspectRatio;

                    var image = new System.Windows.Controls.Image 
                    { 
                        Source = bitmap,
                        Stretch = Stretch.Fill,
                        Width = exactWidth,
                        Height = exactHeight
                    };

                    var page = new FixedPage();
                    page.Width = exactWidth;
                    page.Height = exactHeight;
                    
                    page.Children.Add(image);

                    var pageContent = new PageContent();
                    pageContent.Child = page;
                    fixedDoc.Pages.Add(pageContent);
                }

                printDialog.PrintDocument(fixedDoc.DocumentPaginator, $"ComprobanteDeuda_{note.Id:D5}");
            }
        }
    }
}
