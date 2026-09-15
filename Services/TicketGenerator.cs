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


using System.Printing;

namespace Cajolote.Services;

public class TicketGenerator
{
    private readonly SettingsService _settingsService;

    public TicketGenerator(SettingsService settingsService)
    {
        // QuestPDF requiere licencia comercial, usamos la comunitaria para desarrollo
        QuestPDF.Settings.License = LicenseType.Community;
        _settingsService = settingsService;
    }

    public byte[] GeneratePdfTicket(Sale sale, StoreProfile profile)
    {
        var document = CreateTicketDocument(sale, profile);

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }

    public System.Collections.Generic.IEnumerable<byte[]> GenerateImageTicket(Sale sale, StoreProfile profile)
    {
        var document = CreateTicketDocument(sale, profile);
        return document.GenerateImages();
    }

    private Document CreateTicketDocument(Sale sale, StoreProfile profile)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // 58mm de rollo tiene aprox 48mm de área de impresión real (136 puntos). 
                // ContinuousSize calcula la altura dinámicamente para que no corte el ticket
                page.ContinuousSize(136); 
                page.Margin(2);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

                page.Header().Element(x => ComposeHeader(x, profile));
                page.Content().Element(x => ComposeContent(x, sale));
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

    private void ComposeContent(IContainer container, Sale sale)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Item().Text($"Ticket: #{sale.Id:D5}");
            column.Item().Text($"Fecha: {sale.Date:dd/MM/yyyy - HH:mm}");
            
            if (sale.IsEdited && sale.EditedAt.HasValue)
            {
                column.Item().Text($"Este registro fue modificado el {sale.EditedAt.Value:dd/MM/yyyy HH:mm}").FontSize(8).Italic();
            }

            column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

            column.Item().PaddingVertical(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Nombre
                    columns.RelativeColumn(1); // Cant
                    columns.RelativeColumn(2); // Subtotal
                });

                table.Header(header =>
                {
                    header.Cell().Text("Art.");
                    header.Cell().AlignCenter().Text("Cant.");
                    header.Cell().AlignRight().Text("Imp.");
                });

                foreach (var detail in sale.Details)
                {
                    table.Cell().Text(detail.Product?.Name ?? "Producto");
                    table.Cell().AlignCenter().Text(detail.Product?.IsBulk == true ? $"{detail.Quantity:0.000}kg" : detail.Quantity.ToString("0"));
                    table.Cell().AlignRight().Text(detail.Subtotal.ToString("C"));
                }
            });

            column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

            column.Item().PaddingTop(10).AlignRight().Text($"TOTAL: {sale.Total:C}").FontSize(10).Bold();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Column(column =>
        {
            column.Item().Text("¡Gracias por su compra!").FontSize(7).Bold();
            column.Item().PaddingTop(2).Text("Este documento no es comprobante fiscal").FontSize(7).SemiBold(); //.FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(5).Text("Cajolote 1.0.2 | © Zsaildor").FontSize(7).Light(); //.FontColor(Colors.Grey.Darken1);
        });
    }

    public void PrintTicket(Sale sale, StoreProfile profile, bool showPrintDialog)
    {
        PrintTicket(sale, profile, doPrint: showPrintDialog, showPrintDialog: showPrintDialog);
    }

    public void PrintTicket(Sale sale, StoreProfile profile, bool doPrint, bool showPrintDialog)
    {
        // 1. Guardar PDF de respaldo en el escritorio asíncronamente
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var pdfBytes = GeneratePdfTicket(sale, profile);
                if (pdfBytes != null)
                {
                    var desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(desktopPath, $"Ticket_{sale.Id}.pdf"), pdfBytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar PDF: {ex.Message}");
            }
        });

        // 2. Imprimir si corresponde
        if (doPrint)
        {
            if (showPrintDialog)
            {
                // Usar Dispatcher.Invoke porque PrintDialog requiere hilo UI (STA)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var printDialog = new PrintDialog();
                        if (_settingsService != null && !string.IsNullOrEmpty(_settingsService.Settings.PrinterName))
                        {
                            using var printServer = new LocalPrintServer();
                            printDialog.PrintQueue = new PrintQueue(printServer, _settingsService.Settings.PrinterName);
                        }

                        if (printDialog.ShowDialog() == true)
                        {
                            PrintDocumentImpl(sale, profile, printDialog);
                        }
                    }
                    catch (PrintQueueException pqe)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error de cola de impresión: {pqe.Message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al mostrar diálogo de impresión: {ex.Message}");
                    }
                });
            }
            else
            {
                // Si no hay diálogo, generar e imprimir en background (Task.Run)
                // Nota: FixedDocument requiere STA, así que creamos un Thread STA para esto.
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        var printDialog = new PrintDialog();
                        if (_settingsService != null && !string.IsNullOrEmpty(_settingsService.Settings.PrinterName))
                        {
                            using var printServer = new LocalPrintServer();
                            printDialog.PrintQueue = new PrintQueue(printServer, _settingsService.Settings.PrinterName);
                        }
                        PrintDocumentImpl(sale, profile, printDialog);
                    }
                    catch (PrintQueueException pqe)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error de cola de impresión: {pqe.Message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al imprimir directo: {ex.Message}");
                    }
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
        }
    }

    private void PrintDocumentImpl(Sale sale, StoreProfile profile, PrintDialog printDialog)
    {
        var imagesBytes = GenerateImageTicket(sale, profile);
        var fixedDoc = new FixedDocument();

        foreach (var imageBytes in imagesBytes)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(imageBytes);
            bitmap.EndInit();

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

        printDialog.PrintDocument(fixedDoc.DocumentPaginator, $"Ticket_{sale.Id:D5}");
    }
}
