using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class RevisarPrecioView : UserControl
    {
        public RevisarPrecioView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<RevisarPrecioViewModel>();
            Loaded += (s, e) => FocusBarcodeTextBox();
        }

        private void FocusBarcodeTextBox()
        {
            if (BarcodeTextBox == null) return;
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                BarcodeTextBox.Focus();
                System.Windows.Input.Keyboard.Focus(BarcodeTextBox);
                BarcodeTextBox.SelectAll();
            }));
        }

        private void Modal_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // Solo regresar el foco al TextBox si ambos modales están cerrados (no visibles)
            if (EditProductModal != null && IconSelectorModal != null && !EditProductModal.IsVisible && !IconSelectorModal.IsVisible)
            {
                FocusBarcodeTextBox();
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

        private void ConfirmButton_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.IsVisible)
            {
                Dispatcher.BeginInvoke(new System.Action(() => btn.Focus()));
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
                if (DataContext is RevisarPrecioViewModel vm)
                {
                    if (vm.SaveProductCommand.CanExecute(null))
                    {
                        vm.SaveProductCommand.Execute(null);
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
                    if (DataContext is RevisarPrecioViewModel vm)
                    {
                        vm.IsProductUnit = true;
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.G)
                {
                    e.Handled = true;
                    if (DataContext is RevisarPrecioViewModel vm)
                    {
                        vm.IsProductBulk = true;
                    }
                    return;
                }
                else if (key == System.Windows.Input.Key.R)
                {
                    e.Handled = true;
                    if (DataContext is RevisarPrecioViewModel vm)
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
