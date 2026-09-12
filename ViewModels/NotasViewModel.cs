using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;
using Cajolote.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Cajolote.Services;
using System.Collections.Generic;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cajolote.ViewModels;

public partial class NotasViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;
    private readonly SettingsService _settingsService;

    public NotasViewModel(CajoloteDbContext dbContext, SettingsService settingsService)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        LoadNotes();
    }

    public ObservableCollection<Note> Notes { get; } = new();
    public ObservableCollection<Note> PaidNotes { get; } = new();

    [ObservableProperty] private bool _isNewCustomer = true;
    [ObservableProperty] private Note? _selectedExistingCustomer;
    [ObservableProperty] private string _newCustomerName = string.Empty;
    [ObservableProperty] private decimal? _newAmount;
    [ObservableProperty] private string _newDetails = string.Empty;

    [ObservableProperty] private string _abonoAmount = string.Empty;
    [ObservableProperty] private bool _isConfirmSettleOpen;
    [ObservableProperty] private Note? _noteToSettle;

    [ObservableProperty] private bool _isPrintReceiptModalOpen;
    [ObservableProperty] private Note? _noteToPrint;

    [ObservableProperty] private bool _isConfirmPermanentDeleteOpen;
    [ObservableProperty] private Note? _noteToPermanentDelete;

    [ObservableProperty] private decimal _totalFiado;
    [ObservableProperty] private int _deudoresActivos;
    [ObservableProperty] private decimal _deudaPromedio;

    private int _paidNotesOffset = 0;
    [ObservableProperty] private bool _isLoadingMorePaidNotes;
    [ObservableProperty] private bool _hasMorePaidNotes;

    public ISnackbarMessageQueue ErrorMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

    [ObservableProperty] private bool _isEditManualDebtOpen;
    [ObservableProperty] private ManualDebt? _selectedManualDebtForEdit;
    [ObservableProperty] private decimal _editingManualDebtAmount;
    [ObservableProperty] private string _editingManualDebtDetails = string.Empty;

    [ObservableProperty] private bool _isEditSaleOpen;
    [ObservableProperty] private Sale? _selectedSaleForEdit;
    [ObservableProperty] private decimal _editingTotal;
    public ObservableCollection<EditSaleDetailViewModel> EditingDetails { get; } = new();

    [ObservableProperty] private bool _isDeleteSaleConfirmationOpen;
    [ObservableProperty] private Sale? _saleToDelete;

    [ObservableProperty] private bool _isConfirmFullDeleteOpen;
    [ObservableProperty] private Note? _noteForFullDelete;

    [ObservableProperty] private bool _isAddDebtModalOpen;
    [ObservableProperty] private Note? _selectedNoteForAddDebt;
    [ObservableProperty] private decimal _addDebtAmount;
    [ObservableProperty] private string _addDebtDetails = string.Empty;

    [ObservableProperty] private ISeries[] _topDebtorsSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _topDebtorsXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _topDebtorsYAxes = Array.Empty<Axis>();
    [ObservableProperty] private bool _isConfirmDeleteAllPaidOpen;
    [ObservableProperty] private bool _isDeletingAllPaidNotes;

    private void LoadNotes()
    {
        var activeNotes = _dbContext.Notes
            .Include(n => n.Sales)
                .ThenInclude(s => s.Details)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Category)
            .Include(n => n.ManualDebts)
            .Where(n => !n.IsPaid)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
            
        Notes.Clear();
        foreach (var n in activeNotes)
        {
            Notes.Add(n);
        }

        _paidNotesOffset = 0;
        PaidNotes.Clear();

        var paidNotesQuery = _dbContext.Notes
            .Include(n => n.Sales)
                .ThenInclude(s => s.Details)
                .ThenInclude(d => d.Product)
            .Include(n => n.ManualDebts)
            .Include(n => n.HistoricalSales)
                .ThenInclude(hs => hs.Details)
                .ThenInclude(d => d.Product)
            .Where(n => n.IsPaid)
            .OrderByDescending(n => n.PaidAt);

        var initialPaidNotes = paidNotesQuery.Take(10).ToList();
        
        foreach (var n in initialPaidNotes)
        {
            PaidNotes.Add(n);
        }
        
        HasMorePaidNotes = initialPaidNotes.Count == 10;
        _paidNotesOffset += initialPaidNotes.Count;

        TotalFiado = Notes.Sum(n => n.Faltante);
        DeudoresActivos = Notes.Count;
        DeudaPromedio = DeudoresActivos > 0 ? TotalFiado / DeudoresActivos : 0;

        // Sales that are linked to active notes (i.e. IsPaid == false)

        // 2. TopDebtorsSeries: Bar chart showing the top 5 debtors by Faltante
        var topDebtors = activeNotes
            .OrderByDescending(n => n.Faltante)
            .Take(5)
            .ToList();
            
        var debtorNames = topDebtors.Select(d => d.CustomerName).ToArray();
        var debtorAmounts = topDebtors.Select(d => (double)d.Faltante).ToArray();
        
        TopDebtorsSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = debtorAmounts,
                Name = "Saldo Pendiente",
                Fill = new SolidColorPaint(SKColor.Parse("#E76F51")),
                MaxBarWidth = 30,
                Rx = 10,
                Ry = 10
            }
        };
        
        TopDebtorsXAxes = new Axis[]
        {
            new Axis
            {
                Labels = debtorNames,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 11
            }
        };
        
        TopDebtorsYAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                Labeler = value => value.ToString("C0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 11
            }
        };
    }

    [RelayCommand]
    private void AddNote()
    {
        if (NewAmount == null || NewAmount <= 0) return;

        if (IsNewCustomer)
        {
            if (string.IsNullOrWhiteSpace(NewCustomerName)) return;

            var note = new Note 
            { 
                CustomerName = CapitalizeFirstLetter(NewCustomerName.Trim()), 
                Amount = NewAmount.Value,
                CreatedAt = DateTime.Now 
            };
            note.ManualDebts.Add(new ManualDebt 
            {
                Amount = NewAmount.Value,
                Details = CapitalizeFirstLetter((NewDetails ?? string.Empty).Trim()),
                Date = DateTime.Now
            });
            _dbContext.Notes.Add(note);
        }
        else
        {
            if (SelectedExistingCustomer == null) return;

            var note = _dbContext.Notes.Find(SelectedExistingCustomer.Id);
            if (note != null)
            {
                note.Amount += NewAmount.Value;
                var manualDebt = new ManualDebt
                {
                    Amount = NewAmount.Value,
                    Details = CapitalizeFirstLetter((NewDetails ?? string.Empty).Trim()),
                    Date = DateTime.Now,
                    NoteId = note.Id
                };
                _dbContext.ManualDebts.Add(manualDebt);
                _dbContext.Notes.Update(note);
            }
        }

        _dbContext.SaveChanges();
        
        NewCustomerName = string.Empty;
        NewAmount = null;
        NewDetails = string.Empty;
        LoadNotes();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadMorePaidNotesAsync()
    {
        if (IsLoadingMorePaidNotes || !HasMorePaidNotes) return;
        
        IsLoadingMorePaidNotes = true;
        try
        {
            await System.Threading.Tasks.Task.Delay(300); // UI feel
            
            var morePaidNotes = await _dbContext.Notes
                .Include(n => n.Sales)
                    .ThenInclude(s => s.Details)
                    .ThenInclude(d => d.Product)
                .Include(n => n.ManualDebts)
                .Include(n => n.HistoricalSales)
                    .ThenInclude(hs => hs.Details)
                    .ThenInclude(d => d.Product)
                .Where(n => n.IsPaid)
                .OrderByDescending(n => n.PaidAt)
                .Skip(_paidNotesOffset)
                .Take(10)
                .ToListAsync();
                
            foreach (var n in morePaidNotes)
            {
                PaidNotes.Add(n);
            }
            
            _paidNotesOffset += morePaidNotes.Count;
            HasMorePaidNotes = morePaidNotes.Count == 10;
        }
        catch (Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al cargar más historial: {ex.Message}");
        }
        finally
        {
            IsLoadingMorePaidNotes = false;
        }
    }

    [RelayCommand]
    private void AddAbono(Note? note)
    {
        if (note == null) return;

        var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        string cleaned = (AbonoAmount ?? string.Empty).Replace(",", decSep).Replace(".", decSep);
        if (!decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal abono) || abono <= 0)
        {
            ErrorMessageQueue.Enqueue("Por favor ingrese un monto de abono válido mayor a 0.");
            return;
        }

        if (abono > note.Faltante)
        {
            ErrorMessageQueue.Enqueue($"El abono no puede superar el saldo faltante de {note.Faltante:C}.");
            return;
        }

        if (abono == note.Faltante)
        {
            NoteToSettle = note;
            IsConfirmSettleOpen = true;
            return;
        }

        var dbNote = _dbContext.Notes.Find(note.Id);
        if (dbNote != null)
        {
            dbNote.PaidAmount += abono;
            _dbContext.Notes.Update(dbNote);
            _dbContext.SaveChanges();
            AbonoAmount = string.Empty;
            LoadNotes();
            ErrorMessageQueue.Enqueue("Abono aplicado correctamente.");
        }
    }

    [RelayCommand]
    private void RequestSettleNote(Note? note)
    {
        if (note != null)
        {
            NoteToSettle = note;
            IsConfirmSettleOpen = true;
        }
    }

    [RelayCommand]
    private void ConfirmSettleNote()
    {
        if (NoteToSettle != null)
        {
            var note = NoteToSettle;
            var now = DateTime.Now;

            // Marcar nota como pagada y archivar
            var dbNote = _dbContext.Notes.Find(note.Id);
            if (dbNote != null)
            {
                dbNote.PaidAmount = dbNote.Amount;
                dbNote.IsPaid = true;
                dbNote.PaidAt = now;
                _dbContext.Notes.Update(dbNote);
            }

            // Ventas fiadas que se están pagando ahora
            var relatedSales = _dbContext.Sales.Where(s => s.NoteId == note.Id).ToList();
            foreach (var sale in relatedSales)
            {
                sale.IsPaid = true;
                sale.Date = now; // Actualizamos la fecha para que cuente como ganancia de hoy
                _dbContext.Sales.Update(sale);
            }

            // Deudas manuales que se están cobrando y convertimos en "Ventas" para la estadística
            var manualDebts = _dbContext.ManualDebts.Where(d => d.NoteId == note.Id).ToList();
            foreach (var md in manualDebts)
            {
                var sale = new Sale
                {
                    Date = now,
                    Total = md.Amount,
                    IsPaid = true
                };
                _dbContext.Sales.Add(sale);
            }

            _dbContext.SaveChanges();

            // Cargar de nuevo con detalles para la impresión del comprobante
            var noteWithDetails = _dbContext.Notes
                .Include(n => n.Sales)
                .Include(n => n.ManualDebts)
                .FirstOrDefault(n => n.Id == note.Id);

            NoteToPrint = noteWithDetails ?? note;

            AbonoAmount = string.Empty;
            NoteToSettle = null;
            IsConfirmSettleOpen = false;
            LoadNotes();

            // Abrir modal para preguntar si desea imprimir ticket
            IsPrintReceiptModalOpen = true;

            ErrorMessageQueue.Enqueue("Cuenta cobrada y liquidada con éxito.");
        }
    }

    [RelayCommand]
    private void CancelSettleNote()
    {
        AbonoAmount = string.Empty;
        NoteToSettle = null;
        IsConfirmSettleOpen = false;
    }

    [RelayCommand]
    private void ConfirmPrintReceipt()
    {
        IsPrintReceiptModalOpen = false;
        if (NoteToPrint != null)
        {
            var note = NoteToPrint;
            NoteToPrint = null;
            PrintReceiptAndClear(note, true);
        }
    }

    [RelayCommand]
    private void SkipPrintReceipt()
    {
        IsPrintReceiptModalOpen = false;
        NoteToPrint = null;
    }

    [RelayCommand]
    private void ReprintReceipt(Note? note)
    {
        if (note != null)
        {
            var noteWithDetails = _dbContext.Notes
                .Include(n => n.Sales)
                    .ThenInclude(s => s.Details)
                    .ThenInclude(d => d.Product)
                .Include(n => n.ManualDebts)
                .Include(n => n.HistoricalSales)
                    .ThenInclude(hs => hs.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefault(n => n.Id == note.Id);

            NoteToPrint = noteWithDetails ?? note;
            IsPrintReceiptModalOpen = true;
        }
    }

    private void PrintReceiptAndClear(Note note, bool doPrint)
    {
        var profile = _dbContext.StoreProfiles.FirstOrDefault() ?? new StoreProfile();
        var generator = App.Current.Services.GetService<Cajolote.Services.NoteReceiptGenerator>();
        if (generator != null)
        {
            try
            {
                generator.PrintReceipt(note, profile, doPrint, showPrintDialog: _settingsService.Settings.AlwaysAskBeforePrinting);
            }
            catch (Exception ex)
            {
                ErrorMessageQueue.Enqueue($"Error al imprimir: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void RequestPermanentDelete(Note? note)
    {
        if (note != null)
        {
            NoteToPermanentDelete = note;
            IsConfirmPermanentDeleteOpen = true;
        }
    }

    [RelayCommand]
    private void ConfirmPermanentDelete()
    {
        if (NoteToPermanentDelete != null)
        {
            var note = NoteToPermanentDelete;
            
            var relatedSales = _dbContext.Sales.Where(s => s.NoteId == note.Id).ToList();
            foreach (var sale in relatedSales)
            {
                sale.NoteId = null;
                _dbContext.Sales.Update(sale);
            }

            // Romper vínculos con ventas históricas
            var relatedHistoricalSales = _dbContext.HistoricalSales.Where(s => s.NoteId == note.Id).ToList();
            foreach (var hSale in relatedHistoricalSales)
            {
                hSale.NoteId = null;
                _dbContext.HistoricalSales.Update(hSale);
            }

            // Eliminar deudas manuales vinculadas
            var manualDebts = _dbContext.ManualDebts.Where(md => md.NoteId == note.Id).ToList();
            foreach (var md in manualDebts)
            {
                _dbContext.ManualDebts.Remove(md);
            }

            var dbNote = _dbContext.Notes.Find(note.Id);
            if (dbNote != null)
            {
                _dbContext.Notes.Remove(dbNote);
            }

            _dbContext.SaveChanges();

            NoteToPermanentDelete = null;
            IsConfirmPermanentDeleteOpen = false;
            LoadNotes();
            ErrorMessageQueue.Enqueue("Cuenta eliminada permanentemente.");
        }
    }

    [RelayCommand]
    private void CancelPermanentDelete()
    {
        NoteToPermanentDelete = null;
        IsConfirmPermanentDeleteOpen = false;
    }

    [RelayCommand]
    private void DeleteManualDebt(ManualDebt? debt)
    {
        if (debt == null) return;
        var dbDebt = _dbContext.ManualDebts.Find(debt.Id);
        if (dbDebt != null)
        {
            var noteId = dbDebt.NoteId;
            _dbContext.ManualDebts.Remove(dbDebt);
            _dbContext.SaveChanges();
            RecalculateNoteAmount(noteId);
            LoadNotes();
            ErrorMessageQueue.Enqueue("Deuda manual eliminada.");
        }
    }

    [RelayCommand]
    private void EditManualDebt(ManualDebt? debt)
    {
        if (debt == null) return;
        SelectedManualDebtForEdit = debt;
        EditingManualDebtAmount = debt.Amount;
        EditingManualDebtDetails = debt.Details;
        IsEditManualDebtOpen = true;
    }

    [RelayCommand]
    private void SaveEditManualDebt()
    {
        if (SelectedManualDebtForEdit == null) return;
        if (EditingManualDebtAmount <= 0)
        {
            ErrorMessageQueue.Enqueue("Por favor ingrese un monto válido mayor a 0.");
            return;
        }

        var dbDebt = _dbContext.ManualDebts.Find(SelectedManualDebtForEdit.Id);
        if (dbDebt != null)
        {
            var noteId = dbDebt.NoteId;
            dbDebt.Amount = EditingManualDebtAmount;
            dbDebt.Details = CapitalizeFirstLetter((EditingManualDebtDetails ?? string.Empty).Trim());
            _dbContext.ManualDebts.Update(dbDebt);
            _dbContext.SaveChanges();
            RecalculateNoteAmount(noteId);
            LoadNotes();
            
            IsEditManualDebtOpen = false;
            SelectedManualDebtForEdit = null;
            ErrorMessageQueue.Enqueue("Deuda manual actualizada.");
        }
    }

    [RelayCommand]
    private void CancelEditManualDebt()
    {
        IsEditManualDebtOpen = false;
        SelectedManualDebtForEdit = null;
    }

    private void RecalculateEditingTotal()
    {
        EditingTotal = EditingDetails.Sum(d => d.Subtotal);
    }

    [RelayCommand]
    private void EditLinkedSale(Sale? sale)
    {
        if (sale == null) return;
        SelectedSaleForEdit = sale;
        EditingDetails.Clear();

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
        IsEditSaleOpen = true;
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
    private void SaveEditSale()
    {
        if (SelectedSaleForEdit == null) return;

        if (!EditingDetails.Any())
        {
            IsEditSaleOpen = false;
            SaleToDelete = SelectedSaleForEdit;
            IsDeleteSaleConfirmationOpen = true;
            return;
        }

        var saleInDb = _dbContext.Sales
            .Include(s => s.Details)
            .FirstOrDefault(s => s.Id == SelectedSaleForEdit.Id);

        if (saleInDb == null) return;

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

        try
        {
            _dbContext.SaveChanges();
            RecalculateNoteAmount(saleInDb.NoteId);
            LoadNotes();
            IsEditSaleOpen = false;
            SelectedSaleForEdit = null;
            ErrorMessageQueue.Enqueue("Venta actualizada.");
        }
        catch (Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al guardar cambios de la venta: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelEditSale()
    {
        IsEditSaleOpen = false;
        SelectedSaleForEdit = null;
        EditingDetails.Clear();
    }

    [RelayCommand]
    private void DeleteLinkedSale(Sale? sale)
    {
        if (sale == null) return;
        SaleToDelete = sale;
        IsDeleteSaleConfirmationOpen = true;
    }

    [RelayCommand]
    private void DeleteSaleFromEdit()
    {
        if (SelectedSaleForEdit == null) return;
        SaleToDelete = SelectedSaleForEdit;
        IsEditSaleOpen = false;
        IsDeleteSaleConfirmationOpen = true;
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
            try
            {
                var noteId = saleInDb.NoteId;
                _dbContext.Sales.Remove(saleInDb);
                _dbContext.SaveChanges();
                RecalculateNoteAmount(noteId);
                LoadNotes();
                IsDeleteSaleConfirmationOpen = false;
                SaleToDelete = null;
                ErrorMessageQueue.Enqueue("Venta eliminada por completo de la base de datos.");
            }
            catch (Exception ex)
            {
                ErrorMessageQueue.Enqueue($"Error al eliminar la venta: {ex.Message}");
            }
        }
        else
        {
            IsDeleteSaleConfirmationOpen = false;
            SaleToDelete = null;
        }
    }

    [RelayCommand]
    private void CancelDeleteSale()
    {
        IsDeleteSaleConfirmationOpen = false;
        SaleToDelete = null;
    }

    [RelayCommand]
    private void RequestFullDeleteNote(Note? note)
    {
        if (note != null)
        {
            NoteForFullDelete = note;
            IsConfirmFullDeleteOpen = true;
        }
    }

    [RelayCommand]
    private void ConfirmFullDeleteNote()
    {
        if (NoteForFullDelete != null)
        {
            var note = NoteForFullDelete;

            var relatedSales = _dbContext.Sales
                .Include(s => s.Details)
                .Where(s => s.NoteId == note.Id)
                .ToList();
            foreach (var sale in relatedSales)
            {
                foreach (var detail in sale.Details)
                {
                    _dbContext.SaleDetails.Remove(detail);
                }
                _dbContext.Sales.Remove(sale);
            }

            // Romper vínculos con ventas históricas
            var relatedHistoricalSales = _dbContext.HistoricalSales.Where(s => s.NoteId == note.Id).ToList();
            foreach (var hSale in relatedHistoricalSales)
            {
                hSale.NoteId = null;
                _dbContext.HistoricalSales.Update(hSale);
            }

            var manualDebts = _dbContext.ManualDebts.Where(md => md.NoteId == note.Id).ToList();
            foreach (var md in manualDebts)
            {
                _dbContext.ManualDebts.Remove(md);
            }

            var dbNote = _dbContext.Notes.Find(note.Id);
            if (dbNote != null)
            {
                _dbContext.Notes.Remove(dbNote);
            }

            _dbContext.SaveChanges();

            NoteForFullDelete = null;
            IsConfirmFullDeleteOpen = false;
            LoadNotes();
            ErrorMessageQueue.Enqueue("Deuda y todos sus registros asociados fueron eliminados.");
        }
    }

    [RelayCommand]
    private void CancelFullDeleteNote()
    {
        NoteForFullDelete = null;
        IsConfirmFullDeleteOpen = false;
    }

    [RelayCommand]
    private void RequestAddDebt(Note? note)
    {
        if (note != null)
        {
            SelectedNoteForAddDebt = note;
            AddDebtAmount = 0;
            AddDebtDetails = string.Empty;
            IsAddDebtModalOpen = true;
        }
    }

    [RelayCommand]
    private void ConfirmAddDebt()
    {
        if (SelectedNoteForAddDebt == null) return;
        if (AddDebtAmount <= 0)
        {
            ErrorMessageQueue.Enqueue("Por favor ingrese un monto válido mayor a 0.");
            return;
        }

        var note = _dbContext.Notes.Find(SelectedNoteForAddDebt.Id);
        if (note != null)
        {
            note.Amount += AddDebtAmount;
            var manualDebt = new ManualDebt
            {
                Amount = AddDebtAmount,
                Details = CapitalizeFirstLetter((AddDebtDetails ?? string.Empty).Trim()),
                Date = DateTime.Now,
                NoteId = note.Id
            };
            _dbContext.ManualDebts.Add(manualDebt);
            _dbContext.Notes.Update(note);
            _dbContext.SaveChanges();
            
            LoadNotes();
            IsAddDebtModalOpen = false;
            SelectedNoteForAddDebt = null;
            ErrorMessageQueue.Enqueue("Deuda agregada con éxito.");
        }
    }

    [RelayCommand]
    private void CancelAddDebt()
    {
        IsAddDebtModalOpen = false;
        SelectedNoteForAddDebt = null;
    }

    [RelayCommand]
    private void RequestDeleteAllPaidNotes()
    {
        IsConfirmDeleteAllPaidOpen = true;
    }

    [RelayCommand]
    private void CancelDeleteAllPaidNotes()
    {
        IsConfirmDeleteAllPaidOpen = false;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ConfirmDeleteAllPaidNotes()
    {
        IsConfirmDeleteAllPaidOpen = false;
        IsDeletingAllPaidNotes = true;

        await System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var paidNotesInDb = _dbContext.Notes.Where(n => n.IsPaid).ToList();
                if (paidNotesInDb.Any())
                {
                    foreach (var note in paidNotesInDb)
                    {
                        // Romper vínculos con ventas
                        var relatedSales = _dbContext.Sales.Where(s => s.NoteId == note.Id).ToList();
                        foreach (var sale in relatedSales)
                        {
                            sale.NoteId = null;
                            _dbContext.Sales.Update(sale);
                        }

                        // Romper vínculos con ventas históricas
                        var relatedHistoricalSales = _dbContext.HistoricalSales.Where(s => s.NoteId == note.Id).ToList();
                        foreach (var hSale in relatedHistoricalSales)
                        {
                            hSale.NoteId = null;
                            _dbContext.HistoricalSales.Update(hSale);
                        }

                        // Eliminar deudas manuales vinculadas
                        var manualDebts = _dbContext.ManualDebts.Where(md => md.NoteId == note.Id).ToList();
                        foreach (var md in manualDebts)
                        {
                            _dbContext.ManualDebts.Remove(md);
                        }

                        _dbContext.Notes.Remove(note);
                    }

                    _dbContext.SaveChanges();
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        ErrorMessageQueue.Enqueue("Historial de cuentas liquidadas eliminado por completo.");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ErrorMessageQueue.Enqueue($"Error al borrar el historial: {ex.Message}");
                });
            }
        });

        IsDeletingAllPaidNotes = false;
        LoadNotes();
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

    private string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        if (char.IsLetter(input[0]))
        {
            return char.ToUpper(input[0]) + input.Substring(1);
        }
        return input;
    }
}
