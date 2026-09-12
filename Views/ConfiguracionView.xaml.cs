using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class ConfiguracionView : UserControl
    {
        public ConfiguracionView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<ConfiguracionViewModel>();

            Loaded += (s, e) => MainScrollViewer.Focus();

            BulkUnitComboBox.DropDownClosed += (s, e) => MainScrollViewer.Focus();
            PrintModelComboBox.DropDownClosed += (s, e) => MainScrollViewer.Focus();
            PrinterComboBox.DropDownClosed += (s, e) => MainScrollViewer.Focus();

            PreviewKeyDown += (s, e) =>
            {
                if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    if (e.Key == System.Windows.Input.Key.G)
                    {
                        BulkUnitComboBox.Focus();
                        BulkUnitComboBox.IsDropDownOpen = true;
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.M)
                    {
                        PrintModelComboBox.Focus();
                        PrintModelComboBox.IsDropDownOpen = true;
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.P && PrinterComboBox.IsVisible)
                    {
                        PrinterComboBox.Focus();
                        PrinterComboBox.IsDropDownOpen = true;
                        e.Handled = true;
                    }
                }
            };
        }
    }
}
