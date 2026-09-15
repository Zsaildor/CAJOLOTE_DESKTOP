using System.Windows;
using System.Windows.Controls;
using Cajolote.Views;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.Services;
using Cajolote.ViewModels;
using Cajolote.Data;
using CommunityToolkit.Mvvm.Messaging;

namespace Cajolote
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Configurar el Scanner KeyHook global a la ventana principal
            var scanner = App.Current.Services.GetRequiredService<BarcodeScannerService>();
            scanner.Attach(this);
            scanner.BarcodeScanned += Scanner_BarcodeScanned;

            // Registrarse para recibir mensajes de diálogo modales
            WeakReferenceMessenger.Default.Register<Cajolote.Messages.ShowModalMessage>(this, (r, m) =>
            {
                this.Dispatcher.Invoke(() => 
                {
                    if (this.IsVisible)
                    {
                        ShowMessageOverlay(m);
                    }
                });
            });

            // Registrarse para recibir mensajes de actualización de perfil
            WeakReferenceMessenger.Default.Register<Cajolote.Messages.ProfileSyncedMessage>(this, (r, m) =>
            {
                this.Dispatcher.Invoke(() => 
                {
                    _ = LoadProfileLogoAsync();
                });
            });

            // Suscribirse a cambios de conectividad a internet
            var networkService = App.Current.Services.GetRequiredService<Cajolote.Services.NetworkService>();
            UpdateConnectionStatus(networkService.IsOnline);
            networkService.ConnectivityChanged += NetworkService_ConnectivityChanged;

            _ = LoadProfileLogoAsync();

            // Track focus for restoration
            GotKeyboardFocus += MainWindow_GotKeyboardFocus;
            SizeChanged += MainWindow_SizeChanged;
            Activated += MainWindow_Activated;
        }

        private async System.Threading.Tasks.Task LoadProfileLogoAsync()
        {
            string storeName = "Cajolote";
            string? imagePath = null;

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var context = new CajoloteDbContext())
                    {
                        var profile = context.StoreProfiles.FirstOrDefault();
                        if (profile != null)
                        {
                            storeName = !string.IsNullOrEmpty(profile.StoreName) ? profile.StoreName : "Cajolote";

                            if (!string.IsNullOrEmpty(profile.ImagePath) && System.IO.File.Exists(profile.ImagePath))
                            {
                                imagePath = profile.ImagePath;
                            }
                        }
                    }
                });

                if (imagePath != null)
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new System.Uri(imagePath, System.UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ProfileLogoBrush.ImageSource = bitmap;
                    ProfileLogoBorder.Visibility = Visibility.Visible;
                    DefaultLogoBorder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ProfileLogoBorder.Visibility = Visibility.Collapsed;
                    DefaultLogoBorder.Visibility = Visibility.Visible;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar logo de perfil en MainWindow: {ex.Message}");
                ProfileLogoBorder.Visibility = Visibility.Collapsed;
                DefaultLogoBorder.Visibility = Visibility.Visible;
            }

            TxtStoreName.Text = storeName;
        }

        private void Scanner_BarcodeScanned(object? sender, string barcode)
        {
            // Si la vista actual es la de Ventas, enviamos el código escaneado
            if (MainContent.Content is PosView posView && posView.DataContext is PosViewModel vm)
            {
                vm.AddProductCommand.Execute(barcode);
            }
            // Si la vista actual es la de Revisar Precio, le pasamos el código y ejecutamos la búsqueda
            else if (MainContent.Content is RevisarPrecioView revView && revView.DataContext is RevisarPrecioViewModel revVm)
            {
                revVm.BarcodeInput = barcode;
                revVm.CheckPriceCommand.Execute(null);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BackupOverlay.Visibility = Visibility.Visible;
            BackupOverlayStatusText.Text = "Sincronizando datos de la cuenta...";

            // Dynamically set the synchronization icon
            if (Application.Current.TryFindResource("di_SyncImage") is System.Windows.Media.DrawingImage syncImg)
            {
                BackupOverlayIcon.Source = syncImg;
            }

            // Yield control to let WPF draw the overlay and status text
            await System.Threading.Tasks.Task.Delay(300);

            // 1. Sincronizar perfil con Firestore
            try
            {
                var syncService = App.Current.Services.GetRequiredService<SyncService>();
                await syncService.SyncStoreProfileAsync();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al sincronizar perfil en inicio: {ex.Message}");
            }

            // Yield control so any UI updates from the synchronization are processed and the message transition looks smooth
            await System.Threading.Tasks.Task.Delay(300);

            // 2. Cambiar mensaje a optimización de base de datos
            BackupOverlayStatusText.Text = "Optimizando y respaldando ventas...";

            // Dynamically set the optimization/preparing icon
            if (Application.Current.TryFindResource("di_Image") is System.Windows.Media.DrawingImage prepImg)
            {
                BackupOverlayIcon.Source = prepImg;
            }

            // Yield control to let WPF render the new status text
            await System.Threading.Tasks.Task.Delay(300);

            // 3. Ejecutar la optimización
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using var context = new CajoloteDbContext();
                    Cajolote.Services.SalesArchiver.ArchiveOldSalesIfNeeded(context);
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al archivar ventas: {ex.Message}");
                }
            });

            BackupOverlay.Visibility = Visibility.Collapsed;
            NavigateTo(new DashboardView());

            // 4. Verificación mensual de respaldo en segundo plano (evalúa primero >= 30 días, luego internet)
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var backupService = App.Current.Services.GetService<Cajolote.Services.DatabaseBackupService>();
                    if (backupService != null)
                    {
                        await backupService.CheckAndPerformMonthlyBackupAsync();
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Error en verificación mensual de respaldo: {ex.Message}");
                }
            });
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Alt)
            {
                var key = e.SystemKey == System.Windows.Input.Key.None ? e.Key : e.SystemKey;
                switch (key)
                {
                    case System.Windows.Input.Key.D1:
                    case System.Windows.Input.Key.NumPad1:
                        Main_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D2:
                    case System.Windows.Input.Key.NumPad2:
                        NuevaCompra_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D3:
                    case System.Windows.Input.Key.NumPad3:
                        RevisarPrecio_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D4:
                    case System.Windows.Input.Key.NumPad4:
                        Products_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D5:
                    case System.Windows.Input.Key.NumPad5:
                        Recientes_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D6:
                    case System.Windows.Input.Key.NumPad6:
                        Registros_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D7:
                    case System.Windows.Input.Key.NumPad7:
                        Estadisticas_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.M:
                        Estadisticas_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.T:
                        EstadisticasTotales_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D8:
                    case System.Windows.Input.Key.NumPad8:
                        Notas_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D9:
                    case System.Windows.Input.Key.NumPad9:
                        Configuracion_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case System.Windows.Input.Key.D0:
                    case System.Windows.Input.Key.NumPad0:
                        Perfil_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                }
            }
        }

        private void Main_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new DashboardView());
        }

        private void NuevaCompra_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new PosView());
        }

        private void RevisarPrecio_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new RevisarPrecioView());
        }

        private void Products_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new ProductosView());
        }

        private void Recientes_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new RecientesView());
        }

        private void Registros_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new RegistrosView());
        }

        private void Estadisticas_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new EstadisticasView());
        }

        private void EstadisticasTotales_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new EstadisticasTotalesView());
        }

        private void Notas_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new NotasView());
        }

        private void Configuracion_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new ConfiguracionView());
        }

        private void Perfil_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateToAsync(() => new PerfilView());
        }

        public void NavigateTo(UserControl view)
        {
            MainContent.Content = view;
            UpdateActiveMenuItem(view);
        }

        public async System.Threading.Tasks.Task NavigateToAsync(System.Func<UserControl> viewFactory)
        {
            ViewLoadingStatusText.Text = "Cargando...";
            ViewLoadingOverlay.Visibility = Visibility.Visible;

            await System.Threading.Tasks.Task.Delay(50);

            try
            {
                UserControl view = viewFactory();
                MainContent.Content = view;
                UpdateActiveMenuItem(view);
            }
            finally
            {
                ViewLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateActiveMenuItem(UserControl view)
        {
            if (BtnMenu == null) return;
            
            BtnMenu.IsChecked = false;
            BtnNuevaVenta.IsChecked = false;
            BtnRevisarPrecio.IsChecked = false;
            BtnProductos.IsChecked = false;
            BtnVentasRecientes.IsChecked = false;
            BtnVentasTotales.IsChecked = false;
            BtnEstadisticas.IsChecked = false;
            BtnNotas.IsChecked = false;
            BtnConfiguracion.IsChecked = false;
            BtnPerfil.IsChecked = false;

            if (view is DashboardView) BtnMenu.IsChecked = true;
            else if (view is PosView) BtnNuevaVenta.IsChecked = true;
            else if (view is RevisarPrecioView) BtnRevisarPrecio.IsChecked = true;
            else if (view is ProductosView) BtnProductos.IsChecked = true;
            else if (view is RecientesView) BtnVentasRecientes.IsChecked = true;
            else if (view is RegistrosView) BtnVentasTotales.IsChecked = true;
            else if (view is EstadisticasView || view is EstadisticasTotalesView) BtnEstadisticas.IsChecked = true;
            else if (view is NotasView) BtnNotas.IsChecked = true;
            else if (view is ConfiguracionView) BtnConfiguracion.IsChecked = true;
            else if (view is PerfilView) BtnPerfil.IsChecked = true;
        }

        private Cajolote.Messages.ShowModalMessage? _currentModalMessage;

        private void ShowMessageOverlay(Cajolote.Messages.ShowModalMessage msg)
        {
            _currentModalMessage = msg;
            OverlayTitle.Text = msg.Title;
            OverlayMessage.Text = msg.Message;

            // Configure Icon
            switch (msg.Icon)
            {
                case MessageBoxImage.Error:
                    OverlayIcon.Text = "❌";
                    break;
                case MessageBoxImage.Warning:
                    OverlayIcon.Text = "⚠️";
                    break;
                case MessageBoxImage.Question:
                    OverlayIcon.Text = "❓";
                    break;
                case MessageBoxImage.Information:
                default:
                    OverlayIcon.Text = "🏪";
                    break;
            }

            // Configure Buttons
            switch (msg.Button)
            {
                case MessageBoxButton.OKCancel:
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.Content = "Cancelar";
                    BtnOk.Content = "Aceptar";
                    break;
                case MessageBoxButton.YesNo:
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.Content = "No";
                    BtnOk.Content = "Sí";
                    break;
                case MessageBoxButton.OK:
                default:
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnOk.Content = "Aceptar";
                    break;
            }

            NotificationOverlay.Visibility = Visibility.Visible;
            BtnOk.Focus();
        }

        private void OverlayOk_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModalMessage != null)
            {
                var result = BtnOk.Content.ToString() == "Sí" ? MessageBoxResult.Yes : MessageBoxResult.OK;
                _currentModalMessage.Tcs.SetResult(result);
                _currentModalMessage = null;
            }
            NotificationOverlay.Visibility = Visibility.Collapsed;
        }

        private void OverlayCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModalMessage != null)
            {
                var result = BtnCancel.Content.ToString() == "No" ? MessageBoxResult.No : MessageBoxResult.Cancel;
                _currentModalMessage.Tcs.SetResult(result);
                _currentModalMessage = null;
            }
            NotificationOverlay.Visibility = Visibility.Collapsed;
        }

        private void NetworkService_ConnectivityChanged(object? sender, bool isOnline)
        {
            this.Dispatcher.Invoke(() =>
            {
                UpdateConnectionStatus(isOnline);
            });
        }

        private void UpdateConnectionStatus(bool isOnline)
        {
            if (isOnline)
            {
                ConnectionStatusDot.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(118, 200, 147)); // #76C893
                TxtConnectionStatus.Text = "En línea";
                TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 231, 231)); // #EDE7E7
            }
            else
            {
                ConnectionStatusDot.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 126, 128)); // #E67E80
                TxtConnectionStatus.Text = "Sin conexión";
                TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 215, 215));
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            WeakReferenceMessenger.Default.Unregister<Cajolote.Messages.ShowModalMessage>(this);
            WeakReferenceMessenger.Default.Unregister<Cajolote.Messages.ProfileSyncedMessage>(this);

            var networkService = App.Current.Services.GetService<Cajolote.Services.NetworkService>();
            if (networkService != null)
            {
                networkService.ConnectivityChanged -= NetworkService_ConnectivityChanged;
            }

            var scanner = App.Current.Services.GetService<Cajolote.Services.BarcodeScannerService>();
            if (scanner != null)
            {
                scanner.BarcodeScanned -= Scanner_BarcodeScanned;
                scanner.Detach(this);
            }

            base.OnClosed(e);
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void ToggleMaximizeRestore()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                BtnMaximizeRestore.Content = "🗖";
                BtnMaximizeRestore.ToolTip = "Maximizar";
                CenterWindowOnScreen();
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                BtnMaximizeRestore.Content = "🗗";
                BtnMaximizeRestore.ToolTip = "Restaurar tamaño";
            }
            RestoreFocusedElement();
        }

        private void BtnMaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CenterWindowOnScreen()
        {
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;
            double windowWidth = this.Width;
            double windowHeight = this.Height;

            this.Left = SystemParameters.WorkArea.Left + (screenWidth - windowWidth) / 2;
            this.Top = SystemParameters.WorkArea.Top + (screenHeight - windowHeight) / 2;
        }

        private void Sidebar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximizeRestore();
                }
                else
                {
                    if (this.WindowState == WindowState.Maximized)
                    {
                        this.WindowState = WindowState.Normal;
                        BtnMaximizeRestore.Content = "🗖";
                        BtnMaximizeRestore.ToolTip = "Maximizar";
                        CenterWindowOnScreen();
                    }

                    try
                    {
                        this.DragMove();
                    }
                    catch (System.InvalidOperationException)
                    {
                    }
                    RestoreFocusedElement();
                }
            }
        }

        private IInputElement? _lastFocusedElement = null;

        private void MainWindow_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus != null && MainContent != null && IsDescendantOf(e.NewFocus as DependencyObject, MainContent))
            {
                _lastFocusedElement = e.NewFocus;
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RestoreFocusedElement();
        }

        private void MainWindow_Activated(object? sender, System.EventArgs e)
        {
            RestoreFocusedElement();
        }

        private void RestoreFocusedElement()
        {
            if (_lastFocusedElement is UIElement element && element.IsVisible && element.IsEnabled)
            {
                element.Focus();
                System.Windows.Input.Keyboard.Focus(element);
            }
        }

        private bool IsDescendantOf(DependencyObject? child, DependencyObject? parent)
        {
            if (child == null || parent == null) return false;
            DependencyObject? current = child;
            while (current != null)
            {
                if (current == parent) return true;
                DependencyObject? next = System.Windows.Media.VisualTreeHelper.GetParent(current);
                if (next == null)
                {
                    next = LogicalTreeHelper.GetParent(current);
                }
                current = next;
            }
            return false;
        }
    }
}
