using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class RegistrosView : UserControl
    {
        public RegistrosView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<RegistrosViewModel>();

            Loaded += (s, e) => RestoreDefaultFocus();

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
                var modifiers = Keyboard.Modifiers;

                if (DataContext is RegistrosViewModel vm)
                {
                    if (vm.IsCreditModalOpen)
                    {
                        if ((modifiers & ModifierKeys.Control) != 0 && key == Key.N)
                        {
                            e.Handled = true;
                            vm.CreateNewCreditAccount = !vm.CreateNewCreditAccount;
                            return;
                        }
                    }

                    if (vm.IsPrintModalOpen || vm.IsEditModalOpen || vm.IsDeleteConfirmationOpen || vm.IsCreditModalOpen || vm.IsExportModalOpen)
                    {
                        return;
                    }
                }

                if (e.Key == Key.Enter)
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
                else if (e.Key == Key.Up && SalesListBox.IsKeyboardFocusWithin && SalesListBox.SelectedIndex == 0)
                {
                    e.Handled = true;
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.F)
                    {
                        e.Handled = true;
                        SearchTextBox.Focus();
                        SearchTextBox.SelectAll();
                    }
                    else if (e.Key == Key.O)
                    {
                        e.Handled = true;
                        SearchTypeComboBox.Focus();
                        SearchTypeComboBox.IsDropDownOpen = true;
                    }
                    else if (e.Key == Key.L)
                    {
                        e.Handled = true;
                        FilterComboBox.Focus();
                        FilterComboBox.IsDropDownOpen = true;
                    }
                    else if (e.Key == Key.X)
                    {
                        e.Handled = true;
                        if (DataContext is RegistrosViewModel regVm && regVm.ExportarCommand.CanExecute(null))
                        {
                            regVm.ExportarCommand.Execute(null);
                        }
                    }
                    else
                    {
                        var selectedSale = SalesListBox.SelectedItem as Cajolote.Models.Sale;
                        if (selectedSale != null && SalesListBox.IsKeyboardFocusWithin && DataContext is RegistrosViewModel regVm)
                        {
                            if (e.Key == Key.Delete)
                            {
                                e.Handled = true;
                                if (regVm.DeleteSaleFromListCommand.CanExecute(selectedSale))
                                {
                                    regVm.DeleteSaleFromListCommand.Execute(selectedSale);
                                }
                            }
                            else if (e.Key == Key.E)
                            {
                                e.Handled = true;
                                if (selectedSale.Details != null && selectedSale.Details.Count > 0)
                                {
                                    if (regVm.EditSaleCommand.CanExecute(selectedSale))
                                    {
                                        regVm.EditSaleCommand.Execute(selectedSale);
                                    }
                                }
                            }
                            else if (e.Key == Key.D)
                            {
                                e.Handled = true;
                                if (selectedSale.IsPaid)
                                {
                                    if (regVm.FiarVentaCommand.CanExecute(selectedSale))
                                    {
                                        regVm.FiarVentaCommand.Execute(selectedSale);
                                    }
                                }
                            }

                            else if (e.Key == Key.P)
                            {
                                e.Handled = true;
                                if (selectedSale.Details != null && selectedSale.Details.Count > 0)
                                {
                                    if (regVm.ReimprimirTicketCommand.CanExecute(selectedSale))
                                    {
                                        regVm.ReimprimirTicketCommand.Execute(selectedSale);
                                    }
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

        private void RestoreDefaultFocus()
        {
            if (SalesListBox != null && SalesListBox.SelectedItem != null)
            {
                SalesListBox.Focus();
                var container = SalesListBox.ItemContainerGenerator.ContainerFromItem(SalesListBox.SelectedItem) as UIElement;
                container?.Focus();
            }
            else if (SearchTextBox != null)
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                if (SalesListBox != null && SalesListBox.Items.Count > 0)
                {
                    SalesListBox.Focus();
                    if (SalesListBox.SelectedIndex == -1)
                    {
                        SalesListBox.SelectedIndex = 0;
                    }
                    if (SalesListBox.SelectedItem != null)
                    {
                        SalesListBox.UpdateLayout();
                        var container = SalesListBox.ItemContainerGenerator.ContainerFromItem(SalesListBox.SelectedItem) as UIElement;
                        container?.Focus();
                    }
                }
            }
        }

        private void ConfirmButton_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Button btn && btn.IsVisible)
            {
                Dispatcher.BeginInvoke(new Action(() => btn.Focus()));
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
                RestoreDefaultFocus();
            }
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }
        }


        private T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t)
                    return t;
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}
