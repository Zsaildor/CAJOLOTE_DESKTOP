using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cajolote.Data;
using Cajolote.Models;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace Cajolote.ViewModels;

public partial class RevisarPrecioViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;
    private readonly Cajolote.Services.ICatalogCacheService _catalogCacheService;

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

    public RevisarPrecioViewModel(CajoloteDbContext dbContext, Cajolote.Services.ICatalogCacheService catalogCacheService)
    {
        _dbContext = dbContext;
        _catalogCacheService = catalogCacheService;
        LoadCategories();
    }

    private void LoadCategories()
    {
        Categories.Clear();
        foreach (var c in _dbContext.Categories.ToList())
        {
            Categories.Add(c);
        }
    }

    [ObservableProperty]
    private string _barcodeInput = string.Empty;

    [ObservableProperty]
    private Product? _foundProduct;

    [ObservableProperty]
    private string _statusMessage = "Escanea un producto para ver su precio";

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

    public void ToggleQuickProduct()
    {
        if (SelectedProduct != null)
        {
            SelectedProduct.IsQuickProduct = !SelectedProduct.IsQuickProduct;
            OnPropertyChanged(nameof(SelectedProduct));
        }
    }

    [ObservableProperty]
    private bool _isEditingProduct;

    [ObservableProperty]
    private bool _isAddingProduct;

    [ObservableProperty]
    private bool _isIconSelectorOpen;

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
    public bool CanEditBarcode => IsAddingProduct && ProductHasBarcode;

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

    [RelayCommand]
    private void CheckPrice()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

        _dbContext.ChangeTracker.Clear(); // Limpiar el tracker para asegurar datos frescos
        FoundProduct = _dbContext.Products.Include(p => p.Category).AsNoTracking().FirstOrDefault(p => p.Barcode == BarcodeInput);

        if (FoundProduct != null)
        {
            StatusMessage = "¡Producto Encontrado!";
        }
        else
        {
            StatusMessage = "Producto no encontrado";
            // Pre-fill barcode and open registration modal
            LoadCategories();
            SelectedProduct = new Product
            {
                Barcode = BarcodeInput
            };
            ProductHasNoBarcode = false;
            IsAddingProduct = true;
            IsEditingProduct = true;
        }

        BarcodeInput = string.Empty;
    }

    [RelayCommand]
    private void EditProduct()
    {
        if (FoundProduct != null)
        {
            LoadCategories();
            SelectedProduct = new Product
            {
                Id = FoundProduct.Id,
                Barcode = FoundProduct.Barcode,
                Name = FoundProduct.Name,
                Price = FoundProduct.Price,
                CategoryId = FoundProduct.CategoryId,
                IsQuickProduct = FoundProduct.IsQuickProduct,
                IsBulk = FoundProduct.IsBulk,
                IconKey = FoundProduct.IconKey
            };
            ProductHasNoBarcode = string.IsNullOrWhiteSpace(FoundProduct.Barcode);
            IsAddingProduct = false;
            IsEditingProduct = true;
        }
    }

    [RelayCommand]
    private void SaveProduct()
    {
        if (SelectedProduct == null) return;

        if (string.IsNullOrWhiteSpace(SelectedProduct.Name) || 
            (!ProductHasNoBarcode && string.IsNullOrWhiteSpace(SelectedProduct.Barcode)) ||
            SelectedProduct.CategoryId == 0 ||
            SelectedProduct.Price <= 0)
        {
            StatusMessage = "Error: Todos los campos del producto son requeridos y deben ser válidos.";
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

        bool saved = false;
        if (IsAddingProduct)
        {
            _dbContext.Products.Add(SelectedProduct);
            _dbContext.SaveChanges();
            saved = true;

            _dbContext.ChangeTracker.Clear(); // Limpiar tracker
            FoundProduct = _dbContext.Products.Include(p => p.Category).AsNoTracking().FirstOrDefault(p => p.Id == SelectedProduct.Id);
            StatusMessage = "¡Producto registrado con éxito!";
        }
        else
        {
            _dbContext.ChangeTracker.Clear(); // Limpiar tracker antes de buscar para evitar conflictos
            var existing = _dbContext.Products.Find(SelectedProduct.Id);
            if (existing != null)
            {
                existing.Barcode = SelectedProduct.Barcode;
                existing.Name = SelectedProduct.Name;
                existing.Price = SelectedProduct.Price;
                existing.CategoryId = SelectedProduct.CategoryId;
                existing.IsQuickProduct = SelectedProduct.IsQuickProduct;
                existing.IsBulk = SelectedProduct.IsBulk;
                existing.IconKey = SelectedProduct.IconKey;
                _dbContext.SaveChanges();
                saved = true;

                _dbContext.ChangeTracker.Clear(); // Limpiar tracker después de guardar para que la consulta sea fresca
                FoundProduct = null;
                FoundProduct = _dbContext.Products.Include(p => p.Category).AsNoTracking().FirstOrDefault(p => p.Id == existing.Id);
                StatusMessage = "¡Producto actualizado con éxito!";
            }
        }

        if (saved)
        {
            _catalogCacheService.RefreshCache();
        }

        IsEditingProduct = false;
    }

    [RelayCommand]
    private void CancelProduct()
    {
        IsEditingProduct = false;
        SelectedProduct = null;
    }

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
}
