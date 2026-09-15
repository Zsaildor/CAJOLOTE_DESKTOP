using System.Configuration;
using System.Data;
using System.Windows;
using Cajolote.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace Cajolote
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public App()
        {
            Cajolote.Services.EnvLoader.Load();
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            
            services.AddDbContext<CajoloteDbContext>(ServiceLifetime.Transient);
            services.AddTransient<PosViewModel>();
            services.AddTransient<ProductosViewModel>();
            services.AddTransient<RevisarPrecioViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<PerfilViewModel>();
            services.AddTransient<RegistrosViewModel>();
            services.AddTransient<NotasViewModel>();
            services.AddTransient<RecientesViewModel>();
            services.AddTransient<EstadisticasViewModel>();
            services.AddTransient<EstadisticasTotalesViewModel>();
            services.AddTransient<ConfiguracionViewModel>();
            
            // Phase 3 Services
            services.AddSingleton<Cajolote.Services.BarcodeScannerService>();
            services.AddSingleton<Cajolote.Services.TicketGenerator>();
            services.AddSingleton<Cajolote.Services.NoteReceiptGenerator>();
            services.AddSingleton<Cajolote.Services.ThermalPrinterService>();
            services.AddSingleton<Cajolote.Services.SalesReportGenerator>();
            services.AddSingleton<Cajolote.Services.SettingsService>();
            services.AddSingleton<Cajolote.Services.ICatalogCacheService, Cajolote.Services.CatalogCacheService>();

            // Authentication & Local Session
            services.AddSingleton<Cajolote.Services.FirebaseAuthService>();
            services.AddSingleton<Cajolote.Services.NetworkService>();
            services.AddSingleton<Cajolote.Services.SyncService>();
            services.AddSingleton<Cajolote.Services.DatabaseBackupService>();

            return services.BuildServiceProvider();
        }

        public new static App Current => (App)Application.Current;

        public static async System.Threading.Tasks.Task<MessageBoxResult> ShowMessageAsync(string message, string title = "Aviso", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var msg = new Cajolote.Messages.ShowModalMessage(message, title, button, icon);
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(msg);
            return await msg.Tcs.Task;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Migrar la base de datos de shopsharp.db a cajolote.db si existe la anterior
            try
            {
                var folder = Environment.SpecialFolder.LocalApplicationData;
                var path = Environment.GetFolderPath(folder);
                var oldDbPath = System.IO.Path.Combine(path, "shopsharp.db");
                var newDbPath = System.IO.Path.Combine(path, "cajolote.db");
                
                if (System.IO.File.Exists(oldDbPath) && !System.IO.File.Exists(newDbPath))
                {
                    System.IO.File.Move(oldDbPath, newDbPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al migrar la base de datos: {ex.Message}");
            }
            using var context = new CajoloteDbContext();
            
            // Si la base de datos ya existía pero no se creó con migraciones (p. ej. EnsureCreated),
            // registramos la migración inicial en el historial para evitar que intente recrear las tablas.
            try
            {
                using var command = context.Database.GetDbConnection().CreateCommand();
                context.Database.OpenConnection();
                
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Categories';";
                var hasCategories = command.ExecuteScalar() != null;
                
                if (hasCategories)
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
                    command.ExecuteNonQuery();
                    
                    command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260914024505_InitialCreate';";
                    var hasInitial = Convert.ToInt32(command.ExecuteScalar()) > 0;
                    
                    if (!hasInitial)
                    {
                        command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20260914024505_InitialCreate', '10.0.0');";
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al verificar historial de migraciones: {ex.Message}");
            }
            finally
            {
                try
                {
                    context.Database.CloseConnection();
                }
                catch {}
            }

            context.Database.Migrate();
            context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            DbSeeder.Seed(context);

            base.OnStartup(e);

            // Conditional startup based on user authentication status
            var authService = Services.GetRequiredService<Cajolote.Services.FirebaseAuthService>();
            
            // Inicializar servicios en segundo plano de monitoreo de red y sincronización
            _ = Services.GetRequiredService<Cajolote.Services.NetworkService>();
            var syncService = Services.GetRequiredService<Cajolote.Services.SyncService>();

            if (authService.IsUserAuthenticated)
            {
                // Warm-up database and load cache asynchronously without blocking UI
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var catalogCache = Services.GetRequiredService<Cajolote.Services.ICatalogCacheService>();
                        await catalogCache.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warm-up failed: {ex.Message}");
                    }
                });

                var mainWindow = new MainWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                var loginWindow = new LoginWindow();
                Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
            }
        }
    }
}
