using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class EstadisticasView : UserControl
    {
        public EstadisticasView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<EstadisticasViewModel>();
            Loaded += (s, e) => MainScrollViewer.Focus();

            PreviewKeyDown += (s, e) =>
            {
                if (DataContext is EstadisticasViewModel vm)
                {
                    if (e.Key == System.Windows.Input.Key.Left)
                    {
                        if (vm.PreviousWeekCommand.CanExecute(null))
                        {
                            vm.PreviousWeekCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == System.Windows.Input.Key.Right)
                    {
                        if (vm.NextWeekCommand.CanExecute(null))
                        {
                            vm.NextWeekCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == System.Windows.Input.Key.G && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
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
            };
        }

        private void TotalStatsRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                _ = mainWindow?.NavigateToAsync(() => new EstadisticasTotalesView());
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (StatsGrid == null || ChartsGrid1 == null || ChartsGrid2 == null)
                return;

            if (e.NewSize.Width < 900)
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

                // 2. Gráfico 1 (ChartsGrid1: Ventas por día y top productos)
                ColChart1_Left.Width = new GridLength(1, GridUnitType.Star);
                ColChart1_Right.Width = new GridLength(0, GridUnitType.Pixel);

                RowChart1_Top.Height = new GridLength(300, GridUnitType.Pixel);
                RowChart1_Bottom.Height = new GridLength(300, GridUnitType.Pixel);

                Grid.SetColumn(Chart1LeftCard, 0); Grid.SetRow(Chart1LeftCard, 0);
                Chart1LeftCard.Margin = new Thickness(10, 0, 10, 10);

                Grid.SetColumn(Chart1RightCard, 0); Grid.SetRow(Chart1RightCard, 1);
                Chart1RightCard.Margin = new Thickness(10, 10, 10, 0);

                // 3. Gráfico 2 (ChartsGrid2: Horas pico e ingresos por categoría)
                ColChart2_Left.Width = new GridLength(1, GridUnitType.Star);
                ColChart2_Right.Width = new GridLength(0, GridUnitType.Pixel);

                RowChart2_Top.Height = GridLength.Auto;
                RowChart2_Bottom.Height = GridLength.Auto;

                Grid.SetColumn(Chart2LeftCard, 0); Grid.SetRow(Chart2LeftCard, 0);
                Chart2LeftCard.Margin = new Thickness(10, 0, 10, 10);

                Grid.SetColumn(Chart2RightCard, 0); Grid.SetRow(Chart2RightCard, 1);
                Chart2RightCard.Margin = new Thickness(10, 10, 10, 10);
            }
            else
            {
                // PANTALLA GRANDE: Distribución Horizontal Original

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

                // 2. Gráfico 1 (ChartsGrid1: Ventas por día y top productos)
                ColChart1_Left.Width = new GridLength(580, GridUnitType.Pixel);
                ColChart1_Right.Width = new GridLength(1, GridUnitType.Star);

                RowChart1_Top.Height = new GridLength(1, GridUnitType.Star);
                RowChart1_Bottom.Height = new GridLength(0, GridUnitType.Pixel);

                Grid.SetColumn(Chart1LeftCard, 0); Grid.SetRow(Chart1LeftCard, 0);
                Chart1LeftCard.Margin = new Thickness(10, 0, 10, 0);

                Grid.SetColumn(Chart1RightCard, 1); Grid.SetRow(Chart1RightCard, 0);
                Chart1RightCard.Margin = new Thickness(5, 0, 10, 0);

                // 3. Gráfico 2 (ChartsGrid2: Horas pico e ingresos por categoría)
                ColChart2_Left.Width = new GridLength(1, GridUnitType.Star);
                ColChart2_Right.Width = new GridLength(0, GridUnitType.Pixel);

                RowChart2_Top.Height = GridLength.Auto;
                RowChart2_Bottom.Height = GridLength.Auto;

                Grid.SetColumn(Chart2LeftCard, 0); Grid.SetRow(Chart2LeftCard, 0);
                Chart2LeftCard.Margin = new Thickness(10, 0, 10, 10);

                Grid.SetColumn(Chart2RightCard, 0); Grid.SetRow(Chart2RightCard, 1);
                Chart2RightCard.Margin = new Thickness(10, 10, 10, 10);
            }
        }
    }
}
