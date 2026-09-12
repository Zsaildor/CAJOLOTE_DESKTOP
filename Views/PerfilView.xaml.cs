using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;

namespace Cajolote.Views
{
    public partial class PerfilView : UserControl
    {
        private readonly TextBox[] _textBoxes;

        public PerfilView()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<PerfilViewModel>();

            _textBoxes = new[] { OwnerNameTextBox, StoreNameTextBox, RfcTextBox, PhoneTextBox, AddressTextBox };

            Loaded += (s, e) =>
            {
                MainScrollViewer.Focus();
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Activated += ParentWindow_Activated;
                }
            };

            Unloaded += (s, e) =>
            {
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Activated -= ParentWindow_Activated;
                }
            };

            PreviewKeyDown += (s, e) =>
            {
                if (DataContext is PerfilViewModel vm)
                {
                    // 1. Ctrl+I toggles the image actions menu (only when not editing text)
                    if (e.Key == Key.I && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && !IsTextBoxFocused())
                    {
                        EditImageToggleButton.IsChecked = !EditImageToggleButton.IsChecked;
                        e.Handled = true;
                    }
                    // 1b. Ctrl+A for changing image (only when menu is open and not editing text)
                    else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && !IsTextBoxFocused())
                    {
                        if (EditImageToggleButton.IsChecked == true)
                        {
                            if (vm.SelectImageCommand.CanExecute(null))
                            {
                                vm.SelectImageCommand.Execute(null);
                                EditImageToggleButton.IsChecked = false;
                                e.Handled = true;
                            }
                        }
                    }
                    // 2. Ctrl+Delete for deleting image (only when not editing text)
                    else if (e.Key == Key.Delete && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && !IsTextBoxFocused())
                    {
                        if (EditImageToggleButton.IsChecked == true)
                        {
                            if (vm.RemoveImageCommand.CanExecute(null))
                            {
                                vm.RemoveImageCommand.Execute(null);
                                EditImageToggleButton.IsChecked = false;
                                e.Handled = true;
                            }
                        }
                    }
                    // 3. Ctrl+S for saving changes
                    else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        if (vm.SaveProfileCommand.CanExecute(null))
                        {
                            vm.SaveProfileCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                    // 4. Ctrl+Shift+Z for Logout
                    else if (e.Key == Key.Z && 
                             (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && 
                             (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        if (vm.LogoutCommand.CanExecute(null))
                        {
                            vm.LogoutCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                    // 5. Ctrl+E for editing/focusing or defocusing textboxes
                    else if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        if (IsTextBoxFocused())
                        {
                            // Defocus and focus main scrollviewer
                            Keyboard.ClearFocus();
                            MainScrollViewer.Focus();
                        }
                        else
                        {
                            // Focus first textbox
                            OwnerNameTextBox.Focus();
                            OwnerNameTextBox.SelectAll();
                        }
                        e.Handled = true;
                    }
                    // 6. Navigation keys when a textbox is focused
                    else if (IsTextBoxFocused())
                    {
                        if (e.Key == Key.Down || e.Key == Key.Enter)
                        {
                            MoveFocus(1);
                            e.Handled = true;
                        }
                        else if (e.Key == Key.Up)
                        {
                            MoveFocus(-1);
                            e.Handled = true;
                        }
                    }
                }
            };
        }

        private void ParentWindow_Activated(object? sender, EventArgs e)
        {
            if (!IsTextBoxFocused())
            {
                MainScrollViewer.Focus();
            }
        }

        private bool IsTextBoxFocused()
        {
            foreach (var tb in _textBoxes)
            {
                if (tb != null && tb.IsFocused)
                {
                    return true;
                }
            }
            return false;
        }

        private void MoveFocus(int direction)
        {
            int currentIndex = -1;
            for (int i = 0; i < _textBoxes.Length; i++)
            {
                if (_textBoxes[i].IsFocused)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex != -1)
            {
                int nextIndex = currentIndex + direction;
                if (nextIndex >= 0 && nextIndex < _textBoxes.Length)
                {
                    _textBoxes[nextIndex].Focus();
                    _textBoxes[nextIndex].SelectAll();
                }
                else if (nextIndex < 0)
                {
                    _textBoxes[_textBoxes.Length - 1].Focus();
                    _textBoxes[_textBoxes.Length - 1].SelectAll();
                }
                else if (nextIndex >= _textBoxes.Length)
                {
                    _textBoxes[0].Focus();
                    _textBoxes[0].SelectAll();
                }
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            EditImageToggleButton.IsChecked = false;
        }
    }
}
