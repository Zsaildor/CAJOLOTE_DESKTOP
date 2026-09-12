using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<DashboardViewModel>();
            Loaded += (s, e) => MainScrollViewer.Focus();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (StatsGrid == null || ContentGrid == null)
                return;

            if (e.NewSize.Width < 800)
            {
                // PANTALLA PEQUEÑA: Apilado Vertical

                // 1. Tarjetas Superiores (StatsGrid: 1 columna, 4 filas)
                ColStat0.Width = new GridLength(1, GridUnitType.Star);
                ColStat1.Width = new GridLength(0, GridUnitType.Pixel);
                ColStat2.Width = new GridLength(0, GridUnitType.Pixel);
                ColStat3.Width = new GridLength(0, GridUnitType.Pixel);

                RowStat0.Height = GridLength.Auto;
                RowStat1.Height = GridLength.Auto;
                RowStat2.Height = GridLength.Auto;
                RowStat3.Height = GridLength.Auto;

                Grid.SetColumn(StatCard0, 0); Grid.SetRow(StatCard0, 0); StatCard0.Margin = new Thickness(10, 0, 10, 8);
                Grid.SetColumn(StatCard1, 0); Grid.SetRow(StatCard1, 1); StatCard1.Margin = new Thickness(10, 8, 10, 8);
                Grid.SetColumn(StatCard2, 0); Grid.SetRow(StatCard2, 2); StatCard2.Margin = new Thickness(10, 8, 10, 8);
                Grid.SetColumn(StatCard3, 0); Grid.SetRow(StatCard3, 3); StatCard3.Margin = new Thickness(10, 8, 10, 0);
                // 2. Gráficos y Detalles (ContentGrid: 1 columna, 4 filas)
                ColContent0.Width = new GridLength(1, GridUnitType.Star);
                ColContent1.Width = new GridLength(0, GridUnitType.Pixel);

                RowContent0.Height = GridLength.Auto; // Ventas por hora
                RowContent1.Height = GridLength.Auto; // Ventas fiadas
                RowContent2.Height = GridLength.Auto; // Top Productos
                RowContent3.Height = GridLength.Auto; // Categorías (PieChart)

                // Ajustar alturas de tarjetas
                ChartHourCard.Height = 300;
                ChartUnpaidCard.Height = 300;
                TopProductsCard.Height = 300;
                ChartCategoryCard.Height = 350;

                // Apilado ordenado verticalmente (RowSpan = 1 para todos)
                Grid.SetColumn(ChartHourCard, 0); Grid.SetRow(ChartHourCard, 0); Grid.SetRowSpan(ChartHourCard, 1); Grid.SetColumnSpan(ChartHourCard, 1);
                ChartHourCard.Margin = new Thickness(10, 15, 10, 10);

                Grid.SetColumn(ChartUnpaidCard, 0); Grid.SetRow(ChartUnpaidCard, 1); Grid.SetRowSpan(ChartUnpaidCard, 1); Grid.SetColumnSpan(ChartUnpaidCard, 1);
                ChartUnpaidCard.Margin = new Thickness(10, 10, 10, 10);

                Grid.SetColumn(TopProductsCard, 0); Grid.SetRow(TopProductsCard, 2); Grid.SetRowSpan(TopProductsCard, 1); Grid.SetColumnSpan(TopProductsCard, 1);
                TopProductsCard.Margin = new Thickness(10, 10, 10, 10);

                Grid.SetColumn(ChartCategoryCard, 0); Grid.SetRow(ChartCategoryCard, 3); Grid.SetRowSpan(ChartCategoryCard, 1); Grid.SetColumnSpan(ChartCategoryCard, 1);
                ChartCategoryCard.Margin = new Thickness(10, 10, 10, 10);
            }
            else
            {
                // PANTALLA GRANDE: Distribución Rejilla

                // 1. Tarjetas Superiores (StatsGrid: 4 columnas, 1 fila)
                ColStat0.Width = new GridLength(1, GridUnitType.Star);
                ColStat1.Width = new GridLength(1, GridUnitType.Star);
                ColStat2.Width = new GridLength(1, GridUnitType.Star);
                ColStat3.Width = new GridLength(1, GridUnitType.Star);

                RowStat0.Height = new GridLength(1, GridUnitType.Star);
                RowStat1.Height = new GridLength(0, GridUnitType.Pixel);
                RowStat2.Height = new GridLength(0, GridUnitType.Pixel);
                RowStat3.Height = new GridLength(0, GridUnitType.Pixel);

                Grid.SetColumn(StatCard0, 0); Grid.SetRow(StatCard0, 0); StatCard0.Margin = new Thickness(8);
                Grid.SetColumn(StatCard1, 1); Grid.SetRow(StatCard1, 0); StatCard1.Margin = new Thickness(8);
                Grid.SetColumn(StatCard2, 2); Grid.SetRow(StatCard2, 0); StatCard2.Margin = new Thickness(8);
                Grid.SetColumn(StatCard3, 3); Grid.SetRow(StatCard3, 0); StatCard3.Margin = new Thickness(8);

                // 2. Gráficos y Detalles (ContentGrid: 2 columnas, 3 filas)
                ColContent0.Width = new GridLength(1.5, GridUnitType.Star);
                ColContent1.Width = new GridLength(1, GridUnitType.Star);

                RowContent0.Height = GridLength.Auto;
                RowContent1.Height = GridLength.Auto;
                RowContent2.Height = GridLength.Auto;
                RowContent3.Height = new GridLength(0, GridUnitType.Pixel);

                // Alturas
                ChartHourCard.Height = 300;
                ChartUnpaidCard.Height = 300;
                TopProductsCard.Height = 300;
                ChartCategoryCard.Height = 350;

                // Ventas por hora: Fila 0, Span 2 (Todo el ancho)
                Grid.SetColumn(ChartHourCard, 0); Grid.SetRow(ChartHourCard, 0); Grid.SetRowSpan(ChartHourCard, 1); Grid.SetColumnSpan(ChartHourCard, 2);
                ChartHourCard.Margin = new Thickness(8, 15, 8, 8);

                // Ventas fiadas: Fila 1, Columna izquierda (Col 0), Span 1 (60% ancho)
                Grid.SetColumn(ChartUnpaidCard, 0); Grid.SetRow(ChartUnpaidCard, 1); Grid.SetRowSpan(ChartUnpaidCard, 1); Grid.SetColumnSpan(ChartUnpaidCard, 1);
                ChartUnpaidCard.Margin = new Thickness(8, 8, 8, 8);

                // Top Productos: Fila 1, Columna derecha (Col 1), Span 1 (40% ancho)
                Grid.SetColumn(TopProductsCard, 1); Grid.SetRow(TopProductsCard, 1); Grid.SetRowSpan(TopProductsCard, 1); Grid.SetColumnSpan(TopProductsCard, 1);
                TopProductsCard.Margin = new Thickness(8, 8, 8, 8);

                // Gráfico Categorías: Fila 2, Columnas 0 y 1 (Span 2)
                Grid.SetColumn(ChartCategoryCard, 0); Grid.SetRow(ChartCategoryCard, 2); Grid.SetRowSpan(ChartCategoryCard, 1); Grid.SetColumnSpan(ChartCategoryCard, 2);
                ChartCategoryCard.Margin = new Thickness(8, 8, 8, 8);
            }
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((FrameworkElement)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
