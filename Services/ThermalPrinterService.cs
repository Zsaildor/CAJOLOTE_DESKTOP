using Cajolote.Models;
using System;
using System.IO.Ports;
using System.Text;

namespace Cajolote.Services;

/// <summary>
/// Servicio para imprimir tickets térmicos enviando comandos crudos ESC/POS al puerto serial (COM).
/// </summary>
public class ThermalPrinterService
{
    private readonly string _comPort;
    private readonly int _baudRate;

    public ThermalPrinterService(string comPort = "COM1", int baudRate = 9600)
    {
        _comPort = comPort;
        _baudRate = baudRate;
    }

    public void PrintTicket(Sale sale, StoreProfile profile)
    {
        try
        {
            using var port = new SerialPort(_comPort, _baudRate, Parity.None, 8, StopBits.One);
            port.Open();

            // Comandos básicos ESC/POS
            byte[] initPrinter = new byte[] { 27, 64 }; // ESC @
            byte[] alignCenter = new byte[] { 27, 97, 1 }; // ESC a 1
            byte[] alignLeft = new byte[] { 27, 97, 0 }; // ESC a 0
            byte[] boldOn = new byte[] { 27, 69, 1 }; // ESC E 1
            byte[] boldOff = new byte[] { 27, 69, 0 }; // ESC E 0
            byte[] cutPaper = new byte[] { 29, 86, 66, 0 }; // GS V B 0

            port.Write(initPrinter, 0, initPrinter.Length);

            // Cabecera
            port.Write(alignCenter, 0, alignCenter.Length);
            port.Write(boldOn, 0, boldOn.Length);
            
            string storeName = !string.IsNullOrWhiteSpace(profile?.StoreName) ? profile.StoreName : "CAJOLOTE";
            port.WriteLine($"{storeName}\n");
            port.Write(boldOff, 0, boldOff.Length);
            
            if (!string.IsNullOrWhiteSpace(profile?.OwnerName))
                port.WriteLine($"{profile.OwnerName}");
            if (!string.IsNullOrWhiteSpace(profile?.Address))
                port.WriteLine($"{profile.Address}");
            if (!string.IsNullOrWhiteSpace(profile?.Rfc))
                port.WriteLine($"RFC: {profile.Rfc}");
            if (!string.IsNullOrWhiteSpace(profile?.Phone))
                port.WriteLine($"Tel: {profile.Phone}\n");
            else
                port.WriteLine("\n");

            // Detalles
            port.Write(alignLeft, 0, alignLeft.Length);
            port.WriteLine($"Ticket: #{sale.Id:D5}");
            port.WriteLine($"Fecha: {sale.Date:dd/MM/yyyy HH:mm}");
            port.WriteLine("--------------------------------");
            
            foreach (var detail in sale.Details)
            {
                string productName = detail.Product?.Name ?? "Producto";
                if (productName.Length > 20) productName = productName.Substring(0, 20);
                
                string qtyStr = detail.Product?.IsBulk == true ? $"{detail.Quantity:0.000}kg" : detail.Quantity.ToString("00");
                string line = $"{productName.PadRight(20)} {qtyStr.PadLeft(8)} {detail.Subtotal,7:C}";
                port.WriteLine(line);
            }

            port.WriteLine("--------------------------------");
            port.Write(alignCenter, 0, alignCenter.Length);
            port.Write(boldOn, 0, boldOn.Length);
            port.WriteLine($"TOTAL: {sale.Total:C}\n");
            port.Write(boldOff, 0, boldOff.Length);

            port.WriteLine("Gracias por su compra!");
            port.WriteLine("\n\n\n"); // Espacio para corte

            // Cortar papel
            port.Write(cutPaper, 0, cutPaper.Length);

            port.Close();
        }
        catch (Exception ex)
        {
            // Aquí se manejaría el error (puerto cerrado, impresora no conectada)
            System.Diagnostics.Debug.WriteLine($"Error de impresión: {ex.Message}");
        }
    }
}
