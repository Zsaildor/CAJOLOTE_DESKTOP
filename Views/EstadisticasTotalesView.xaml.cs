using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class EstadisticasTotalesView : UserControl
    {
        public EstadisticasTotalesView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<EstadisticasTotalesViewModel>();
            Loaded += (s, e) => MainScrollViewer.Focus();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.G && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                {
                    if (CategoriesListBox.SelectedIndex != -1)
                    {
                        CategoriesListBox.SelectedIndex = -1;
                        MainScrollViewer.Focus();
                    }
                    else
                    {
                        if (CategoriesListBox.Items.Count > 0)
                        {
                            CategoriesListBox.SelectedIndex = 0;
                            CategoriesListBox.UpdateLayout();
                            var container = CategoriesListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                            if (container != null)
                            {
                                container.Focus();
                            }
                            else
                            {
                                CategoriesListBox.Focus();
                            }
                        }
                        else
                        {
                            CategoriesListBox.Focus();
                        }
                    }
                    e.Handled = true;
                }
            };
        }

        private void MonthStatsRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                _ = mainWindow?.NavigateToAsync(() => new EstadisticasView());
            }
        }
    }
}
