using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class RecientesView : UserControl
    {
        public RecientesView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<RecientesViewModel>();

            Loaded += (s, e) => MainScrollViewer.Focus();

            SalesListBox.IsKeyboardFocusWithinChanged += (s, e) =>
            {
                if (!(bool)e.NewValue)
                {
                    SalesListBox.SelectedIndex = -1;
                }
            };

            SalesListBox.SelectionChanged += (s, e) =>
            {
                CollapseAllExpanders();
            };

            PreviewKeyDown += (s, e) =>
            {
                var key = e.Key;
                var modifiers = System.Windows.Input.Keyboard.Modifiers;

                if (DataContext is RecientesViewModel vm)
                {
                    if (vm.IsCreditModalOpen)
                    {
                        if ((modifiers & System.Windows.Input.ModifierKeys.Control) != 0 && key == System.Windows.Input.Key.N)
                        {
                            e.Handled = true;
                            vm.CreateNewCreditAccount = !vm.CreateNewCreditAccount;
                            return;
                        }
                    }

                    if (vm.IsPrintModalOpen || vm.IsEditModalOpen || vm.IsDeleteConfirmationOpen || vm.IsCreditModalOpen)
                    {
                        return;
                    }
                }

                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    if (!SalesListBox.IsKeyboardFocusWithin)
                    {
                        return;
                    }
                    var selectedItem = SalesListBox.SelectedItem;
                    if (selectedItem != null)
                    {
                        var container = SalesListBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                        if (container != null)
                        {
                            var expander = FindVisualChild<Expander>(container);
                            if (expander != null)
                            {
                                expander.IsExpanded = !expander.IsExpanded;
                                e.Handled = true;
                            }
                        }
                    }
                }
                else if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    if (e.Key == System.Windows.Input.Key.T)
                    {
                        SalesListBox.Focus();
                        if (SalesListBox.Items.Count > 0 && SalesListBox.SelectedIndex == -1)
                        {
                            SalesListBox.SelectedIndex = 0;
                        }
                        e.Handled = true;
                        return;
                    }

                    var selectedSale = SalesListBox.SelectedItem as Cajolote.Models.Sale;
                    if (selectedSale != null && SalesListBox.IsKeyboardFocusWithin && DataContext is RecientesViewModel recVm)
                    {
                        if (e.Key == System.Windows.Input.Key.Delete)
                        {
                            if (recVm.DeleteSaleFromListCommand.CanExecute(selectedSale))
                            {
                                recVm.DeleteSaleFromListCommand.Execute(selectedSale);
                                e.Handled = true;
                            }
                        }
                        else if (e.Key == System.Windows.Input.Key.E)
                        {
                            if (selectedSale.Details != null && selectedSale.Details.Count > 0)
                            {
                                if (recVm.EditSaleCommand.CanExecute(selectedSale))
                                {
                                    recVm.EditSaleCommand.Execute(selectedSale);
                                    e.Handled = true;
                                }
                            }
                        }
                        else if (e.Key == System.Windows.Input.Key.D)
                        {
                            if (selectedSale.IsPaid)
                            {
                                if (recVm.FiarVentaCommand.CanExecute(selectedSale))
                                {
                                    recVm.FiarVentaCommand.Execute(selectedSale);
                                    e.Handled = true;
                                }
                            }
                        }
                        else if (e.Key == System.Windows.Input.Key.P)
                        {
                            if (selectedSale.Details != null && selectedSale.Details.Count > 0)
                            {
                                if (recVm.ReimprimirTicketCommand.CanExecute(selectedSale))
                                {
                                    recVm.ReimprimirTicketCommand.Execute(selectedSale);
                                    e.Handled = true;
                                }
                            }
                        }
                    }
                }
            };
        }

        private void CollapseAllExpanders()
        {
            if (SalesListBox == null) return;
            foreach (var item in SalesListBox.Items)
            {
                var container = SalesListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    var expander = FindVisualChild<Expander>(container);
                    if (expander != null)
                    {
                        expander.IsExpanded = false;
                    }
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T t)
                    return t;
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void BtnNuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                _ = main.NavigateToAsync(() => new PosView());
            }
        }

        private void ConfirmButton_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() => btn.Focus()));
            }
        }

        private void CreditCustomerComboBoxBorder_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border && border.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    CreditCustomerComboBox.Focus();
                    System.Windows.Input.Keyboard.Focus(CreditCustomerComboBox);
                    CreditCustomerComboBox.IsDropDownOpen = true;
                }));
            }
        }

        private void NewCreditCustomerTextBoxBorder_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border && border.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    NewCreditCustomerTextBox.Focus();
                    System.Windows.Input.Keyboard.Focus(NewCreditCustomerTextBox);
                    NewCreditCustomerTextBox.SelectAll();
                }));
            }
        }

        private void Modal_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is FrameworkElement element && !element.IsVisible)
            {
                SalesListBox.Focus();
            }
        }

        private void SalesListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                MainScrollViewer.RaiseEvent(eventArg);
            }
        }
    }
}
