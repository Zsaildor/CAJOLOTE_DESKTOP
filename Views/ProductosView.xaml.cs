using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class ProductosView : UserControl
    {
        public ProductosView()
        {
            InitializeComponent();
            var viewModel = App.Current.Services.GetRequiredService<ProductosViewModel>();
            DataContext = viewModel;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += ProductosView_Loaded;
            Unloaded += ProductosView_Unloaded;
        }

        private void ProductosView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ProductosViewModel vm)
            {
                vm.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductosViewModel.IsManagingCategories))
            {
                if (DataContext is ProductosViewModel vm)
                {
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        if (vm.IsManagingCategories)
                        {
                            FocusActiveTable();
                        }
                        else
                        {
                            FocusSearchTextBox();
                        }
                    }));
                }
            }
        }

        private void ProductosView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            RestoreDefaultFocus();
        }

        private void RestoreDefaultFocus()
        {
            if (DataContext is ProductosViewModel vm)
            {
                if (vm.IsManagingProducts)
                {
                    FocusSearchTextBox();
                }
                else
                {
                    FocusActiveTable();
                }
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
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void Modal_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && !element.IsVisible)
            {
                if (DataContext is ProductosViewModel vm && vm.IsEditing)
                {
                    if (vm.ProductHasNoBarcode)
                    {
                        FocusTextBox(NameTextBox);
                    }
                    else
                    {
                        FocusTextBox(BarcodeTextBox);
                    }
                }
                else
                {
                    RestoreDefaultFocus();
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

        private void FocusTextBox(System.Windows.Controls.TextBox textBox)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                textBox.Focus();
                System.Windows.Input.Keyboard.Focus(textBox);
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BarcodeTextBox_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.IsVisible && textBox.IsEnabled)
            {
                FocusTextBox(textBox);
            }
        }

        private void NameTextBox_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.IsVisible)
            {
                if (DataContext is ProductosViewModel vm && vm.ProductHasNoBarcode)
                {
                    FocusTextBox(textBox);
                }
            }
        }

        private void CategoryNameTextBox_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.IsVisible)
            {
                FocusTextBox(textBox);
            }
        }

        private void ProductModal_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;
            var modifiers = System.Windows.Input.Keyboard.Modifiers;

            // Ctrl+S to save/submit the product form
            if (key == System.Windows.Input.Key.S && (modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                e.Handled = true;
                if (DataContext is ProductosViewModel vm)
                {
                    if (vm.SaveCommand.CanExecute(null))
                    {
                        vm.SaveCommand.Execute(null);
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
                    if (DataContext is ProductosViewModel vm)
                    {
                        vm.IsProductUnit = true;
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.G)
                {
                    e.Handled = true;
                    if (DataContext is ProductosViewModel vm)
                    {
                        vm.IsProductBulk = true;
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.N)
                {
                    e.Handled = true;
                    if (DataContext is ProductosViewModel vm)
                    {
                        vm.ProductHasNoBarcode = !vm.ProductHasNoBarcode;
                        if (vm.ProductHasNoBarcode)
                        {
                            FocusTextBox(NameTextBox);
                        }
                        else
                        {
                            FocusTextBox(BarcodeTextBox);
                        }
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.R)
                {
                    e.Handled = true;
                    if (DataContext is ProductosViewModel vm)
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

        private void SearchTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down)
            {
                e.Handled = true;
                if (ProductsListBox != null && ProductsListBox.IsVisible)
                {
                    ProductsListBox.Focus();
                    if (ProductsListBox.SelectedIndex == -1 && ProductsListBox.Items.Count > 0)
                    {
                        ProductsListBox.SelectedIndex = 0;
                    }
                    if (ProductsListBox.SelectedItem != null)
                    {
                        var container = ProductsListBox.ItemContainerGenerator.ContainerFromItem(ProductsListBox.SelectedItem) as System.Windows.UIElement;
                        container?.Focus();
                    }
                }
                else if (CategoriesListBox != null && CategoriesListBox.IsVisible)
                {
                    CategoriesListBox.Focus();
                    if (CategoriesListBox.SelectedIndex == -1 && CategoriesListBox.Items.Count > 0)
                    {
                        CategoriesListBox.SelectedIndex = 0;
                    }
                    if (CategoriesListBox.SelectedItem != null)
                    {
                        var container = CategoriesListBox.ItemContainerGenerator.ContainerFromItem(CategoriesListBox.SelectedItem) as System.Windows.UIElement;
                        container?.Focus();
                    }
                }
            }
        }

        private void FocusActiveTable()
        {
            if (ProductsListBox != null && ProductsListBox.IsVisible)
            {
                ProductsListBox.Focus();
                if (ProductsListBox.SelectedIndex == -1 && ProductsListBox.Items.Count > 0)
                {
                    ProductsListBox.SelectedIndex = 0;
                }
                if (ProductsListBox.SelectedItem != null)
                {
                    var container = ProductsListBox.ItemContainerGenerator.ContainerFromItem(ProductsListBox.SelectedItem) as System.Windows.UIElement;
                    container?.Focus();
                }
            }
            else if (CategoriesListBox != null && CategoriesListBox.IsVisible)
            {
                CategoriesListBox.Focus();
                if (CategoriesListBox.SelectedIndex == -1 && CategoriesListBox.Items.Count > 0)
                {
                    CategoriesListBox.SelectedIndex = 0;
                }
                if (CategoriesListBox.SelectedItem != null)
                {
                    var container = CategoriesListBox.ItemContainerGenerator.ContainerFromItem(CategoriesListBox.SelectedItem) as System.Windows.UIElement;
                    container?.Focus();
                }
            }
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;
            var modifiers = System.Windows.Input.Keyboard.Modifiers;

            if (DataContext is ProductosViewModel vm)
            {
                // Si hay un modal abierto, no aplicar atajos globales de la lista
                if (vm.IsEditing || vm.IsEditingCategory || vm.IsDeleteConfirmationOpen || vm.IsDeleteCategoryConfirmationOpen || vm.IsIconSelectorOpen)
                {
                    return;
                }

                if (key == System.Windows.Input.Key.Up)
                {
                    if (ProductsListBox != null && ProductsListBox.IsVisible && ProductsListBox.IsKeyboardFocusWithin && ProductsListBox.SelectedIndex == 0)
                    {
                        e.Handled = true;
                        FocusSearchTextBox();
                        return;
                    }
                    else if (CategoriesListBox != null && CategoriesListBox.IsVisible && CategoriesListBox.IsKeyboardFocusWithin && CategoriesListBox.SelectedIndex == 0)
                    {
                        e.Handled = true;
                        FocusSearchTextBox();
                        return;
                    }
                }

                if ((modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    switch (key)
                    {
                        case System.Windows.Input.Key.F:
                            e.Handled = true;
                            SearchTextBox.Focus();
                            SearchTextBox.SelectAll();
                            break;
                        case System.Windows.Input.Key.P:
                            e.Handled = true;
                            if (vm.AddNewCommand.CanExecute(null))
                                vm.AddNewCommand.Execute(null);
                            break;
                        case System.Windows.Input.Key.G:
                            e.Handled = true;
                            if (vm.AddNewCategoryCommand.CanExecute(null))
                                vm.AddNewCategoryCommand.Execute(null);
                            break;
                        case System.Windows.Input.Key.T:
                            e.Handled = true;
                            if (vm.ToggleManageCommand.CanExecute(null))
                                vm.ToggleManageCommand.Execute(null);
                            FocusSearchTextBox();
                            break;
                        case System.Windows.Input.Key.O:
                            e.Handled = true;
                            SearchTypeComboBox.Focus();
                            SearchTypeComboBox.IsDropDownOpen = true;
                            break;
                        case System.Windows.Input.Key.L:
                            e.Handled = true;
                            LimitComboBox.Focus();
                            LimitComboBox.IsDropDownOpen = true;
                            break;
                        case System.Windows.Input.Key.E:
                            e.Handled = true;
                            TriggerEditForSelected(vm);
                            break;
                        case System.Windows.Input.Key.Delete:
                            e.Handled = true;
                            TriggerDeleteForSelected(vm);
                            break;
                    }
                }
                else if (key == System.Windows.Input.Key.Delete)
                {
                    e.Handled = true;
                    TriggerDeleteForSelected(vm);
                }
            }
        }

        private void TriggerEditForSelected(ProductosViewModel vm)
        {
            if (ProductsListBox.IsVisible && ProductsListBox.SelectedItem is Cajolote.Models.Product selectedProduct)
            {
                if (vm.EditCommand.CanExecute(selectedProduct))
                    vm.EditCommand.Execute(selectedProduct);
            }
            else if (CategoriesListBox.IsVisible && CategoriesListBox.SelectedItem is Cajolote.Models.Category selectedCategory)
            {
                if (vm.EditCategoryCommand.CanExecute(selectedCategory))
                    vm.EditCategoryCommand.Execute(selectedCategory);
            }
        }

        private void TriggerDeleteForSelected(ProductosViewModel vm)
        {
            if (ProductsListBox.IsVisible && ProductsListBox.SelectedItem is Cajolote.Models.Product selectedProduct)
            {
                if (vm.DeleteCommand.CanExecute(selectedProduct))
                    vm.DeleteCommand.Execute(selectedProduct);
            }
            else if (CategoriesListBox.IsVisible && CategoriesListBox.SelectedItem is Cajolote.Models.Category selectedCategory)
            {
                if (vm.DeleteCategoryCommand.CanExecute(selectedCategory))
                    vm.DeleteCategoryCommand.Execute(selectedCategory);
            }
        }

        private void ComboBox_DropDownClosed(object sender, System.EventArgs e)
        {
            FocusSearchTextBox();
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
            if (CategoryModalBorder != null && CategoryModalBorder.IsVisible) return CategoryModalBorder;
            if (DeleteProductModalBorder != null && DeleteProductModalBorder.IsVisible) return DeleteProductModalBorder;
            if (DeleteCategoryModalBorder != null && DeleteCategoryModalBorder.IsVisible) return DeleteCategoryModalBorder;
            return null;
        }

        private bool IsDescendantOf(System.Windows.DependencyObject? element, System.Windows.DependencyObject parent)
        {
            while (element != null)
            {
                if (element == parent)
                    return true;

                // Check if the element is hosted inside a Popup
                if (element.GetType().Name == "PopupRoot" && element is System.Windows.FrameworkElement fe)
                {
                    if (fe.Parent is System.Windows.Controls.Primitives.Popup popup)
                    {
                        element = popup.PlacementTarget ?? popup.Parent;
                        continue;
                    }
                }

                // Try to get visual parent first, then logical parent
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
