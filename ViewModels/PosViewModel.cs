using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Cajolote.Models;
using Cajolote.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignThemes.Wpf;
using System;
using Cajolote.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Cajolote.ViewModels;

public partial class CartItemViewModel : ObservableObject
{
    private readonly PosViewModel _parent;
    public SaleDetail SaleDetail { get; }

    public CartItemViewModel(SaleDetail saleDetail, PosViewModel parent)
    {
        SaleDetail = saleDetail;
        _parent = parent;
    }

    public Product Product => SaleDetail.Product;
    public decimal UnitPrice => SaleDetail.UnitPrice;

    public decimal Quantity
    {
        get => SaleDetail.Quantity;
        set
        {
            if (SaleDetail.Quantity != value)
            {
                SaleDetail.Quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(QuantityDisplay));
                _parent.UpdateTotal();
            }
        }
    }

    public string QuantityDisplay => Product.IsBulk ? $"{Quantity:0.000} kg" : $"{Quantity:0}";

    public decimal Subtotal => SaleDetail.Subtotal;

    public void UpdatePrice(decimal newPrice)
    {
        SaleDetail.UnitPrice = newPrice;
        Product.Price = newPrice;
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(Subtotal));
        _parent.UpdateTotal();
    }
}

public partial class PosViewModel : ObservableObject
{
    public event Action? ProductAddedDirectly;

    private readonly CajoloteDbContext _dbContext;
    private readonly SettingsService _settingsService;

    public UserSettings Settings => _settingsService.Settings;

    public ObservableCollection<Category> Categories { get; } = new();

    public System.Collections.Generic.List<string> AvailableIcons { get; } = new()
    {
        "Icon_Sabritas",
        "Icon_Bolillo",
        "Icon_QuesoOaxaca",
        "Chorizo",
        "Chile_Seco",
        "Pan_Dulce",
        "Huevo"
    };

    private void LoadCategories()
    {
        Categories.Clear();
        foreach (var c in _dbContext.Categories.ToList())
        {
            Categories.Add(c);
        }
    }

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private string _priceText = string.Empty;

    private bool _isSyncingPriceText;

