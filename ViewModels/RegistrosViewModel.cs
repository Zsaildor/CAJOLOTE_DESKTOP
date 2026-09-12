using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;
using Cajolote.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.Services;


namespace Cajolote.ViewModels;

public partial class RegistrosViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;
    private readonly SettingsService _settingsService;
    public ISnackbarMessageQueue ErrorMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

    private readonly SemaphoreSlim _dbSemaphore = new(1, 1);
    private CancellationTokenSource? _cts;
    private System.Threading.Timer? _debounceTimer;

    [ObservableProperty]
    private bool _isLoadingData;

    [ObservableProperty]
    private int _selectedFilterIndex = 1; // Default to "Esta semana" (Index 1)

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedSearchTypeIndex = 0; // Default to "Por folio" (Index 0)

    partial void OnSelectedFilterIndexChanged(int value)
    {
        LoadSales();
    }

    partial void OnSearchTextChanged(string value)
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            App.Current?.Dispatcher?.Invoke(() => LoadSales());
        }, null, 300, Timeout.Infinite);
    }

    partial void OnSelectedSearchTypeIndexChanged(int value)
    {
        LoadSales();
    }

    public RegistrosViewModel(CajoloteDbContext dbContext, SettingsService settingsService)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        LoadSales();
    }

    public ObservableCollection<Sale> Sales { get; } = new();

    private void LoadSales()
    {
        _ = LoadSalesAsync();
    }

    private async Task LoadSalesAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        IsLoadingData = true;
        try
        {
            await _dbSemaphore.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();

                var sortedList = await Task.Run(() => GetSalesList(50), token);

                token.ThrowIfCancellationRequested();

                Sales.Clear();
                foreach (var s in sortedList) Sales.Add(s);
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al cargar ventas: {ex.Message}");
        }
        finally
        {
            if (_cts == cts)
            {
                IsLoadingData = false;
            }
        }
    }

    private List<Sale> GetSalesList(int limit)
    {
        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var startOfPrevMonth = startOfMonth.AddMonths(-1);
        var startOfYear = new DateTime(today.Year, 1, 1);

        IQueryable<Sale> activeQuery = _dbContext.Sales.Include(s => s.Details).ThenInclude(d => d.Product).Include(s => s.Note);
        IQueryable<HistoricalSale> histQuery = _dbContext.HistoricalSales.Include(s => s.Details).ThenInclude(d => d.Product);

        bool includeActive = false;
        bool includeHistorical = false;

        switch (SelectedFilterIndex)
        {
            case 0: // Hoy
                activeQuery = activeQuery.Where(s => s.Date >= today);
                includeActive = true;
                break;
            case 1: // Esta semana
                activeQuery = activeQuery.Where(s => s.Date >= startOfWeek);
                includeActive = true;
                if (startOfWeek < startOfMonth)
                {
                    histQuery = histQuery.Where(s => s.Date >= startOfWeek && s.Date < startOfMonth);
                    includeHistorical = true;
                }
                break;
            case 2: // Este mes
                activeQuery = activeQuery.Where(s => s.Date >= startOfMonth);
                includeActive = true;
                break;
            case 3: // Mes anterior
                histQuery = histQuery.Where(s => s.Date >= startOfPrevMonth && s.Date < startOfMonth);
                includeHistorical = true;
                break;
            case 4: // Este año
                activeQuery = activeQuery.Where(s => s.Date >= startOfYear);
                includeActive = true;
                histQuery = histQuery.Where(s => s.Date >= startOfYear);
                includeHistorical = true;
                break;
            case 5: // Todos
            default:
                includeActive = true;
                includeHistorical = true;
                break;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();
            switch (SelectedSearchTypeIndex)
            {
                case 0: // Por folio
                    if (includeActive)
                        activeQuery = activeQuery.Where(s => s.Id.ToString().Contains(text));
                    if (includeHistorical)
                        histQuery = histQuery.Where(s => s.Id.ToString().Contains(text));
                    break;
                case 1: // Por monto total
                    var cleanText = text.Replace("$", "").Replace(",", "").Trim();
                    if (decimal.TryParse(cleanText, out decimal amount))
                    {
                        if (includeActive)
                            activeQuery = activeQuery.Where(s => s.Total == amount);
                        if (includeHistorical)
                            histQuery = histQuery.Where(s => s.Total == amount);
                    }
                    else
                    {
                        if (includeActive)
                            activeQuery = activeQuery.Where(s => s.Total.ToString().Contains(cleanText));
                        if (includeHistorical)
                            histQuery = histQuery.Where(s => s.Total.ToString().Contains(cleanText));
                    }
                    break;
                case 2: // Por producto
                    if (includeActive)
                        activeQuery = activeQuery.Where(s => s.Details.Any(d => d.Product != null && d.Product.Name.ToLower().Contains(text.ToLower())));
                    if (includeHistorical)
                        histQuery = histQuery.Where(s => s.Details.Any(d => d.Product != null && d.Product.Name.ToLower().Contains(text.ToLower())));
                    break;
            }
        }

        List<Sale> combinedList = new List<Sale>();
        if (includeActive)
        {
            combinedList.AddRange(activeQuery.OrderByDescending(s => s.Date).Take(limit).ToList());
        }

        if (includeHistorical)
        {
            var historicalSales = histQuery
                .OrderByDescending(s => s.Date)
                .Take(limit == int.MaxValue ? int.MaxValue : Math.Max(500, limit * 2))
                .Select(hs => new Sale
                {
                    Id = hs.Id,
                    Date = hs.Date,
                    Total = hs.Total,
                    IsSynced = hs.IsSynced,
                    IsPaid = hs.IsPaid,
                    NoteId = hs.NoteId,
                    Note = hs.Note,
                    IsEdited = hs.IsEdited,
                    EditedAt = hs.EditedAt,
                    Details = hs.Details.Select(hsd => new SaleDetail
                    {
                        Id = hsd.Id,
                        SaleId = hsd.HistoricalSaleId,
                        ProductId = hsd.ProductId,
                        Product = hsd.Product,
                        Quantity = hsd.Quantity,
                        UnitPrice = hsd.UnitPrice
                    }).ToList()
                })
                .ToList();

            combinedList.AddRange(historicalSales);
        }

        return combinedList.OrderByDescending(s => s.Date).Take(limit).ToList();
    }

    [ObservableProperty]
    private bool _isPrintModalOpen;

    [ObservableProperty]
    private bool _isEditModalOpen;

    [ObservableProperty]
    private bool _isDeleteConfirmationOpen;

    [ObservableProperty]
    private Sale? _selectedSaleForEdit;

    [ObservableProperty]
    private Sale? _saleToDelete;

    [ObservableProperty]
    private decimal _editingTotal;

    public ObservableCollection<EditSaleDetailViewModel> EditingDetails { get; } = new();

    private Sale? _pendingSale;

    private void RecalculateEditingTotal()
    {
        EditingTotal = EditingDetails.Sum(d => d.Subtotal);
    }

    [RelayCommand]
    private void EditSale(Sale? sale)
    {
        if (sale == null) return;
        SelectedSaleForEdit = sale;
        EditingDetails.Clear();

        // Determinar si es una venta activa o histórica
        bool isActive = _dbContext.Sales.Any(s => s.Id == sale.Id);

        if (isActive)
        {
            var saleWithDetails = _dbContext.Sales
                .Include(s => s.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefault(s => s.Id == sale.Id);

            if (saleWithDetails == null) return;

            foreach (var detail in saleWithDetails.Details)
            {
                var editDetail = new EditSaleDetailViewModel(RecalculateEditingTotal)
                {
                    Id = detail.Id,
                    ProductId = detail.ProductId,
                    ProductName = detail.Product?.Name ?? "Producto Desconocido",
                    IsBulk = detail.Product?.IsBulk ?? false,
                    UnitPrice = detail.UnitPrice,
                    Quantity = detail.Quantity
                };
                EditingDetails.Add(editDetail);
            }
        }
        else
        {
            var histSaleWithDetails = _dbContext.HistoricalSales
                .Include(s => s.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefault(s => s.Id == sale.Id);

            if (histSaleWithDetails == null) return;

            foreach (var detail in histSaleWithDetails.Details)
            {
                var editDetail = new EditSaleDetailViewModel(RecalculateEditingTotal)
                {
                    Id = detail.Id,
                    ProductId = detail.ProductId,
                    ProductName = detail.Product?.Name ?? "Producto Desconocido",
                    IsBulk = detail.Product?.IsBulk ?? false,
                    UnitPrice = detail.UnitPrice,
                    Quantity = detail.Quantity
                };
                EditingDetails.Add(editDetail);
            }
        }

        RecalculateEditingTotal();
        IsEditModalOpen = true;
    }

    [RelayCommand]
    private void DecreaseEditQuantity(EditSaleDetailViewModel? detail)
    {
        if (detail == null) return;
        if (detail.IsBulk)
        {
            detail.Quantity = Math.Max(0.1m, detail.Quantity - 0.1m);
        }
        else
        {
            detail.Quantity = Math.Max(1m, detail.Quantity - 1m);
        }
    }

    [RelayCommand]
    private void IncreaseEditQuantity(EditSaleDetailViewModel? detail)
    {
        if (detail == null) return;
        if (detail.IsBulk)
        {
            detail.Quantity += 0.1m;
        }
        else
        {
            detail.Quantity += 1m;
        }
    }

    [RelayCommand]
    private void RemoveEditItem(EditSaleDetailViewModel? detail)
    {
        if (detail == null) return;
        EditingDetails.Remove(detail);
        RecalculateEditingTotal();
    }

    [RelayCommand]
    private void SaveEdit()
    {
        if (SelectedSaleForEdit == null) return;

        // Si la lista queda vacía, es equivalente a eliminar el registro
        if (!EditingDetails.Any())
        {
            IsEditModalOpen = false;
            SaleToDelete = SelectedSaleForEdit;
            IsDeleteConfirmationOpen = true;
            return;
        }

        bool isActive = _dbContext.Sales.Any(s => s.Id == SelectedSaleForEdit.Id);

        try
        {
            int? noteIdToRecalculate = null;
            if (isActive)
            {
                var saleInDb = _dbContext.Sales
                    .Include(s => s.Details)
                    .FirstOrDefault(s => s.Id == SelectedSaleForEdit.Id);

                if (saleInDb != null)
                {
                    noteIdToRecalculate = saleInDb.NoteId;
                    var editingIds = EditingDetails.Select(ed => ed.Id).ToList();
                    var detailsToRemove = saleInDb.Details.Where(d => !editingIds.Contains(d.Id)).ToList();
                    foreach (var d in detailsToRemove)
                    {
                        _dbContext.SaleDetails.Remove(d);
                        saleInDb.Details.Remove(d);
                    }

                    foreach (var editingDetail in EditingDetails)
                    {
                        var dbDetail = saleInDb.Details.FirstOrDefault(d => d.Id == editingDetail.Id);
                        if (dbDetail != null)
                        {
                            dbDetail.Quantity = editingDetail.Quantity;
                        }
                    }

                    saleInDb.IsEdited = true;
                    saleInDb.EditedAt = DateTime.Now;
                    saleInDb.Total = saleInDb.Details.Sum(d => d.Quantity * d.UnitPrice);
                }
            }
            else
            {
                var histSaleInDb = _dbContext.HistoricalSales
                    .Include(s => s.Details)
                    .FirstOrDefault(s => s.Id == SelectedSaleForEdit.Id);

                if (histSaleInDb != null)
                {
                    var editingIds = EditingDetails.Select(ed => ed.Id).ToList();
                    var detailsToRemove = histSaleInDb.Details.Where(d => !editingIds.Contains(d.Id)).ToList();
                    foreach (var d in detailsToRemove)
                    {
                        _dbContext.HistoricalSaleDetails.Remove(d);
                        histSaleInDb.Details.Remove(d);
                    }

                    foreach (var editingDetail in EditingDetails)
                    {
                        var dbDetail = histSaleInDb.Details.FirstOrDefault(d => d.Id == editingDetail.Id);
                        if (dbDetail != null)
                        {
                            dbDetail.Quantity = editingDetail.Quantity;
                        }
                    }

                    histSaleInDb.IsEdited = true;
                    histSaleInDb.EditedAt = DateTime.Now;
                    histSaleInDb.Total = histSaleInDb.Details.Sum(d => d.Quantity * d.UnitPrice);
                }
            }

            _dbContext.SaveChanges();
            if (noteIdToRecalculate != null)
            {
                RecalculateNoteAmount(noteIdToRecalculate);
            }
            LoadSales();
            IsEditModalOpen = false;
            SelectedSaleForEdit = null;
        }
        catch (Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al guardar cambios: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditModalOpen = false;
        SelectedSaleForEdit = null;
        EditingDetails.Clear();
    }

    [RelayCommand]
    private void DeleteSale()
    {
        if (SelectedSaleForEdit == null) return;
        SaleToDelete = SelectedSaleForEdit;
        IsEditModalOpen = false;
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void DeleteSaleFromList(Sale? sale)
    {
        if (sale == null) return;
        SaleToDelete = sale;
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void ConfirmDeleteSale()
    {
        if (SaleToDelete == null) return;

        bool isActive = _dbContext.Sales.Any(s => s.Id == SaleToDelete.Id);
        int? noteIdToRecalculate = null;

        try
        {
            if (isActive)
            {
                var saleInDb = _dbContext.Sales.FirstOrDefault(s => s.Id == SaleToDelete.Id);
                if (saleInDb != null)
                {
                    noteIdToRecalculate = saleInDb.NoteId;
                    _dbContext.Sales.Remove(saleInDb);
                }
            }
            else
            {
                var histSaleInDb = _dbContext.HistoricalSales.FirstOrDefault(hs => hs.Id == SaleToDelete.Id);
                if (histSaleInDb != null)
                {
                    _dbContext.HistoricalSales.Remove(histSaleInDb);
                }
            }

            _dbContext.SaveChanges();
            if (noteIdToRecalculate != null)
            {
                RecalculateNoteAmount(noteIdToRecalculate);
            }
            LoadSales();
            IsDeleteConfirmationOpen = false;
            SaleToDelete = null;
        }
        catch (Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al eliminar la venta: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelDeleteSale()
    {
        IsDeleteConfirmationOpen = false;
        SaleToDelete = null;
    }

    [RelayCommand]
    private void ReimprimirTicket(Sale? sale)
    {
        if (sale == null) return;
        _pendingSale = sale;
        IsPrintModalOpen = true;
    }

    [RelayCommand]
    private void ConfirmPrintTicket()
    {
        IsPrintModalOpen = false;
        if (_pendingSale != null)
        {
            var profile = _dbContext.StoreProfiles.FirstOrDefault() ?? new StoreProfile();
            var ticketGenerator = App.Current.Services.GetService<Cajolote.Services.TicketGenerator>();
            if (ticketGenerator != null)
            {
                try
                {
                    ticketGenerator.PrintTicket(_pendingSale, profile, doPrint: true, showPrintDialog: _settingsService.Settings.AlwaysAskBeforePrinting);
                }
                catch (System.Exception ex)
                {
                    ErrorMessageQueue.Enqueue($"Error al reimprimir ticket: {ex.Message}");
                }
            }
            _pendingSale = null;
        }
    }

    [RelayCommand]
    private void SkipPrintTicket()
    {
        IsPrintModalOpen = false;
        _pendingSale = null;
    }

    [ObservableProperty]
    private bool _isExportModalOpen;

    [ObservableProperty]
    private bool _isExportWarningModalOpen;

    [ObservableProperty]
    private bool _isLoadingExport;

    [ObservableProperty]
    private int _selectedExportQuantityIndex = 0;

    public ObservableCollection<string> ExportQuantityOptions { get; } = new() { "10", "25", "50", "100", "Todos" };

    [RelayCommand]
    private void Exportar()
    {
        if (!Sales.Any())
        {
            ErrorMessageQueue.Enqueue("No hay registros para exportar.");
            return;
        }
        SelectedExportQuantityIndex = 0;
        IsExportModalOpen = true;
    }

    [RelayCommand]
    private void CancelExport()
    {
        IsExportModalOpen = false;
    }

    [RelayCommand]
    private void ProceedExport()
    {
        IsExportModalOpen = false;
        if (SelectedExportQuantityIndex == 4) // "Todos"
        {
            IsExportWarningModalOpen = true;
        }
        else
        {
            int[] limits = { 10, 25, 50, 100 };
            int limit = limits[SelectedExportQuantityIndex];
            StartExportAsync(limit);
        }
    }

    [RelayCommand]
    private void CancelExportWarning()
    {
        IsExportWarningModalOpen = false;
    }

    [RelayCommand]
    private void ConfirmExportAll()
    {
        IsExportWarningModalOpen = false;
        StartExportAsync(int.MaxValue); // To get all
    }

    private async void StartExportAsync(int limit)
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = $"Reporte_Ventas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                Title = "Guardar Reporte de Ventas"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var profile = _dbContext.StoreProfiles.FirstOrDefault() ?? new StoreProfile();
                
                string filterDesc = string.Empty;
                switch (SelectedFilterIndex)
                {
                    case 0: filterDesc = "Filtro: Hoy"; break;
                    case 1: filterDesc = "Filtro: Esta semana"; break;
                    case 2: filterDesc = "Filtro: Este mes"; break;
                    case 3: filterDesc = "Filtro: Mes anterior"; break;
                    case 4: filterDesc = "Filtro: Este año"; break;
                    case 5: filterDesc = "Filtro: Todos"; break;
                }
                
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    string searchType = SelectedSearchTypeIndex switch
                    {
                        0 => "Folio",
                        1 => "Monto",
                        2 => "Producto",
                        _ => ""
                    };
                    filterDesc += $" | Búsqueda por {searchType}: '{SearchText}'";
                }

                IsLoadingExport = true;

                // Capturar todo lo necesario para correr en Task.Run
                var generator = App.Current.Services.GetService<Cajolote.Services.SalesReportGenerator>();
                string filePath = saveFileDialog.FileName;

                await System.Threading.Tasks.Task.Run(async () =>
                {
                    await _dbSemaphore.WaitAsync();
                    try
                    {
                        var exportSales = GetSalesList(limit);
                        if (generator != null)
                        {
                            var pdfBytes = generator.GenerateSalesReportPdf(exportSales, profile, filterDesc);
                            System.IO.File.WriteAllBytes(filePath, pdfBytes);
                        }
                        else
                        {
                            throw new System.Exception("El servicio de generación no está disponible.");
                        }
                    }
                    finally
                    {
                        _dbSemaphore.Release();
                    }
                });

                ErrorMessageQueue.Enqueue("Reporte exportado exitosamente.");
            }
        }
        catch (System.Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al exportar reporte: {ex.Message}");
        }
        finally
        {
            IsLoadingExport = false;
        }
    }

    [ObservableProperty]
    private bool _isCreditModalOpen;

    [ObservableProperty]
    private Note? _selectedNote;

    [ObservableProperty]
    private bool _createNewCreditAccount;

    [ObservableProperty]
    private string _newCreditCustomerName = string.Empty;

    [ObservableProperty]
    private Sale? _saleToChangeToCredit;

    public ObservableCollection<Note> AvailableNotes { get; } = new();

    [RelayCommand]
    private void FiarVenta(Sale? sale)
    {
        if (sale == null) return;
        
        SaleToChangeToCredit = sale;
        
        AvailableNotes.Clear();
        foreach (var n in _dbContext.Notes.Where(n => !n.IsPaid).ToList())
        {
            AvailableNotes.Add(n);
        }
        
        SelectedNote = null;
        CreateNewCreditAccount = !AvailableNotes.Any();
        NewCreditCustomerName = string.Empty;
        IsCreditModalOpen = true;
    }

    [RelayCommand]
    private void ConfirmCredit()
    {
        if (SaleToChangeToCredit == null) return;

        Note? note = null;

        if (CreateNewCreditAccount)
        {
            if (string.IsNullOrWhiteSpace(NewCreditCustomerName))
            {
                ErrorMessageQueue.Enqueue("Por favor ingrese el nombre del nuevo cliente.");
                return;
            }

            var customerNameTrimmed = NewCreditCustomerName.Trim();
            if (!string.IsNullOrEmpty(customerNameTrimmed) && char.IsLetter(customerNameTrimmed[0]))
            {
                customerNameTrimmed = char.ToUpper(customerNameTrimmed[0]) + customerNameTrimmed.Substring(1);
            }
            
            var existingDuplicate = _dbContext.Notes.FirstOrDefault(n => n.CustomerName.ToLower() == customerNameTrimmed.ToLower() && !n.IsPaid);
            if (existingDuplicate != null)
            {
                System.Windows.MessageBox.Show("Ya existe una cuenta abierta (no liquidada) con ese mismo nombre. Por favor, asigne otro nombre o elija la cuenta existente.", "Cuenta duplicada", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            note = new Note
            {
                CustomerName = customerNameTrimmed,
                Amount = SaleToChangeToCredit.Total,
                CreatedAt = System.DateTime.Now
            };
            _dbContext.Notes.Add(note);
            _dbContext.SaveChanges();
        }
        else
        {
            if (SelectedNote == null)
            {
                ErrorMessageQueue.Enqueue("Seleccione una cuenta para fiar o cree una nueva.");
                return;
            }

            note = _dbContext.Notes.Find(SelectedNote.Id);
            if (note != null)
            {
                note.Amount += SaleToChangeToCredit.Total;
                _dbContext.Notes.Update(note);
            }
        }

        if (note == null)
        {
            ErrorMessageQueue.Enqueue("Error: No se pudo determinar la cuenta de destino.");
            return;
        }

        var saleInDb = _dbContext.Sales.Find(SaleToChangeToCredit.Id);
        if (saleInDb != null)
        {
            saleInDb.IsPaid = false;
            saleInDb.NoteId = note.Id;
            _dbContext.SaveChanges();
        }

        IsCreditModalOpen = false;
        SaleToChangeToCredit = null;
        LoadSales();
    }

    [RelayCommand]
    private void CancelCredit()
    {
        IsCreditModalOpen = false;
        SaleToChangeToCredit = null;
    }

    private void RecalculateNoteAmount(int? noteId)
    {
        if (noteId == null) return;
        var note = _dbContext.Notes
            .Include(n => n.Sales)
            .Include(n => n.ManualDebts)
            .FirstOrDefault(n => n.Id == noteId.Value);

        if (note != null)
        {
            decimal totalManual = note.ManualDebts.Sum(md => md.Amount);
            decimal totalSales = note.Sales.Sum(s => s.Total);
            note.Amount = totalManual + totalSales;

            if (note.Amount <= 0)
            {
                var relatedHistoricalSales = _dbContext.HistoricalSales.Where(hs => hs.NoteId == note.Id).ToList();
                foreach (var hSale in relatedHistoricalSales)
                {
                    hSale.NoteId = null;
                    _dbContext.HistoricalSales.Update(hSale);
                }
                _dbContext.Notes.Remove(note);
            }
            else
            {
                _dbContext.Notes.Update(note);
            }
            _dbContext.SaveChanges();
        }
    }
}
