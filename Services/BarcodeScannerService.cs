using System;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Cajolote.Services;

/// <summary>
/// Escucha eventos de teclado a nivel de ventana para identificar lecturas de un escáner de códigos de barras (Keyboard Wedge).
/// Se basa en la velocidad de escritura: si las teclas se presionan muy rápido y terminan en Enter, es un escáner.
/// </summary>
public class BarcodeScannerService
{
    private StringBuilder _barcodeBuffer = new StringBuilder();
    private DateTime _lastKeystroke = DateTime.Now;
    
    // Tiempo máximo entre teclas para ser considerado escáner (usualmente un humano tarda > 100ms, un escáner < 30ms)
    private readonly TimeSpan _keystrokeThreshold = TimeSpan.FromMilliseconds(50);

    public event EventHandler<string>? BarcodeScanned;

    public void Attach(Window window)
    {
        // Se usa PreviewTextInput para capturar el texto directamente sin lidiar con Key to Char conversion
        window.PreviewTextInput += Window_PreviewTextInput;
        window.PreviewKeyDown += Window_PreviewKeyDown;
    }

    public void Detach(Window window)
    {
        window.PreviewTextInput -= Window_PreviewTextInput;
        window.PreviewKeyDown -= Window_PreviewKeyDown;
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var now = DateTime.Now;
        
        // Si ha pasado mucho tiempo desde la última tecla, limpiamos el buffer (era un humano escribiendo)
        if (now - _lastKeystroke > _keystrokeThreshold)
        {
            _barcodeBuffer.Clear();
        }

        _barcodeBuffer.Append(e.Text);
        _lastKeystroke = now;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            var now = DateTime.Now;
            
            // Si la tecla Enter se presionó rápido después de los caracteres, asumimos que fue el escáner
            if (now - _lastKeystroke <= _keystrokeThreshold && _barcodeBuffer.Length > 2)
            {
                var barcode = _barcodeBuffer.ToString().Trim();
                
                // Disparamos el evento
                BarcodeScanned?.Invoke(this, barcode);
                
                // Marcamos como manejado para evitar que el Enter accione otros botones accidentalmente
                e.Handled = true;
            }
            
            _barcodeBuffer.Clear();
        }
    }
}
