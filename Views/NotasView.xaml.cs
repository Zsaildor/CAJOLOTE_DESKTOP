using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;
using Cajolote.Models;

namespace Cajolote.Views
{
    public partial class NotasView : UserControl
    {
        private UIElement? _lastFocusedElement;

        public NotasView()
        {
            InitializeComponent();
            var vm = App.Current.Services.GetRequiredService<NotasViewModel>();
            DataContext = vm;
            
            Loaded += (s, e) =>
            {
                MainScrollViewer.Focus();
                HookCollectionChanged(vm);
                HookModalVisibilityChanged();
            };

            ActiveNotesListBox.SelectionChanged += (s, e) =>
            {
                if (e.OriginalSource == ActiveNotesListBox)
                {
                    CollapseAllExpanders(ActiveNotesListBox);
                }
            };

            PaidNotesListBox.SelectionChanged += (s, e) =>
            {
                if (e.OriginalSource == PaidNotesListBox)
                {
                    CollapseAllExpanders(PaidNotesListBox);
                }
            };

            PreviewKeyDown += (s, e) =>
            {
                if (DataContext is NotasViewModel vm)
                {
                    if (vm.IsEditManualDebtOpen)
                    {
                        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (vm.SaveEditManualDebtCommand.CanExecute(null))
                            {
                                vm.SaveEditManualDebtCommand.Execute(null);
                            }
                        }
                        else if (e.Key == Key.Enter || e.Key == Key.Down)
                        {
                            e.Handled = true;
                            MoveModalFocus(1, new List<Control> { EditManualDebtAmountTextBox, EditManualDebtDetailsTextBox });
                        }
                        else if (e.Key == Key.Up)
                        {
                            e.Handled = true;
                            MoveModalFocus(-1, new List<Control> { EditManualDebtAmountTextBox, EditManualDebtDetailsTextBox });
                        }
                        return;
                    }
                    else if (vm.IsAddDebtModalOpen)
                    {
                        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (vm.ConfirmAddDebtCommand.CanExecute(null))
                            {
                                vm.ConfirmAddDebtCommand.Execute(null);
                            }
                        }
                        else if (e.Key == Key.Enter || e.Key == Key.Down)
                        {
                            e.Handled = true;
                            MoveModalFocus(1, new List<Control> { AddDebtAmountTextBox, AddDebtDetailsTextBox });
                        }
                        else if (e.Key == Key.Up)
                        {
                            e.Handled = true;
                            MoveModalFocus(-1, new List<Control> { AddDebtAmountTextBox, AddDebtDetailsTextBox });
                        }
                        return;
                    }
                    else if (vm.IsEditSaleOpen)
                    {
                        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (vm.SaveEditSaleCommand.CanExecute(null))
                            {
                                vm.SaveEditSaleCommand.Execute(null);
                            }
                        }
                        return;
                    }

