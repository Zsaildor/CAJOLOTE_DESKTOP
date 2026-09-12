using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;
using System.Windows.Input;
using System.Windows;

namespace Cajolote.Views
{
    public partial class PosView : UserControl
    {
        private bool _isLoadedOnce = false;

        public PosView()
        {
            InitializeComponent();
            var viewModel = App.Current.Services.GetRequiredService<PosViewModel>();
            DataContext = viewModel;
            
            viewModel.CartItems.CollectionChanged += CartItems_CollectionChanged;
            viewModel.ProductAddedDirectly += ViewModel_ProductAddedDirectly;
            Loaded += PosView_Loaded;
        }

        private void CartItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    CartScrollViewer?.ScrollToBottom();
                }));
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    FocusQuickProducts();
                }));
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (CartListBox != null)
                    {
                        if (CartListBox.Items.Count > 0)
                        {
                            int oldIndex = e.OldStartingIndex;
                            int nextIndex = System.Math.Min(oldIndex, CartListBox.Items.Count - 1);
                            if (nextIndex >= 0)
                            {
                                CartListBox.SelectedIndex = nextIndex;
                                FocusListBoxItem(CartListBox, nextIndex);
                            }
                            else
                            {
                                FocusQuickProducts();
                            }
                        }
                        else
                        {
                            FocusQuickProducts();
                        }
                    }
                }));
            }
        }

        private void ViewModel_ProductAddedDirectly()
        {
            if (SearchTextBox != null && SearchTextBox.IsFocused)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    FocusQuickProducts();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void QuantityTextBox_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    textBox.Focus();
                    System.Windows.Input.Keyboard.Focus(textBox);
                    textBox.SelectAll();
                }));
            }
        }

        private void NameTextBox_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    textBox.Focus();
                    System.Windows.Input.Keyboard.Focus(textBox);
                    textBox.SelectAll();
                }));
            }
        }

        private void ByWeightRadio_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (QuantityTextBox != null)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (QuantityTextBox.IsVisible)
                    {
                        QuantityTextBox.Focus();
                        System.Windows.Input.Keyboard.Focus(QuantityTextBox);
                        QuantityTextBox.SelectAll();
                    }
                }));
            }
        }

        private void ByPriceRadio_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (PriceTextBox != null)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (PriceTextBox.IsVisible)
                    {
                        PriceTextBox.Focus();
                        System.Windows.Input.Keyboard.Focus(PriceTextBox);
                        PriceTextBox.SelectAll();
                    }
                }));
            }
        }

        private void ConfirmButton_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() => btn.Focus()));
            }
        }

        private void BarcodeTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                if (sender is System.Windows.Controls.TextBox tb)
                {
                    tb.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                }
            }
        }

        private void UserControl_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if (ColCart == null || ColProducts == null || RowCart == null || RowProducts == null || CartBorder == null || ProductsGrid == null || MainScrollViewer == null)
                return;

            if (e.NewSize.Width < 800)
            {
                // PANTALLA PEQUEÑA: Apilado Vertical (Carrito arriba, Productos abajo) con Scroll general habilitado
                MainScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;

                ColCart.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                ColProducts.Width = new System.Windows.GridLength(0, System.Windows.GridUnitType.Pixel);

                // Damos alturas fijas cómodas para que los paneles no se aplasten y el ScrollViewer actúe
                RowCart.Height = new System.Windows.GridLength(500, System.Windows.GridUnitType.Pixel);
                RowProducts.Height = new System.Windows.GridLength(650, System.Windows.GridUnitType.Pixel);

                Grid.SetColumn(CartBorder, 0);
                Grid.SetRow(CartBorder, 0);
                CartBorder.Margin = new System.Windows.Thickness(10, 0, 10, 10);

                Grid.SetColumn(ProductsGrid, 0);
                Grid.SetRow(ProductsGrid, 1);
                ProductsGrid.Margin = new System.Windows.Thickness(10, 10, 10, 0);
            }
            else
            {
                // PANTALLA GRANDE: Distribución Horizontal Original (Carrito a la izquierda, Productos a la derecha) sin scroll general
                MainScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;

                ColCart.Width = new System.Windows.GridLength(420, System.Windows.GridUnitType.Pixel);
                ColProducts.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);

                RowCart.Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                RowProducts.Height = new System.Windows.GridLength(0, System.Windows.GridUnitType.Pixel);

                Grid.SetColumn(CartBorder, 0);
                Grid.SetRow(CartBorder, 0);
                CartBorder.Margin = new System.Windows.Thickness(0, 0, 10, 0);

                Grid.SetColumn(ProductsGrid, 1);
                Grid.SetRow(ProductsGrid, 0);
                ProductsGrid.Margin = new System.Windows.Thickness(10, 0, 0, 0);
            }
        }

        private string _barcodeBuffer = "";
        private System.DateTime _lastKeystroke = System.DateTime.Now;

        private void UserControl_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (DataContext is PosViewModel vm && (vm.IsEditingQuantity || vm.IsEditingPrice || vm.IsCreditModalOpen || vm.IsPrintModalOpen || vm.IsAddingProduct))
                return;

            TimeSpan elapsed = System.DateTime.Now - _lastKeystroke;
            _lastKeystroke = System.DateTime.Now;

            if (elapsed.TotalMilliseconds > 100)
            {
                _barcodeBuffer = "";
            }
            _barcodeBuffer += e.Text;
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;
            var modifiers = System.Windows.Input.Keyboard.Modifiers;

            if (DataContext is PosViewModel vm)
            {
                // Si el modal de cantidad está abierto para un producto a granel, interceptar atajos de cambio de modo
                if (vm.IsEditingQuantity && vm.EditingCartItem?.Product?.IsBulk == true)
                {
                    if ((modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                    {
                        if (key == System.Windows.Input.Key.P) // Por Precio (Ctrl+P)
                        {
                            e.Handled = true;
                            vm.IsQuantityByWeight = false;
                            return;
                        }
                        else if (key == System.Windows.Input.Key.G || key == System.Windows.Input.Key.K) // Por kg/g/peso (Ctrl+G o Ctrl+K)
                        {
                            e.Handled = true;
                            vm.IsQuantityByWeight = true;
                            return;
                        }
                    }
                }

                // Si el modal de crédito/deuda está abierto, interceptar Ctrl+N para alternar la casilla
                if (vm.IsCreditModalOpen)
                {
                    if ((modifiers & System.Windows.Input.ModifierKeys.Control) != 0 && key == System.Windows.Input.Key.N)
                    {
                        e.Handled = true;
                        vm.CreateNewCreditAccount = !vm.CreateNewCreditAccount;
                        return;
                    }
                }

                // Si hay un modal abierto, no aplicar atajos globales de la lista
                if (vm.IsEditingQuantity || vm.IsEditingPrice || vm.IsCreditModalOpen || vm.IsPrintModalOpen || vm.IsAddingProduct)
                {
                    return;
                }

                // Barcode Scanner detection
                if (key == System.Windows.Input.Key.Enter)
                {
                    TimeSpan elapsed = System.DateTime.Now - _lastKeystroke;
                    if (elapsed.TotalMilliseconds <= 100 && _barcodeBuffer.Length >= 3)
                    {
                        e.Handled = true;
                        string scannedCode = _barcodeBuffer;
                        _barcodeBuffer = "";

                        if (SearchTextBox != null && SearchTextBox.IsFocused)
                        {
                            SearchTextBox.Text = string.Empty;
                        }

                        if (vm.AddProductCommand.CanExecute(scannedCode))
                        {
                            vm.AddProductCommand.Execute(scannedCode);
                        }
                        return;
                    }
                    _barcodeBuffer = ""; // reset on normal enter
                }

                if ((modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    switch (key)
                    {
                        case System.Windows.Input.Key.Delete: // Vaciar (Ctrl+Delete)
                            e.Handled = true;
                            if (vm.ClearCartCommand.CanExecute(null))
                                vm.ClearCartCommand.Execute(null);
                            break;
                        case System.Windows.Input.Key.C: // Cobrar
                            e.Handled = true;
                            if (vm.CheckoutCommand.CanExecute(null))
                                vm.CheckoutCommand.Execute(null);
                            break;
                        case System.Windows.Input.Key.D: // Fiar (Deuda)
                            e.Handled = true;
                            if (vm.CheckoutCreditCommand.CanExecute(null))
                                vm.CheckoutCreditCommand.Execute(null);
                            break;
                        case System.Windows.Input.Key.F: // Buscar
                            e.Handled = true;
                            FocusSearchTextBox();
                            break;
                        case System.Windows.Input.Key.L: // Candado (Timer lock)
                            e.Handled = true;
                            if (vm.ToggleSearchTimerLockCommand.CanExecute(null))
                                vm.ToggleSearchTimerLockCommand.Execute(null);
                            break;
                        case System.Windows.Input.Key.Q: // Focus Fast Products ListBox
                            e.Handled = true;
                            FocusQuickProducts();
                            break;
                        case System.Windows.Input.Key.K: // Focus Cart ListBox
                            e.Handled = true;
                            FocusCart();
                            break;
                        case System.Windows.Input.Key.X: // Limpiar búsqueda (Ctrl+X)
                            e.Handled = true;
                            if (vm.ClearSearchCommand.CanExecute(null))
                                vm.ClearSearchCommand.Execute(null);
                            break;
                    }
                }
            }
        }
        protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsKeyboardFocusWithinChanged(e);
            if (!(bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (!IsKeyboardFocusWithin)
                    {
                        var window = Window.GetWindow(this);
                        if (window != null && window.IsActive)
                        {
                            var focused = Keyboard.FocusedElement as DependencyObject;
                            if (focused == null || focused == window)
                            {
                                FocusQuickProducts();
                            }
                        }
                    }
                }));
            }
        }

        private void FocusSearchTextBox()
        {
            if (SearchTextBox != null)
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    SearchTextBox.Focus();
                    System.Windows.Input.Keyboard.Focus(SearchTextBox);
                    SearchTextBox.SelectAll();
                }));
            }
        }

        private void FocusQuickProducts()
        {
            if (QuickProductsListBox != null && QuickProductsListBox.Items.Count > 0)
            {
                QuickProductsListBox.Focus();
                if (QuickProductsListBox.SelectedIndex == -1)
                {
                    QuickProductsListBox.SelectedIndex = 0;
                }
                FocusListBoxItem(QuickProductsListBox, QuickProductsListBox.SelectedIndex);
            }
            else
            {
                FocusSearchTextBox();
            }
        }

        private void FocusCart()
        {
            if (CartListBox != null && CartListBox.Items.Count > 0)
            {
                CartListBox.Focus();
                if (CartListBox.SelectedIndex == -1)
                {
                    CartListBox.SelectedIndex = 0;
                }
                FocusListBoxItem(CartListBox, CartListBox.SelectedIndex);
            }
        }

        private void QuickProductsListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                if (e.Key == System.Windows.Input.Key.Tab)
                {
                    e.Handled = true;
                    if (listBox.SelectedItem is Cajolote.Models.Product product)
                    {
                        var viewModel = DataContext as PosViewModel;
                        if (viewModel != null && viewModel.AddProductCommand.CanExecute(product))
                        {
                            viewModel.AddProductCommand.Execute(product);
                        }
                    }
                }
                else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
                {
                    e.Handled = true;
                    NavigateWrapPanel(listBox, e.Key);
                }
                else if (e.Key == System.Windows.Input.Key.Left)
                {
                    var selectedItem = listBox.SelectedItem;
                    if (selectedItem != null)
                    {
                        var container = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                        if (container != null)
                        {
                            var pos = container.TransformToAncestor(listBox).Transform(new System.Windows.Point(0, 0));
                            if (pos.X < 20)
                            {
                                if (CartListBox != null && CartListBox.Items.Count > 0 && CartListBox.Visibility == Visibility.Visible)
                                {
                                    e.Handled = true;
                                    FocusCart();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CartListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is CartItemViewModel selectedItem)
            {
                var viewModel = DataContext as PosViewModel;
                if (viewModel == null) return;

                switch (e.Key)
                {
                    case System.Windows.Input.Key.OemMinus:
                    case System.Windows.Input.Key.Subtract:
                        e.Handled = true;
                        if (viewModel.DecreaseQuantityCommand.CanExecute(selectedItem))
                            viewModel.DecreaseQuantityCommand.Execute(selectedItem);
                        break;

                    case System.Windows.Input.Key.OemPlus:
                    case System.Windows.Input.Key.Add:
                        e.Handled = true;
                        if (viewModel.IncreaseQuantityCommand.CanExecute(selectedItem))
                            viewModel.IncreaseQuantityCommand.Execute(selectedItem);
                        break;

                    case System.Windows.Input.Key.Q:
                        e.Handled = true;
                        if (viewModel.EditQuantityCommand.CanExecute(selectedItem))
                            viewModel.EditQuantityCommand.Execute(selectedItem);
                        break;

                    case System.Windows.Input.Key.P:
                        e.Handled = true;
                        if (viewModel.EditPriceCommand.CanExecute(selectedItem))
                            viewModel.EditPriceCommand.Execute(selectedItem);
                        break;

                    case System.Windows.Input.Key.Delete:
                        e.Handled = true;
                        if (viewModel.RemoveItemCommand.CanExecute(selectedItem))
                            viewModel.RemoveItemCommand.Execute(selectedItem);
                        break;

                    case System.Windows.Input.Key.Right:
                        e.Handled = true;
                        FocusQuickProducts();
                        break;
                }
            }
        }

        private void NavigateWrapPanel(ListBox listBox, System.Windows.Input.Key key)
        {
            if (listBox.SelectedIndex == -1)
            {
                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                    FocusListBoxItem(listBox, 0);
                }
                return;
            }

            var selectedItem = listBox.SelectedItem;
            var selectedContainer = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
            if (selectedContainer == null) return;

            var selectedPos = selectedContainer.TransformToAncestor(listBox).Transform(new System.Windows.Point(0, 0));
            var selectedRect = new System.Windows.Rect(selectedPos, new System.Windows.Size(selectedContainer.ActualWidth, selectedContainer.ActualHeight));
            var selectedCenterX = selectedRect.Left + selectedRect.Width / 2;

            ListBoxItem? bestMatch = null;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var item = listBox.Items[i];
                if (item == selectedItem) continue;

                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container == null) continue;

                var pos = container.TransformToAncestor(listBox).Transform(new System.Windows.Point(0, 0));
                var rect = new System.Windows.Rect(pos, new System.Windows.Size(container.ActualWidth, container.ActualHeight));
                var centerX = rect.Left + rect.Width / 2;

                if (key == System.Windows.Input.Key.Up)
                {
                    if (rect.Bottom <= selectedRect.Top + 2)
                    {
                        double dist = System.Math.Abs(centerX - selectedCenterX) + System.Math.Abs(rect.Bottom - selectedRect.Top);
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestMatch = container;
                        }
                    }
                }
                else if (key == System.Windows.Input.Key.Down)
                {
                    if (rect.Top >= selectedRect.Bottom - 2)
                    {
                        double dist = System.Math.Abs(centerX - selectedCenterX) + System.Math.Abs(rect.Top - selectedRect.Bottom);
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestMatch = container;
                        }
                    }
                }
            }

            if (bestMatch != null)
            {
                var targetIndex = listBox.ItemContainerGenerator.IndexFromContainer(bestMatch);
                if (targetIndex >= 0 && targetIndex < listBox.Items.Count)
                {
                    listBox.SelectedIndex = targetIndex;
                    FocusListBoxItem(listBox, targetIndex);
                }
            }
            else
            {
                if (listBox.Name == "QuickProductsListBox" && key == System.Windows.Input.Key.Down)
                {
                    if (OtherProductsListBox != null && OtherProductsListBox.Visibility == Visibility.Visible && OtherProductsListBox.Items.Count > 0)
                    {
                        OtherProductsListBox.Focus();
                        OtherProductsListBox.SelectedIndex = 0;
                        FocusListBoxItem(OtherProductsListBox, 0);
                    }
                }
                else if (listBox.Name == "OtherProductsListBox" && key == System.Windows.Input.Key.Up)
                {
                    if (QuickProductsListBox != null && QuickProductsListBox.Visibility == Visibility.Visible && QuickProductsListBox.Items.Count > 0)
                    {
                        QuickProductsListBox.Focus();
                        int targetIdx = QuickProductsListBox.SelectedIndex != -1 ? QuickProductsListBox.SelectedIndex : QuickProductsListBox.Items.Count - 1;
                        QuickProductsListBox.SelectedIndex = targetIdx;
                        FocusListBoxItem(QuickProductsListBox, targetIdx);
                    }
                    else
                    {
                        FocusSearchTextBox();
                    }
                }
                else if (listBox.Name == "QuickProductsListBox" && key == System.Windows.Input.Key.Up)
                {
                    FocusSearchTextBox();
                }
            }
        }

        private void FocusListBoxItem(ListBox listBox, int index)
        {
            var container = listBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
            if (container != null)
            {
                container.Focus();
                System.Windows.Input.Keyboard.Focus(container);
            }
            else
            {
                listBox.UpdateLayout();
                container = listBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                if (container != null)
                {
                    container.Focus();
                    System.Windows.Input.Keyboard.Focus(container);
                }
            }
        }

        private void PosView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoadedOnce)
            {
                _isLoadedOnce = true;
                FocusQuickProducts();
            }

            // Apply grouping based on configuration
            var viewModel = DataContext as PosViewModel;
            if (viewModel != null)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(QuickProductsListBox.ItemsSource);
                if (view != null)
                {
                    view.GroupDescriptions.Clear();
                    QuickProductsListBox.GroupStyle.Clear();
                    if (viewModel.Settings.RapidCardsLayout == "Con divisiones")
                    {
                        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("CategoryName"));
                        if (TryFindResource("QuickProductsGroupStyle") is GroupStyle groupStyle)
                        {
                            QuickProductsListBox.GroupStyle.Add(groupStyle);
                        }
                    }
                }
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
                if (element.Name == "EditPriceModal" || element.Name == "EditQuantityModal")
                {
                    FocusCart();
                }
                else
                {
                    FocusQuickProducts();
                }
            }
        }

        private void FocusOtherProducts()
        {
            if (OtherProductsListBox != null && OtherProductsListBox.Items.Count > 0)
            {
                OtherProductsListBox.Focus();
                if (OtherProductsListBox.SelectedIndex == -1)
                {
                    OtherProductsListBox.SelectedIndex = 0;
                }
                FocusListBoxItem(OtherProductsListBox, OtherProductsListBox.SelectedIndex);
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (QuickProductsListBox != null && QuickProductsListBox.Visibility == Visibility.Visible && QuickProductsListBox.Items.Count > 0)
                {
                    e.Handled = true;
                    FocusQuickProducts();
                }
                else if (OtherProductsListBox != null && OtherProductsListBox.Visibility == Visibility.Visible && OtherProductsListBox.Items.Count > 0)
                {
                    e.Handled = true;
                    FocusOtherProducts();
                }
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

        private void FocusTextBox(System.Windows.Controls.TextBox textBox)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                textBox.Focus();
                System.Windows.Input.Keyboard.Focus(textBox);
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ProductModal_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;
            var modifiers = System.Windows.Input.Keyboard.Modifiers;

            // Ctrl+S to save/submit the product form
            if (key == System.Windows.Input.Key.S && (modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                e.Handled = true;
                if (DataContext is PosViewModel vm)
                {
                    if (vm.SaveNewProductCommand.CanExecute(null))
                    {
                        vm.SaveNewProductCommand.Execute(null);
                    }
                }
                return;
            }

            // Enter to move to next control in the form
            if (key == System.Windows.Input.Key.Enter)
            {
                if (CategoryComboBox != null && CategoryComboBox.IsDropDownOpen)
                {
                    return; // Let standard enter select item and close dropdown
                }

                var focusedElement = System.Windows.Input.Keyboard.FocusedElement;
                if (focusedElement is System.Windows.UIElement uiElement && focusedElement is not System.Windows.Controls.Button)
                {
                    e.Handled = true;
                    uiElement.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));

                    // Si el nuevo elemento enfocado es el ComboBox de Categorías, lo abrimos automáticamente
                    if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.ComboBox newFocused)
                    {
                        newFocused.IsDropDownOpen = true;
                    }
                }
                return;
            }

            // Atajos directos para RadioButtons y Checkbox
            if ((modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                if (key == System.Windows.Input.Key.U)
                {
                    e.Handled = true;
                    if (DataContext is PosViewModel vm)
                    {
                        vm.IsProductUnit = true;
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.G)
                {
                    e.Handled = true;
                    if (DataContext is PosViewModel vm)
                    {
                        vm.IsProductBulk = true;
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.R)
                {
                    e.Handled = true;
                    if (DataContext is PosViewModel vm)
                    {
                        vm.ToggleQuickProduct();
                    }
                    return;
                }
            }

            // Navegación con Flecha Arriba/Abajo o Ctrl+W
            bool moveUp = (key == System.Windows.Input.Key.Up) || 
                          (key == System.Windows.Input.Key.W && (modifiers & System.Windows.Input.ModifierKeys.Control) != 0);
            bool moveDown = (key == System.Windows.Input.Key.Down);

            if (moveUp || moveDown)
            {
                // Si el ComboBox de categorías tiene su menú desplegable abierto,
                // permitir usar las flechas para navegar por la lista nativamente.
                if (CategoryComboBox != null && CategoryComboBox.IsDropDownOpen)
                {
                    if (key == System.Windows.Input.Key.Up || key == System.Windows.Input.Key.Down)
                    {
                        return;
                    }
                }

                e.Handled = true;
                var direction = moveDown ? System.Windows.Input.FocusNavigationDirection.Next : System.Windows.Input.FocusNavigationDirection.Previous;

                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.UIElement focusedUI)
                {
                    focusedUI.MoveFocus(new System.Windows.Input.TraversalRequest(direction));

                    // Si el nuevo elemento enfocado es el ComboBox de Categorías, lo abrimos automáticamente
                    if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.ComboBox newFocused)
                    {
                        newFocused.IsDropDownOpen = true;
                    }
                }
            }
        }

        private void UserControl_PreviewLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            var activeModal = GetActiveModalBorder();
            if (activeModal == null)
                return;

            if (e.NewFocus is System.Windows.DependencyObject newFocusDep && !IsDescendantOf(newFocusDep, activeModal))
            {
                e.Handled = true;

                bool isBackward = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Up) ||
                                  System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Left) ||
                                  (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Tab) &&
                                   (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0);

                var focusable = GetFocusableElements(activeModal);
                if (focusable.Count > 0)
                {
                    System.Windows.UIElement target = isBackward ? focusable[focusable.Count - 1] : focusable[0];
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        target.Focus();
                        System.Windows.Input.Keyboard.Focus(target);
                        if (target is System.Windows.Controls.TextBox tb)
                        {
                            tb.SelectAll();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        private System.Windows.FrameworkElement? GetActiveModalBorder()
        {
            if (IconSelectorModalBorder != null && IconSelectorModalBorder.IsVisible) return IconSelectorModalBorder;
            if (ProductModalBorder != null && ProductModalBorder.IsVisible) return ProductModalBorder;
            return null;
        }

        private bool IsDescendantOf(System.Windows.DependencyObject? element, System.Windows.DependencyObject parent)
        {
            while (element != null)
            {
                if (element == parent)
                    return true;

                if (element.GetType().Name == "PopupRoot" && element is System.Windows.FrameworkElement fe)
                {
                    if (fe.Parent is System.Windows.Controls.Primitives.Popup popup)
                    {
                        element = popup.PlacementTarget ?? popup.Parent;
                        continue;
                    }
                }

                var next = System.Windows.Media.VisualTreeHelper.GetParent(element);
                if (next == null)
                {
                    next = System.Windows.LogicalTreeHelper.GetParent(element);
                }
                element = next;
            }
            return false;
        }

        private System.Collections.Generic.List<System.Windows.UIElement> GetFocusableElements(System.Windows.DependencyObject container)
        {
            var list = new System.Collections.Generic.List<System.Windows.UIElement>();
            FindFocusableElementsRecursive(container, list);
            return list;
        }

        private void FindFocusableElementsRecursive(System.Windows.DependencyObject parent, System.Collections.Generic.List<System.Windows.UIElement> list)
        {
            if (parent == null) return;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.UIElement uiElem)
                {
                    if (uiElem.Focusable && uiElem.IsVisible && uiElem.IsEnabled && System.Windows.Input.KeyboardNavigation.GetIsTabStop(uiElem))
                    {
                        list.Add(uiElem);
                    }
                }
                FindFocusableElementsRecursive(child, list);
            }
        }
    }
}