    partial void OnPriceTextChanged(string value)
    {
        if (SelectedProduct != null && !_isSyncingPriceText)
        {
            var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string cleaned = (value ?? string.Empty).Trim().Replace(",", decSep).Replace(".", decSep);
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal price))
            {
                SelectedProduct.Price = price;
            }
            else if (string.IsNullOrWhiteSpace(cleaned))
            {
                SelectedProduct.Price = 0m;
            }
        }
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(IsProductBulk));
        OnPropertyChanged(nameof(IsProductUnit));
        OnPropertyChanged(nameof(PriceHint));

        _isSyncingPriceText = true;
        if (value != null && value.Price > 0)
        {
            PriceText = value.Price.ToString("G", System.Globalization.CultureInfo.CurrentCulture);
        }
        else
        {
            PriceText = string.Empty;
        }
        _isSyncingPriceText = false;
    }

    [ObservableProperty]
    private bool _isIconSelectorOpen;

    [RelayCommand]
    private void OpenIconSelector()
    {
        IsIconSelectorOpen = true;
    }

    [RelayCommand]
    private void CloseIconSelector()
    {
        IsIconSelectorOpen = false;
    }

    [RelayCommand]
    private void SelectIcon(string iconKey)
    {
        if (SelectedProduct != null)
        {
            SelectedProduct.IconKey = iconKey;
            OnPropertyChanged(nameof(SelectedProduct));
        }
        IsIconSelectorOpen = false;
    }

    [RelayCommand]
    private void RemoveIcon()
    {
        if (SelectedProduct != null)
        {
            SelectedProduct.IconKey = null;
            OnPropertyChanged(nameof(SelectedProduct));
        }
    }

    public void ToggleQuickProduct()
    {
        if (SelectedProduct != null)
        {
            SelectedProduct.IsQuickProduct = !SelectedProduct.IsQuickProduct;
            OnPropertyChanged(nameof(SelectedProduct));
        }
    }

    [ObservableProperty]
    private bool _isAddingProduct;

    private bool _productHasNoBarcode;
    public bool ProductHasNoBarcode
    {
        get => _productHasNoBarcode;
        set
        {
            if (SetProperty(ref _productHasNoBarcode, value))
            {
                OnPropertyChanged(nameof(ProductHasBarcode));
                OnPropertyChanged(nameof(CanEditBarcode));
                if (SelectedProduct != null)
                {
                    if (value)
                    {
                        SelectedProduct.Barcode = null;
                        SelectedProduct.IsQuickProduct = true;
                    }
                    else
                    {
                        SelectedProduct.IsQuickProduct = false;
                    }
                    OnPropertyChanged(nameof(SelectedProduct));
                }
            }
        }
    }

    public bool ProductHasBarcode => !ProductHasNoBarcode;
    public bool CanEditBarcode => ProductHasBarcode;

    public bool IsProductBulk
    {
        get => SelectedProduct?.IsBulk ?? false;
        set
        {
            if (SelectedProduct != null && SelectedProduct.IsBulk != value)
            {
                SelectedProduct.IsBulk = value;
                OnPropertyChanged(nameof(IsProductBulk));
                OnPropertyChanged(nameof(IsProductUnit));
                OnPropertyChanged(nameof(PriceHint));
            }
        }
    }

    public bool IsProductUnit
    {
        get => !(SelectedProduct?.IsBulk ?? false);
        set
        {
            if (SelectedProduct != null && SelectedProduct.IsBulk == value)
            {
                SelectedProduct.IsBulk = !value;
                OnPropertyChanged(nameof(IsProductBulk));
                OnPropertyChanged(nameof(IsProductUnit));
                OnPropertyChanged(nameof(PriceHint));
            }
        }
    }

    public string PriceHint => IsProductBulk ? "Precio por Kilogramo" : "Precio de Venta";

    private readonly Cajolote.Services.ICatalogCacheService _catalogCacheService;

    public PosViewModel(CajoloteDbContext dbContext, SettingsService settingsService, Cajolote.Services.ICatalogCacheService catalogCacheService)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _catalogCacheService = catalogCacheService;
        CartItems = new ObservableCollection<CartItemViewModel>();
        
        // Cargar productos rápidos (sin código de barras o marcados explícitamente)
        _allQuickProducts = _catalogCacheService.GetQuickProducts().ToList();
            
        QuickProducts = new ObservableCollection<Product>(_allQuickProducts);
        
        AvailableNotes = new ObservableCollection<Note>(_dbContext.Notes.Where(n => !n.IsPaid).ToList());
        LoadCategories();
    }

    public ISnackbarMessageQueue ErrorMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

    [ObservableProperty]
    private bool _isEditingPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuantityHint))]
    [NotifyPropertyChangedFor(nameof(PriceReferenceText))]
    [NotifyPropertyChangedFor(nameof(WeightUnitOptionText))]
    private CartItemViewModel? _editingCartItem;

    [ObservableProperty]
    private decimal _newPrice;

    [ObservableProperty]
    private bool _isEditingQuantity;

    [ObservableProperty]
    private string _newQuantity = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQuantityByPrice))]
    [NotifyPropertyChangedFor(nameof(QuantityHint))]
    private bool _isQuantityByWeight = true;

    [ObservableProperty]
    private string _newPriceAmount = string.Empty;

    public bool IsQuantityByPrice
    {
        get => !IsQuantityByWeight;
        set => IsQuantityByWeight = !value;
    }

    public string QuantityHint
    {
        get
        {
            if (EditingCartItem == null) return "Cantidad";
            if (!EditingCartItem.Product.IsBulk) return "Cantidad (unidades)";
            return _settingsService.Settings.BulkUnit == "g" ? "Cantidad (g)" : "Cantidad (kg)";
        }
    }

    public string WeightUnitOptionText
    {
        get
        {
            return _settingsService.Settings.BulkUnit == "g" ? "Por g" : "Por kg";
        }
    }

    public string PriceReferenceText
    {
        get
        {
            if (EditingCartItem == null) return string.Empty;
            if (!EditingCartItem.Product.IsBulk)
                return $"Precio de lista: {EditingCartItem.UnitPrice:C} / pza";
            
            if (_settingsService.Settings.BulkUnit == "g")
            {
                decimal pricePerGram = EditingCartItem.UnitPrice / 1000;
                return $"Precio de lista: {EditingCartItem.UnitPrice:C} / kg ({pricePerGram:C} / g)";
            }
            else
            {
                return $"Precio de lista: {EditingCartItem.UnitPrice:C} / kg";
            }
        }
    }

    private bool _isCalculating;
    private List<Product> _allQuickProducts = new();
    private System.Timers.Timer? _clearSearchTimer;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private double _searchClearProgress;

    [ObservableProperty]
    private bool _isSearchTimerLocked;

    partial void OnIsSearchTimerLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(LockIconKind));
        ResetClearSearchTimer();
    }

    public bool ShowSearchClearProgress => !string.IsNullOrEmpty(SearchText);
    public string LockIconKind => IsSearchTimerLocked ? "Lock" : "LockOpen";

    [RelayCommand]
    private void ToggleSearchTimerLock()
    {
        IsSearchTimerLocked = !IsSearchTimerLocked;
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
    private bool _isPrintModalOpen;

    private Sale? _pendingSale;

    public ObservableCollection<CartItemViewModel> CartItems { get; }
    public ObservableCollection<Product> QuickProducts { get; }
    public ObservableCollection<Product> OtherProducts { get; } = new();
    public ObservableCollection<Note> AvailableNotes { get; }

    public bool HasQuickProducts => QuickProducts.Count > 0;
    public bool HasOtherResults => OtherProducts.Count > 0;

    [ObservableProperty]
    private decimal _total;

    public void UpdateTotal()
    {
        Total = CartItems.Sum(x => x.Subtotal);
    }

    [RelayCommand]
    private void AddProduct(object? parameter = null)
    {
        Product? product = null;

        if (parameter is Product p)
        {
            product = p;
        }
        else if (parameter is string inputParam && !string.IsNullOrWhiteSpace(inputParam))
        {
            product = _catalogCacheService.FindProduct(inputParam);
        }

        if (product != null)
        {
            var existingItem = CartItems.FirstOrDefault(c => c.Product.Id == product.Id);
            if (product.IsBulk)
            {
                if (existingItem != null)
                {
                    EditingCartItem = existingItem;
                    if (existingItem.Quantity > 0)
                    {
                        decimal displayQuantity = _settingsService.Settings.BulkUnit == "g" ? existingItem.Quantity * 1000 : existingItem.Quantity;
                        NewQuantity = displayQuantity.ToString(_settingsService.Settings.BulkUnit == "g" ? "0" : "0.000", System.Globalization.CultureInfo.CurrentCulture);
                        NewPriceAmount = (existingItem.Quantity * existingItem.UnitPrice).ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
                    }
                    else
                    {
                        NewQuantity = string.Empty;
                        NewPriceAmount = string.Empty;
                    }
                }
                else
                {
                    var detail = new SaleDetail
                    {
                        ProductId = product.Id,
                        Product = product,
                        Quantity = 0,
                        UnitPrice = product.Price
                    };
                    var newItem = new CartItemViewModel(detail, this);
                    CartItems.Add(newItem);
                    EditingCartItem = newItem;
                    NewQuantity = string.Empty;
                    NewPriceAmount = string.Empty;
                }
                IsQuantityByWeight = true; // Por defecto "por kg"
                IsEditingQuantity = true;
            }
            else
            {
                if (existingItem != null)
                {
                    existingItem.Quantity++;
                }
                else
                {
                    var detail = new SaleDetail
                    {
                        ProductId = product.Id,
                        Product = product,
                        Quantity = 1,
                        UnitPrice = product.Price
                    };
                    CartItems.Add(new CartItemViewModel(detail, this));
                    UpdateTotal();
                }
                ProductAddedDirectly?.Invoke();
            }
        }
        else
        {
            string? searchCode = null;
            if (parameter is string inputParam && !string.IsNullOrWhiteSpace(inputParam))
            {
                searchCode = inputParam;
            }

            if (!string.IsNullOrWhiteSpace(searchCode))
            {
                LoadCategories();
                SelectedProduct = new Product
                {
                    Barcode = searchCode
                };
                ProductHasNoBarcode = false;
                IsAddingProduct = true;
            }
        }

        // Limpiar la barra de búsqueda al agregar cualquier producto
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SaveNewProduct()
    {
        if (SelectedProduct == null) return;

        if (string.IsNullOrWhiteSpace(SelectedProduct.Name) || 
            (!ProductHasNoBarcode && string.IsNullOrWhiteSpace(SelectedProduct.Barcode)) ||
            SelectedProduct.CategoryId == 0 ||
            SelectedProduct.Price <= 0)
        {
            ErrorMessageQueue.Enqueue("Error: Todos los campos del producto son requeridos y deben ser válidos.");
            return;
        }

        if (ProductHasNoBarcode)
        {
            SelectedProduct.Barcode = null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedProduct.Name))
        {
            string name = SelectedProduct.Name.Trim();
            if (name.Length > 0 && char.IsLetter(name[0]))
            {
                SelectedProduct.Name = char.ToUpper(name[0]) + name.Substring(1);
            }
            else
            {
                SelectedProduct.Name = name;
            }
        }

        if (SelectedProduct.Category != null)
        {
            SelectedProduct.CategoryId = SelectedProduct.Category.Id;
            SelectedProduct.Category = null;
        }

        _dbContext.Products.Add(SelectedProduct);
        _dbContext.SaveChanges();
        _catalogCacheService.AddProduct(SelectedProduct);

        // Cargar productos rápidos (sin código de barras o marcados explícitamente)
        _allQuickProducts = _catalogCacheService.GetQuickProducts().ToList();
        ApplyQuickProductsFilter();

        var savedProduct = SelectedProduct;
        IsAddingProduct = false;
        SelectedProduct = null;

        AddProduct(savedProduct);
    }

    [RelayCommand]
    private void CancelNewProduct()
    {
        IsAddingProduct = false;
        SelectedProduct = null;
    }

    [RelayCommand]
    private void IncreaseQuantity(CartItemViewModel? item)
    {
        if (item != null) item.Quantity++;
    }

    [RelayCommand]
    private void DecreaseQuantity(CartItemViewModel? item)
    {
        if (item != null && item.Quantity > 1) item.Quantity--;
    }

    [RelayCommand]
    private void RemoveItem(CartItemViewModel? item)
    {
        if (item != null)
        {
            CartItems.Remove(item);
            UpdateTotal();
        }
    }

    [RelayCommand]
    private void Checkout()
    {
        if (!CartItems.Any()) return;

        var sale = new Sale
        {
            Date = System.DateTime.Now,
            Total = this.Total,
            Details = CartItems.Select(c => c.SaleDetail).ToList(),
            IsPaid = true
        };

        var productRefs = new System.Collections.Generic.Dictionary<Cajolote.Models.SaleDetail, Cajolote.Models.Product>();
        foreach (var detail in sale.Details)
        {
            if (detail.Product != null)
            {
                productRefs[detail] = detail.Product;
                detail.Product = null!;
            }
        }

        _dbContext.Sales.Add(sale);

        _dbContext.SaveChanges();

        _dbContext.Entry(sale).State = EntityState.Detached;
        foreach (var detail in sale.Details)
        {
            _dbContext.Entry(detail).State = EntityState.Detached;
        }

        foreach (var detail in sale.Details)
        {
            if (productRefs.TryGetValue(detail, out var prod))
            {
                detail.Product = prod;
            }
        }

        _pendingSale = sale;
        IsPrintModalOpen = true;
    }

    [RelayCommand]
    private void ConfirmPrintTicket()
    {
        IsPrintModalOpen = false;
        if (_pendingSale != null)
        {
            PrintTicketAndClear(_pendingSale, doPrint: true, showPrintDialog: _settingsService.Settings.AlwaysAskBeforePrinting);
            _pendingSale = null;
        }
    }

    [RelayCommand]
    private void SkipPrintTicket()
    {
        IsPrintModalOpen = false;
        if (_pendingSale != null)
        {
            PrintTicketAndClear(_pendingSale, doPrint: false, showPrintDialog: false);
            _pendingSale = null;
        }
    }

    private void PrintTicketAndClear(Sale sale, bool doPrint, bool showPrintDialog)
    {
        // Extraemos perfil de la tienda
        var profile = _dbContext.StoreProfiles.FirstOrDefault();
        if (profile == null) profile = new StoreProfile();

        var ticketGenerator = App.Current.Services.GetService<Cajolote.Services.TicketGenerator>();
        if (ticketGenerator != null)
        {
            try
            {
                ticketGenerator.PrintTicket(sale, profile, doPrint, showPrintDialog);
            }
            catch (System.Exception ex)
            {
                ErrorMessageQueue.Enqueue($"Error al imprimir: {ex.Message}");
            }
        }

        CartItems.Clear();
        UpdateTotal();
    }

    [RelayCommand]
    private void CheckoutCredit()
    {
        if (!CartItems.Any()) return;
        
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
                Amount = this.Total,
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
                note.Amount += this.Total;
                _dbContext.Notes.Update(note);
            }
        }

        if (note == null)
        {
            ErrorMessageQueue.Enqueue("Error: No se pudo determinar la cuenta de destino.");
            return;
        }

        var sale = new Sale
        {
            Date = System.DateTime.Now,
            Total = this.Total,
            Details = CartItems.Select(c => c.SaleDetail).ToList(),
            IsPaid = false,
            NoteId = note.Id
        };

        var productRefs = new System.Collections.Generic.Dictionary<Cajolote.Models.SaleDetail, Cajolote.Models.Product>();
        foreach (var detail in sale.Details)
        {
            if (detail.Product != null)
            {
                productRefs[detail] = detail.Product;
                detail.Product = null!;
            }
        }

        _dbContext.Sales.Add(sale);

        _dbContext.SaveChanges();

        _dbContext.Entry(sale).State = EntityState.Detached;
        foreach (var detail in sale.Details)
        {
            _dbContext.Entry(detail).State = EntityState.Detached;
        }

        foreach (var detail in sale.Details)
        {
            if (productRefs.TryGetValue(detail, out var prod))
            {
                detail.Product = prod;
            }
        }

        IsCreditModalOpen = false;
        _pendingSale = sale;
        IsPrintModalOpen = true;
    }

    [RelayCommand]
    private void CancelCredit()
    {
        IsCreditModalOpen = false;
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        UpdateTotal();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void EditPrice(CartItemViewModel? item)
    {
        if (item != null)
        {
            EditingCartItem = item;
            NewPrice = item.UnitPrice;
            IsEditingPrice = true;
        }
    }

    [RelayCommand]
    private async Task SaveEditPriceAsync()
    {
        if (NewPrice <= 0)
        {
            ErrorMessageQueue.Enqueue("Error: El precio debe ser mayor a 0 y/o NO contener letras.");
            return;
        }

        if (EditingCartItem != null)
        {
            await _dbContext.Products
                .Where(p => p.Id == EditingCartItem.Product.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, NewPrice));

            _catalogCacheService.UpdateProductPrice(EditingCartItem.Product.Id, NewPrice);
            EditingCartItem.UpdatePrice(NewPrice);
        }

        IsEditingPrice = false;
        EditingCartItem = null;
    }

    [RelayCommand]
    private void CancelEditPrice()
    {
        IsEditingPrice = false;
        EditingCartItem = null;
    }
    partial void OnIsQuantityByWeightChanged(bool value)
    {
        OnPropertyChanged(nameof(IsQuantityByPrice));
        
        if (_isCalculating) return;

        if (value)
        {
            NewPriceAmount = string.Empty;
            if (EditingCartItem != null && EditingCartItem.Quantity > 0)
            {
                decimal displayQuantity = (EditingCartItem.Product.IsBulk && _settingsService.Settings.BulkUnit == "g")
                    ? EditingCartItem.Quantity * 1000
                    : EditingCartItem.Quantity;
                NewQuantity = displayQuantity.ToString((EditingCartItem.Product.IsBulk && _settingsService.Settings.BulkUnit == "g") ? "0" : "0.000", System.Globalization.CultureInfo.CurrentCulture);
                NewPriceAmount = (EditingCartItem.Quantity * EditingCartItem.UnitPrice).ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
            }
            else
            {
                NewQuantity = string.Empty;
            }
        }
        else
        {
            NewQuantity = string.Empty;
            NewPriceAmount = string.Empty;
        }
    }

    partial void OnNewQuantityChanged(string value)
    {
        if (_isCalculating || !IsQuantityByWeight || EditingCartItem == null) return;
        
        _isCalculating = true;
        try
        {
            var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string cleaned = (value ?? string.Empty).Replace(",", decSep).Replace(".", decSep);
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal q))
            {
                decimal quantityInKg = (EditingCartItem.Product.IsBulk && _settingsService.Settings.BulkUnit == "g")
                    ? q / 1000
                    : q;
                NewPriceAmount = (quantityInKg * EditingCartItem.UnitPrice).ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
            }
            else
            {
                NewPriceAmount = string.Empty;
            }
        }
        finally
        {
            _isCalculating = false;
        }
    }

    partial void OnNewPriceAmountChanged(string value)
    {
        if (_isCalculating || IsQuantityByWeight || EditingCartItem == null) return;
        
        _isCalculating = true;
        try
        {
            var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string cleaned = (value ?? string.Empty).Replace(",", decSep).Replace(".", decSep);
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal p) && EditingCartItem.UnitPrice > 0)
            {
                decimal q = p / EditingCartItem.UnitPrice;
                decimal displayQuantity = (EditingCartItem.Product.IsBulk && _settingsService.Settings.BulkUnit == "g")
                    ? q * 1000
                    : q;
                NewQuantity = displayQuantity.ToString((EditingCartItem.Product.IsBulk && _settingsService.Settings.BulkUnit == "g") ? "0" : "0.000", System.Globalization.CultureInfo.CurrentCulture);
            }
            else
            {
                NewQuantity = string.Empty;
            }
        }
        finally
        {
            _isCalculating = false;
        }
    }

    [RelayCommand]
    private void EditQuantity(CartItemViewModel? item)
    {
        if (item != null)
        {
            EditingCartItem = item;
            _isCalculating = true;
            try
            {
                IsQuantityByWeight = true; // Por defecto "por kg"
                if (item.Quantity > 0)
                {
                    if (item.Product.IsBulk)
                    {
                        decimal displayQuantity = _settingsService.Settings.BulkUnit == "g" ? item.Quantity * 1000 : item.Quantity;
                        NewQuantity = displayQuantity.ToString(_settingsService.Settings.BulkUnit == "g" ? "0" : "0.000", System.Globalization.CultureInfo.CurrentCulture);
                    }
                    else
                    {
                        NewQuantity = item.Quantity.ToString(System.Globalization.CultureInfo.CurrentCulture);
                    }
                    NewPriceAmount = (item.Quantity * item.UnitPrice).ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
                }
                else
                {
                    NewQuantity = string.Empty;
                    NewPriceAmount = string.Empty;
                }
            }
            finally
            {
                _isCalculating = false;
            }
            IsEditingQuantity = true;
        }
    }

    [RelayCommand]
    private void SaveEditQuantity()
    {
        decimal parsedQuantity = 0;

        if (EditingCartItem != null && EditingCartItem.Product.IsBulk && !IsQuantityByWeight)
        {
            // Modo por precio: calcular cantidad exacta a partir del precio ingresado para evitar errores de redondeo de 3 decimales
            var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string cleanedInput = (NewPriceAmount ?? string.Empty).Replace(",", decSep).Replace(".", decSep);

            if (!decimal.TryParse(cleanedInput, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal parsedPrice) || parsedPrice <= 0 || EditingCartItem.UnitPrice <= 0)
            {
                ErrorMessageQueue.Enqueue("Error: Ingrese un monto válido mayor a 0.");
                if (EditingCartItem.Quantity <= 0)
                {
                    CartItems.Remove(EditingCartItem);
                    UpdateTotal();
                    IsEditingQuantity = false;
                    EditingCartItem = null;
                }
                return;
            }
            parsedQuantity = parsedPrice / EditingCartItem.UnitPrice;
        }
        else
        {
            // Modo estándar por peso o unidad
            var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string cleanedInput = (NewQuantity ?? string.Empty).Replace(",", decSep).Replace(".", decSep);

            if (!decimal.TryParse(cleanedInput, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsedQuantity) || parsedQuantity <= 0)
            {
                ErrorMessageQueue.Enqueue("Error: Ingrese una cantidad válida mayor a 0.");
                if (EditingCartItem != null && EditingCartItem.Quantity <= 0)
                {
                    CartItems.Remove(EditingCartItem);
                    UpdateTotal();
                    IsEditingQuantity = false;
                    EditingCartItem = null;
                }
                return;
            }
            if (EditingCartItem != null && EditingCartItem.Product.IsBulk && _settingsService.Settings.BulkUnit == "g")
            {
                parsedQuantity = parsedQuantity / 1000;
            }
        }

        if (EditingCartItem != null)
        {
            EditingCartItem.Quantity = parsedQuantity;
        }

        IsEditingQuantity = false;
        EditingCartItem = null;
    }

    [RelayCommand]
    private void CancelEditQuantity()
    {
        if (EditingCartItem != null && EditingCartItem.Quantity <= 0)
        {
            CartItems.Remove(EditingCartItem);
            UpdateTotal();
        }
        IsEditingQuantity = false;
        EditingCartItem = null;
    }

    private System.Timers.Timer? _searchDebounceTimer;

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            IsSearchTimerLocked = false;
        }

        if (_searchDebounceTimer == null)
        {
            _searchDebounceTimer = new System.Timers.Timer(250);
            _searchDebounceTimer.AutoReset = false;
            _searchDebounceTimer.Elapsed += (s, e) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ApplyQuickProductsFilter();
                });
            };
        }
        
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();

        ResetClearSearchTimer();
        OnPropertyChanged(nameof(ShowSearchClearProgress));
    }

    private void ApplyQuickProductsFilter()
    {
        QuickProducts.Clear();
        var filterText = (SearchText ?? string.Empty).Trim().ToLower();
        var filtered = string.IsNullOrWhiteSpace(filterText)
            ? _allQuickProducts
            : _allQuickProducts.Where(p => p.Name.ToLower().Contains(filterText)).ToList();

        foreach (var p in filtered)
        {
            QuickProducts.Add(p);
        }

        // Buscar productos que NO son rápidos de toda la base de datos
        OtherProducts.Clear();
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            var otherFiltered = _catalogCacheService.SearchProducts(filterText)
                .Where(p => !p.IsQuickProduct && p.Barcode != "SN" && !string.IsNullOrEmpty(p.Barcode))
                .Take(10)
                .ToList();

            foreach (var p in otherFiltered)
            {
                OtherProducts.Add(p);
            }
        }

        OnPropertyChanged(nameof(HasQuickProducts));
        OnPropertyChanged(nameof(HasOtherResults));
    }

    private const double MaxTimerMs = 10000; // 10 segundos
    private const double TimerTickMs = 100; // Ticks cada 100ms
    private double _elapsedMs;

    private void ResetClearSearchTimer()
    {
        if (_clearSearchTimer == null)
        {
            _clearSearchTimer = new System.Timers.Timer(TimerTickMs);
            _clearSearchTimer.Elapsed += (s, e) =>
            {
                if (IsSearchTimerLocked)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        SearchClearProgress = 100;
                    });
                    return;
                }

                _elapsedMs += TimerTickMs;
                double progress = Math.Max(0, 100 * (1 - (_elapsedMs / MaxTimerMs)));
                
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    SearchClearProgress = progress;
                    if (_elapsedMs >= MaxTimerMs)
                    {
                        _clearSearchTimer.Stop();
                        SearchText = string.Empty;
                    }
                });
            };
        }
        else
        {
            _clearSearchTimer.Stop();
        }

        _elapsedMs = 0;
        SearchClearProgress = 100;

        if (!string.IsNullOrEmpty(SearchText) && !IsSearchTimerLocked)
        {
            _clearSearchTimer.Start();
        }
        else
        {
            SearchClearProgress = 100;
        }

        OnPropertyChanged(nameof(ShowSearchClearProgress));
    }
}
