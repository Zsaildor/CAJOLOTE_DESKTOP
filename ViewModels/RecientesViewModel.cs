using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;
using Cajolote.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.Services;


namespace Cajolote.ViewModels;

public partial class RecientesViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;
    private readonly SettingsService _settingsService;
    public ISnackbarMessageQueue ErrorMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

    public RecientesViewModel(CajoloteDbContext dbContext, SettingsService settingsService)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        LoadRecentSales();
    }

    public ObservableCollection<Sale> RecentSales { get; } = new();

    private void LoadRecentSales()
    {
        var today = DateTime.Today;
        var sales = _dbContext.Sales
            .Include(s => s.Details)
                .ThenInclude(d => d.Product)
            .Include(s => s.Note)
            .Where(s => s.Date >= today)
            .OrderByDescending(s => s.Date)
            .ToList();
            
        RecentSales.Clear();
        foreach (var s in sales) RecentSales.Add(s);
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
        
        // Cargar detalles frescos de la base de datos
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

        var saleInDb = _dbContext.Sales
            .Include(s => s.Details)
            .FirstOrDefault(s => s.Id == SelectedSaleForEdit.Id);

        if (saleInDb == null) return;

        // 1. Eliminar detalles que ya no existen
        var editingIds = EditingDetails.Select(ed => ed.Id).ToList();
        var detailsToRemove = saleInDb.Details.Where(d => !editingIds.Contains(d.Id)).ToList();
        foreach (var d in detailsToRemove)
        {
            _dbContext.SaleDetails.Remove(d);
            saleInDb.Details.Remove(d);
        }

        // 2. Actualizar cantidades de detalles existentes
        foreach (var editingDetail in EditingDetails)
        {
            var dbDetail = saleInDb.Details.FirstOrDefault(d => d.Id == editingDetail.Id);
            if (dbDetail != null)
            {
                dbDetail.Quantity = editingDetail.Quantity;
            }
        }

        // 3. Actualizar auditoría y total
        saleInDb.IsEdited = true;
        saleInDb.EditedAt = DateTime.Now;
        saleInDb.Total = saleInDb.Details.Sum(d => d.Quantity * d.UnitPrice);

        int? noteId = saleInDb.NoteId;

        try
        {
            _dbContext.SaveChanges();
            RecalculateNoteAmount(noteId);
            LoadRecentSales();
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

        var saleInDb = _dbContext.Sales
            .Include(s => s.Details)
            .FirstOrDefault(s => s.Id == SaleToDelete.Id);

        if (saleInDb != null)
        {
            int? noteId = saleInDb.NoteId;
            try
            {
                _dbContext.Sales.Remove(saleInDb);
                _dbContext.SaveChanges();
                RecalculateNoteAmount(noteId);
                
                LoadRecentSales();
                IsDeleteConfirmationOpen = false;
                SaleToDelete = null;
            }
            catch (Exception ex)
            {
                ErrorMessageQueue.Enqueue($"Error al eliminar la venta: {ex.Message}");
            }
        }
        else
        {
            IsDeleteConfirmationOpen = false;
            SaleToDelete = null;
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
            _dbContext.SaveChanges(); // EF generates the note.Id here
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
        LoadRecentSales();
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

public class EditSaleDetailViewModel : ObservableObject
{
    private decimal _quantity;
    private string _quantityString = string.Empty;
    private readonly Action _onQuantityChanged;

    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public bool IsBulk { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (value < 0) value = 0;
            if (!IsBulk)
            {
                value = Math.Round(value, 0);
            }
            if (SetProperty(ref _quantity, value))
            {
                _quantityString = _quantity.ToString(System.Globalization.CultureInfo.CurrentCulture);
                OnPropertyChanged(nameof(QuantityString));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(QuantityDisplay));
                _onQuantityChanged?.Invoke();
            }
        }
    }

    public string QuantityString
    {
        get => _quantityString;
        set
        {
            if (SetProperty(ref _quantityString, value))
            {
                var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                string cleaned = (value ?? string.Empty).Replace(",", decSep).Replace(".", decSep);
                if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal parsedVal))
                {
                    if (parsedVal < 0) parsedVal = 0;
                    if (!IsBulk)
                    {
                        parsedVal = Math.Round(parsedVal, 0);
                    }
                    if (_quantity != parsedVal)
                    {
                        _quantity = parsedVal;
                        OnPropertyChanged(nameof(Quantity));
                        OnPropertyChanged(nameof(Subtotal));
                        OnPropertyChanged(nameof(QuantityDisplay));
                        _onQuantityChanged?.Invoke();
                    }
                }
            }
        }
    }

    public decimal Subtotal => Quantity * UnitPrice;
    public string QuantityDisplay => IsBulk ? $"{Quantity:0.000} kg" : $"{Quantity:0}";

    public EditSaleDetailViewModel(Action onQuantityChanged)
    {
        _onQuantityChanged = onQuantityChanged;
    }
}