                    // Ctrl+E toggles form selection
                    if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        e.Handled = true;
                        if (FormBorder.IsKeyboardFocusWithin)
                        {
                            MainScrollViewer.Focus();
                        }
                        else
                        {
                            FocusFirstFormControl(vm);
                        }
                    }
                    // Ctrl+D focuses ActiveNotesListBox
                    else if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        e.Handled = true;
                        if (ActiveNotesListBox != null && ActiveNotesListBox.IsKeyboardFocusWithin)
                        {
                            ActiveNotesListBox.SelectedIndex = -1;
                            MainScrollViewer.Focus();
                        }
                        else
                        {
                            if (vm.Notes == null || vm.Notes.Count == 0)
                            {
                                vm.ErrorMessageQueue.Enqueue("No hay cuentas activas registradas.");
                            }
                            else if (ActiveNotesListBox != null)
                            {
                                ActiveNotesListBox.Focus();
                                ActiveNotesListBox.SelectedIndex = 0;
                                Dispatcher.BeginInvoke(new System.Action(() =>
                                {
                                    var item = ActiveNotesListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                                    item?.Focus();
                                }));
                            }
                        }
                    }
                    // Ctrl+H focuses PaidNotesListBox
                    else if (e.Key == Key.H && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        e.Handled = true;
                        if (PaidNotesListBox != null && PaidNotesListBox.IsKeyboardFocusWithin)
                        {
                            PaidNotesListBox.SelectedIndex = -1;
                            MainScrollViewer.Focus();
                        }
                        else
                        {
                            if (vm.PaidNotes == null || vm.PaidNotes.Count == 0)
                            {
                                vm.ErrorMessageQueue.Enqueue("No hay historial de cuentas liquidadas.");
                            }
                            else if (PaidNotesListBox != null)
                            {
                                PaidNotesListBox.Focus();
                                PaidNotesListBox.SelectedIndex = 0;
                                Dispatcher.BeginInvoke(new System.Action(() =>
                                {
                                    var item = PaidNotesListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                                    item?.Focus();
                                }));
                            }
                        }
                    }
                    // Inner details list shortcuts (must check first because focus inside it also makes ActiveNotesListBox.IsKeyboardFocusWithin true)
                    else if (IsFocusInsideInnerDetailsList(out var innerListBox) && innerListBox != null)
                    {
                        var focusedTextBox = Keyboard.FocusedElement as TextBox;
                        if (focusedTextBox != null && focusedTextBox.Name == "AbonoTextBox")
                        {
                            return;
                        }

                        if (e.Key == Key.Enter)
                        {
                            e.Handled = true;
                            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                            {
                                ExitInnerDetailsList(innerListBox);
                            }
                            else
                            {
                                EditInnerItem(innerListBox.SelectedItem);
                            }
                        }
                        else if (e.Key == Key.Delete)
                        {
                            e.Handled = true;
                            DeleteInnerItem(innerListBox.SelectedItem);
                        }
                        else if (e.Key == Key.Escape)
                        {
                            e.Handled = true;
                            ExitInnerDetailsList(innerListBox);
                        }
                    }
                    // Active list item shortcuts
                    else if (ActiveNotesListBox != null && ActiveNotesListBox.IsKeyboardFocusWithin)
                    {
                        if (e.Key == Key.Enter)
                        {
                            e.Handled = true;
                            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                            {
                                EnterInnerDetailsList();
                            }
                            else
                            {
                                ToggleSelectedExpander(ActiveNotesListBox);
                            }
                        }
                        else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (ActiveNotesListBox.SelectedItem is Note note)
                            {
                                if (vm.RequestAddDebtCommand.CanExecute(note))
                                {
                                    vm.RequestAddDebtCommand.Execute(note);
                                }
                            }
                        }
                        else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (ActiveNotesListBox.SelectedItem is Note note)
                            {
                                if (vm.RequestSettleNoteCommand.CanExecute(note))
                                {
                                    vm.RequestSettleNoteCommand.Execute(note);
                                }
                            }
                        }
                        else if (e.Key == Key.Delete && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (ActiveNotesListBox.SelectedItem is Note note)
                            {
                                if (vm.RequestFullDeleteNoteCommand.CanExecute(note))
                                {
                                    vm.RequestFullDeleteNoteCommand.Execute(note);
                                }
                            }
                        }
                        else if (e.Key == Key.Escape)
                        {
                            e.Handled = true;
                            MainScrollViewer.Focus();
                        }
                    }
                    // Paid list item shortcuts
                    else if (PaidNotesListBox != null && PaidNotesListBox.IsKeyboardFocusWithin)
                    {
                        if (e.Key == Key.Enter)
                        {
                            e.Handled = true;
                            ToggleSelectedExpander(PaidNotesListBox);
                        }
                        else if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (PaidNotesListBox.SelectedItem is Note note)
                            {
                                if (vm.ReprintReceiptCommand.CanExecute(note))
                                {
                                    vm.ReprintReceiptCommand.Execute(note);
                                }
                            }
                        }
                        else if (e.Key == Key.Delete && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (PaidNotesListBox.SelectedItem is Note note)
                            {
                                if (vm.RequestPermanentDeleteCommand.CanExecute(note))
                                {
                                    vm.RequestPermanentDeleteCommand.Execute(note);
                                }
                            }
                        }
                        else if (e.Key == Key.Escape)
                        {
                            e.Handled = true;
                            MainScrollViewer.Focus();
                        }
                    }
                    // Controls inside form are active
                    else if (FormBorder.IsKeyboardFocusWithin)
                    {
                        // Ctrl+T toggles between new customer and existing customer
                        if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            vm.IsNewCustomer = !vm.IsNewCustomer;
                            
                            // Wait for UI to update visibility of the controls
                            Dispatcher.BeginInvoke(new System.Action(() =>
                            {
                                FocusFirstFormControl(vm);
                            }));
                        }
                        // Ctrl+S saves/annotates the debt
                        else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            if (vm.AddNoteCommand.CanExecute(null))
                            {
                                vm.AddNoteCommand.Execute(null);
                                // Refocus first control
                                Dispatcher.BeginInvoke(new System.Action(() =>
                                {
                                    FocusFirstFormControl(vm);
                                }));
                            }
                        }
                        // Enter or Arrow Down / Up navigation
                        else if (e.Key == Key.Enter || e.Key == Key.Down)
                        {
                            // Check if ComboBox dropdown is open
                            var focused = Keyboard.FocusedElement as DependencyObject;
                            var cb = focused as ComboBox ?? FindParent<ComboBox>(focused);
                            if (cb != null && cb.IsDropDownOpen)
                            {
                                // Let ComboBox handle it
                                return;
                            }

                            e.Handled = true;
                            MoveFormFocus(1);
                        }
                        else if (e.Key == Key.Up)
                        {
                            // Check if ComboBox dropdown is open
                            var focused = Keyboard.FocusedElement as DependencyObject;
                            var cb = focused as ComboBox ?? FindParent<ComboBox>(focused);
                            if (cb != null && cb.IsDropDownOpen)
                            {
                                // Let ComboBox handle it
                                return;
                            }

                            e.Handled = true;
                            MoveFormFocus(-1);
                        }
                    }
                }
            };
        }

        private void FocusFirstFormControl(NotasViewModel vm)
        {
            if (vm.IsNewCustomer)
            {
                if (NewCustomerNameTextBox != null)
                {
                    NewCustomerNameTextBox.Focus();
                    NewCustomerNameTextBox.SelectAll();
                }
            }
            else
            {
                if (ExistingCustomerComboBox != null)
                {
                    ExistingCustomerComboBox.Focus();
                }
            }
        }

        private void MoveFormFocus(int direction)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            Control? current = null;
            if (focused is TextBox || focused is ComboBox)
            {
                current = focused as Control;
            }
            else
            {
                current = FindParent<ComboBox>(focused);
            }

            if (current == null) return;

            var activeControls = GetActiveControls();
            int index = activeControls.IndexOf(current);
            if (index != -1)
            {
                int nextIndex = (index + direction + activeControls.Count) % activeControls.Count;
                var nextControl = activeControls[nextIndex];
                
                if (nextControl is ComboBox cb)
                {
                    cb.Focus();
                }
                else if (nextControl is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
        }

        private List<Control> GetActiveControls()
        {
            var list = new List<Control>();
            if (DataContext is NotasViewModel vm)
            {
                if (vm.IsNewCustomer)
                {
                    if (NewCustomerNameTextBox != null) list.Add(NewCustomerNameTextBox);
                }
                else
                {
                    if (ExistingCustomerComboBox != null) list.Add(ExistingCustomerComboBox);
                }
                if (NewAmountTextBox != null) list.Add(NewAmountTextBox);
                if (NewDetailsTextBox != null) list.Add(NewDetailsTextBox);
            }
            return list;
        }

        private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private T? FindVisualChild<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null)
                    {
                        if (child is T t)
                        {
                            return t;
                        }

                        T? childItem = FindVisualChild<T>(child);
                        if (childItem != null) return childItem;
                    }
                }
            }
            return null;
        }

        private void ToggleSelectedExpander(ListBox listBox)
        {
            var item = listBox.ItemContainerGenerator.ContainerFromIndex(listBox.SelectedIndex) as ListBoxItem;
            if (item != null)
            {
                var expander = FindVisualChild<Expander>(item);
                if (expander != null)
                {
                    expander.IsExpanded = !expander.IsExpanded;
                }
            }
        }

        private void CollapseAllExpanders(ListBox listBox)
        {
            if (listBox == null) return;
            foreach (var item in listBox.Items)
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
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

        private void ExistingCustomerComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                cb.IsDropDownOpen = true;
            }
        }

        private void ExistingCustomerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                if (cb.IsDropDownOpen)
                {
                    cb.IsDropDownOpen = false;
                }
                if (cb.IsKeyboardFocusWithin && NewAmountTextBox != null)
                {
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        NewAmountTextBox.Focus();
                        NewAmountTextBox.SelectAll();
                    }));
                }
            }
        }

        private void ConfirmButton_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() => btn.Focus()));
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (TopGrid == null || MetricsGrid == null || FormBorder == null || StatsContainerGrid == null ||
                MetricCard0 == null || MetricCard1 == null || MetricCard2 == null)
                return;

            if (e.NewSize.Width < 850)
            {
                // PANTALLA PEQUEÑA: Apilado Vertical

                // 1. TopGrid (Formulario arriba, Stats abajo)
                ColForm.Width = new GridLength(1, GridUnitType.Star);
                ColStats.Width = new GridLength(0, GridUnitType.Pixel);

                RowForm.Height = GridLength.Auto;
                RowStats.Height = GridLength.Auto;

                Grid.SetColumn(FormBorder, 0);
                Grid.SetRow(FormBorder, 0);
                FormBorder.Margin = new Thickness(5, 5, 5, 10);

                Grid.SetColumn(StatsContainerGrid, 0);
                Grid.SetRow(StatsContainerGrid, 1);
                StatsContainerGrid.Margin = new Thickness(5, 10, 5, 5);

                // 2. MetricsGrid (3 tarjetas de métricas apiladas verticalmente)
                ColMetric0.Width = new GridLength(1, GridUnitType.Star);
                ColMetric1.Width = new GridLength(0, GridUnitType.Pixel);
                ColMetric2.Width = new GridLength(0, GridUnitType.Pixel);

                RowMetric0.Height = GridLength.Auto;
                RowMetric1.Height = GridLength.Auto;
                RowMetric2.Height = GridLength.Auto;

                Grid.SetColumn(MetricCard0, 0); Grid.SetRow(MetricCard0, 0); MetricCard0.Margin = new Thickness(0, 0, 0, 8);
                Grid.SetColumn(MetricCard1, 0); Grid.SetRow(MetricCard1, 1); MetricCard1.Margin = new Thickness(0, 8, 0, 8);
                Grid.SetColumn(MetricCard2, 0); Grid.SetRow(MetricCard2, 2); MetricCard2.Margin = new Thickness(0, 8, 0, 0);
            }
            else
            {
                // PANTALLA GRANDE: Distribución Horizontal Original

                // 1. TopGrid (Formulario izquierda, Stats derecha)
                ColForm.Width = new GridLength(320, GridUnitType.Pixel);
                ColStats.Width = new GridLength(1, GridUnitType.Star);

                RowForm.Height = new GridLength(1, GridUnitType.Star);
                RowStats.Height = new GridLength(0, GridUnitType.Pixel);

                Grid.SetColumn(FormBorder, 0);
                Grid.SetRow(FormBorder, 0);
                FormBorder.Margin = new Thickness(5, 5, 15, 5);

                Grid.SetColumn(StatsContainerGrid, 1);
                Grid.SetRow(StatsContainerGrid, 0);
                StatsContainerGrid.Margin = new Thickness(0, 5, 5, 5);

                // 2. MetricsGrid (3 tarjetas de métricas lado a lado)
                ColMetric0.Width = new GridLength(1, GridUnitType.Star);
                ColMetric1.Width = new GridLength(1, GridUnitType.Star);
                ColMetric2.Width = new GridLength(1, GridUnitType.Star);

                RowMetric0.Height = new GridLength(1, GridUnitType.Star);
                RowMetric1.Height = new GridLength(0, GridUnitType.Pixel);
                RowMetric2.Height = new GridLength(0, GridUnitType.Pixel);

                Grid.SetColumn(MetricCard0, 0); Grid.SetRow(MetricCard0, 0); MetricCard0.Margin = new Thickness(5);
                Grid.SetColumn(MetricCard1, 1); Grid.SetRow(MetricCard1, 0); MetricCard1.Margin = new Thickness(5);
                Grid.SetColumn(MetricCard2, 2); Grid.SetRow(MetricCard2, 0); MetricCard2.Margin = new Thickness(5);
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
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

        private void HookCollectionChanged(NotasViewModel vm)
        {
            if (vm.Notes != null)
            {
                vm.Notes.CollectionChanged += (s, e) =>
                {
                    if (vm.Notes.Count == 0 && ActiveNotesListBox != null)
                    {
                        ActiveNotesListBox.SelectedIndex = -1;
                        if (ActiveNotesListBox.IsKeyboardFocusWithin)
                        {
                            Dispatcher.BeginInvoke(new System.Action(() => MainScrollViewer.Focus()));
                        }
                    }
                };
            }
            if (vm.PaidNotes != null)
            {
                vm.PaidNotes.CollectionChanged += (s, e) =>
                {
                    if (vm.PaidNotes.Count == 0 && PaidNotesListBox != null)
                    {
                        PaidNotesListBox.SelectedIndex = -1;
                        if (PaidNotesListBox.IsKeyboardFocusWithin)
                        {
                            Dispatcher.BeginInvoke(new System.Action(() => MainScrollViewer.Focus()));
                        }
                    }
                };
            }
        }

        private void HookModalVisibilityChanged()
        {
            if (this.Content is Grid mainGrid)
            {
                foreach (var child in mainGrid.Children)
                {
                    if (child is Grid modalGrid && modalGrid.Background is SolidColorBrush brush && brush.Color == Color.FromArgb(128, 0, 0, 0))
                    {
                        modalGrid.IsVisibleChanged += ModalGrid_IsVisibleChanged;
                    }
                }
            }
        }

        private void ModalGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Grid modalGrid)
            {
                if (modalGrid.IsVisible)
                {
                    var focused = Keyboard.FocusedElement as UIElement;
                    if (focused != null && !modalGrid.IsAncestorOf(focused))
                    {
                        _lastFocusedElement = focused;
                    }
                }
                else
                {
                    if (!IsAnyModalOpen())
                    {
                        RestoreLastFocus();
                    }
                }
            }
        }

        private bool IsAnyModalOpen()
        {
            if (this.Content is Grid mainGrid)
            {
                foreach (var child in mainGrid.Children)
                {
                    if (child is Grid modalGrid && modalGrid.Background is SolidColorBrush brush && brush.Color == Color.FromArgb(128, 0, 0, 0))
                    {
                        if (modalGrid.IsVisible)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void RestoreLastFocus()
        {
            if (DataContext is NotasViewModel vm)
            {
                if (_lastFocusedElement != null && (ActiveNotesListBox != null && (ActiveNotesListBox.IsKeyboardFocusWithin || _lastFocusedElement == ActiveNotesListBox || IsChildOf(ActiveNotesListBox, _lastFocusedElement))))
                {
                    if (vm.Notes == null || vm.Notes.Count == 0)
                    {
                        if (ActiveNotesListBox != null)
                        {
                            ActiveNotesListBox.SelectedIndex = -1;
                        }
                        MainScrollViewer.Focus();
                    }
                    else if (ActiveNotesListBox != null)
                    {
                        int index = ActiveNotesListBox.SelectedIndex;
                        if (index < 0 || index >= vm.Notes.Count)
                        {
                            index = 0;
                        }
                        ActiveNotesListBox.SelectedIndex = index;
                        Dispatcher.BeginInvoke(new System.Action(() =>
                        {
                            var item = ActiveNotesListBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                            item?.Focus();
                        }));
                    }
                    return;
                }

                if (_lastFocusedElement != null && (PaidNotesListBox != null && (PaidNotesListBox.IsKeyboardFocusWithin || _lastFocusedElement == PaidNotesListBox || IsChildOf(PaidNotesListBox, _lastFocusedElement))))
                {
                    if (vm.PaidNotes == null || vm.PaidNotes.Count == 0)
                    {
                        if (PaidNotesListBox != null)
                        {
                            PaidNotesListBox.SelectedIndex = -1;
                        }
                        MainScrollViewer.Focus();
                    }
                    else if (PaidNotesListBox != null)
                    {
                        int index = PaidNotesListBox.SelectedIndex;
                        if (index < 0 || index >= vm.PaidNotes.Count)
                        {
                            index = 0;
                        }
                        PaidNotesListBox.SelectedIndex = index;
                        Dispatcher.BeginInvoke(new System.Action(() =>
                        {
                            var item = PaidNotesListBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                            item?.Focus();
                        }));
                    }
                    return;
                }

                if (_lastFocusedElement is FrameworkElement fe)
                {
                    if (fe.IsLoaded)
                    {
                        fe.Focus();
                    }
                    else
                    {
                        MainScrollViewer.Focus();
                    }
                }
                else if (_lastFocusedElement != null)
                {
                    _lastFocusedElement.Focus();
                }
                else
                {
                    MainScrollViewer.Focus();
                }
            }
        }

        private bool IsChildOf(DependencyObject parent, DependencyObject? child)
        {
            if (child == null) return false;
            DependencyObject current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private bool IsFocusInsideInnerDetailsList(out ListBox? innerListBox)
        {
            innerListBox = null;
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused != null)
            {
                var parentListBox = FindParent<ListBox>(focused);
                if (parentListBox != null && parentListBox.Name == "InnerDetailsListBox")
                {
                    innerListBox = parentListBox;
                    return true;
                }
            }
            return false;
        }

        private void EnterInnerDetailsList()
        {
            var selectedIndex = ActiveNotesListBox.SelectedIndex;
            if (selectedIndex >= 0)
            {
                var container = ActiveNotesListBox.ItemContainerGenerator.ContainerFromIndex(selectedIndex) as ListBoxItem;
                if (container != null)
                {
                    // Ensure the expander is open
                    var expander = FindVisualChild<Expander>(container);
                    if (expander != null && !expander.IsExpanded)
                    {
                        expander.IsExpanded = true;
                    }

                    // Wait for layout/binding update if needed
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        var innerListBox = FindVisualChild<ListBox>(container);
                        if (innerListBox != null)
                        {
                            if (innerListBox.Items.Count > 0)
                            {
                                innerListBox.Focus();
                                innerListBox.SelectedIndex = 0;
                                Dispatcher.BeginInvoke(new System.Action(() =>
                                {
                                    var innerItem = innerListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                                    innerItem?.Focus();
                                }));
                            }
                            else
                            {
                                if (DataContext is NotasViewModel vm)
                                {
                                    vm.ErrorMessageQueue.Enqueue("No hay deudas o ventas en esta cuenta.");
                                }
                            }
                        }
                    }));
                }
            }
        }

        private void ExitInnerDetailsList(ListBox innerListBox)
        {
            innerListBox.SelectedIndex = -1;
            if (ActiveNotesListBox != null && ActiveNotesListBox.SelectedIndex >= 0)
            {
                ActiveNotesListBox.Focus();
                var parentItem = ActiveNotesListBox.ItemContainerGenerator.ContainerFromIndex(ActiveNotesListBox.SelectedIndex) as ListBoxItem;
                parentItem?.Focus();
            }
        }

        private void EditInnerItem(object item)
        {
            if (DataContext is NotasViewModel vm && item != null)
            {
                if (item is ManualDebt md)
                {
                    if (vm.EditManualDebtCommand.CanExecute(md))
                    {
                        vm.EditManualDebtCommand.Execute(md);
                    }
                }
                else if (item is Sale s)
                {
                    if (vm.EditLinkedSaleCommand.CanExecute(s))
                    {
                        vm.EditLinkedSaleCommand.Execute(s);
                    }
                }
            }
        }

        private void DeleteInnerItem(object item)
        {
            if (DataContext is NotasViewModel vm && item != null)
            {
                if (item is ManualDebt md)
                {
                    if (vm.DeleteManualDebtCommand.CanExecute(md))
                    {
                        vm.DeleteManualDebtCommand.Execute(md);
                    }
                }
                else if (item is Sale s)
                {
                    if (vm.DeleteLinkedSaleCommand.CanExecute(s))
                    {
                        vm.DeleteLinkedSaleCommand.Execute(s);
                    }
                }
            }
        }

        private void EditManualDebtAmountTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }));
            }
        }

        private void AddDebtAmountTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }));
            }
        }

        private void MoveModalFocus(int direction, List<Control> activeControls)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            Control? current = focused as Control;
            if (current == null) return;

            int index = activeControls.IndexOf(current);
            if (index != -1)
            {
                int nextIndex = (index + direction + activeControls.Count) % activeControls.Count;
                var nextControl = activeControls[nextIndex];
                
                nextControl.Focus();
                if (nextControl is TextBox tb)
                {
                    tb.SelectAll();
                }
            }
        }

        private void EditingSaleDetailsListBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.IsVisible)
            {
                listBox.UpdateLayout();
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (listBox.Items.Count > 0)
                    {
                        listBox.SelectedIndex = 0;
                        listBox.UpdateLayout();
                        var item = listBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                        item?.Focus();
                    }
                    else
                    {
                        listBox.Focus();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void EditingSaleDetailsListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is EditSaleDetailViewModel selectedItem)
            {
                var viewModel = DataContext as NotasViewModel;
                if (viewModel == null) return;

                switch (e.Key)
                {
                    case Key.OemMinus:
                    case Key.Subtract:
                        e.Handled = true;
                        if (viewModel.DecreaseEditQuantityCommand.CanExecute(selectedItem))
                            viewModel.DecreaseEditQuantityCommand.Execute(selectedItem);
                        break;

                    case Key.OemPlus:
                    case Key.Add:
                        e.Handled = true;
                        if (viewModel.IncreaseEditQuantityCommand.CanExecute(selectedItem))
                            viewModel.IncreaseEditQuantityCommand.Execute(selectedItem);
                        break;

                    case Key.Q:
                        e.Handled = true;
                        var container = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                        if (container != null)
                        {
                            var textBox = FindVisualChild<TextBox>(container);
                            if (textBox != null)
                            {
                                textBox.Focus();
                                textBox.SelectAll();
                            }
                        }
                        break;

                    case Key.Delete:
                        e.Handled = true;
                        if (viewModel.RemoveEditItemCommand.CanExecute(selectedItem))
                            viewModel.RemoveEditItemCommand.Execute(selectedItem);
                        break;
                }
            }
        }

        private void QtyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                e.Handled = true;
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    var listBoxItem = FindParent<ListBoxItem>(textBox);
                    if (listBoxItem != null)
                    {
                        listBoxItem.Focus();
                    }
                }
            }
        }

        private void InnerListBoxItem_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is PaymentFormItem)
            {
                if (e.OriginalSource == item)
                {
                    item.GotFocus -= InnerListBoxItem_GotFocus;
                    try
                    {
                        var textBox = FindVisualChild<TextBox>(item);
                        if (textBox != null && !textBox.IsKeyboardFocusWithin)
                        {
                            // desfasar para romper la cadena síncrona de eventos
                            Dispatcher.BeginInvoke(new System.Action(() =>
                            {
                                // comprobar de nuevo justo antes de enfocar
                                if (!textBox.IsKeyboardFocusWithin)
                                    textBox.Focus();
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                    finally
                    {
                        item.GotFocus += InnerListBoxItem_GotFocus;
                    }
                }
            }
        }

        private void AbonoTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.GotFocus -= AbonoTextBox_GotFocus;
                try
                {
                    // desfasar SelectAll para evitar reentrancia de Focus/GotFocus
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        if (Keyboard.FocusedElement == textBox || textBox.IsKeyboardFocusWithin)
                        {
                            textBox.SelectAll();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                finally
                {
                    textBox.GotFocus += AbonoTextBox_GotFocus;
                }
            }
        }

        private void AbonoTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                var viewModel = DataContext as NotasViewModel;
                if (viewModel == null) return;

                var parentFormItem = textBox.DataContext as PaymentFormItem;
                if (parentFormItem == null || parentFormItem.Note == null) return;

                string text = textBox.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    viewModel.ErrorMessageQueue.Enqueue("El monto a abonar no puede estar vacío.");
                    return;
                }

                var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                string cleaned = text.Replace(",", decSep).Replace(".", decSep);
                if (!decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal val) || val <= 0)
                {
                    viewModel.ErrorMessageQueue.Enqueue("Por favor ingrese un número entero o decimal válido mayor a 0.");
                    return;
                }

                viewModel.AbonoAmount = text;
                if (viewModel.AddAbonoCommand.CanExecute(parentFormItem.Note))
                {
                    viewModel.AddAbonoCommand.Execute(parentFormItem.Note);
                }
            }
            else if (e.Key == Key.Up)
            {
                var listBoxItem = FindParent<ListBoxItem>(textBox);
                if (listBoxItem != null)
                {
                    var listBox = FindParent<ListBox>(listBoxItem);
                    if (listBox != null)
                    {
                        int index = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
                        if (index > 0)
                        {
                            e.Handled = true;
                            listBox.SelectedIndex = index - 1;
                            listBox.UpdateLayout();
                            var prevItem = listBox.ItemContainerGenerator.ContainerFromIndex(index - 1) as ListBoxItem;
                            prevItem?.Focus();
                        }
                    }
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                var listBoxItem = FindParent<ListBoxItem>(textBox);
                if (listBoxItem != null)
                {
                    var listBox = FindParent<ListBox>(listBoxItem);
                    if (listBox != null)
                    {
                        ExitInnerDetailsList(listBox);
                    }
                }
            }
        }
    }
}
