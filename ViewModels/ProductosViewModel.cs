using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;
using Cajolote.Models;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using System;

namespace Cajolote.ViewModels;

public partial class ProductosViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;
    private readonly Cajolote.Services.ICatalogCacheService _catalogCacheService;

    public ISnackbarMessageQueue ErrorMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedSearchType = "por nombre";

    [ObservableProperty]
    private string _selectedLimit = "10";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isDeleteConfirmationOpen;

    [ObservableProperty]
    private Product? _productToDelete;

    [ObservableProperty]
    private bool _isDeleteCategoryConfirmationOpen;

    [ObservableProperty]
    private Category? _categoryToDelete;

    [ObservableProperty]
    private bool _isIconSelectorOpen;

    public System.Collections.Generic.List<string> SearchTypes { get; } = new()
    {
        "por nombre",
        "por código",
        "por precio",
        "por categoría"
    };

    public System.Collections.Generic.List<string> LimitOptions { get; } = new()
    {
        "10",
        "25",
        "50",
        "100"
    };

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

    partial void OnSearchTextChanged(string value) => _ = ApplyFilterAsync();
    partial void OnSelectedSearchTypeChanged(string value) => _ = ApplyFilterAsync();
    partial void OnSelectedLimitChanged(string value) => _ = ApplyFilterAsync();

    [ObservableProperty]
    private Product _selectedProduct = new();

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

    partial void OnSelectedProductChanged(Product value)
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

    [ObservableProperty]
    private bool _isEditing;

    private bool _productHasNoBarcode;
    public bool ProductHasNoBarcode
    {
        get => _productHasNoBarcode;
        set
        {
            if (SetProperty(ref _productHasNoBarcode, value))
            {
                OnPropertyChanged(nameof(ProductHasBarcode));
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

    public void ToggleQuickProduct()
    {
        if (ProductHasBarcode && SelectedProduct != null)
        {
            SelectedProduct.IsQuickProduct = !SelectedProduct.IsQuickProduct;
            OnPropertyChanged(nameof(SelectedProduct));
        }
    }

    [ObservableProperty]
    private Category _selectedCategory = new();

    [ObservableProperty]
    private bool _isEditingCategory;

    [ObservableProperty]
    private bool _isManagingCategories = false;

    partial void OnIsManagingCategoriesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsManagingProducts));
        OnPropertyChanged(nameof(ManageButtonText));
        OnPropertyChanged(nameof(InventoryTitle));
    }

    public bool IsManagingProducts => !IsManagingCategories;
    public string ManageButtonText => IsManagingCategories ? "Ver Productos" : "Administrar Categorías";
    public string InventoryTitle => IsManagingCategories ? "Administrar Categorías" : "Inventario de Productos";

    public ProductosViewModel(CajoloteDbContext dbContext, Cajolote.Services.ICatalogCacheService catalogCacheService)
    {
        _dbContext = dbContext;
        _catalogCacheService = catalogCacheService;
        LoadData();
    }

    [RelayCommand]
    private void LoadData()
    {
        Categories.Clear();
        var categories = _dbContext.Categories.ToList();
        foreach (var c in categories) Categories.Add(c);

        _ = ApplyFilterAsync();
    }

    private System.Threading.CancellationTokenSource? _filterCts;

    private async System.Threading.Tasks.Task ApplyFilterAsync()
    {
        _filterCts?.Cancel();
        var cts = new System.Threading.CancellationTokenSource();
        _filterCts = cts;

        IsLoading = true;
        try
        {
            // Un pequeño retraso para asegurar que el loader sea visible
            await System.Threading.Tasks.Task.Delay(150, cts.Token);

            IQueryable<Product> query = _dbContext.Products.Include(p => p.Category);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var text = SearchText.Trim().ToLower();
                switch (SelectedSearchType)
                {
                    case "por nombre":
                        query = query.Where(p => p.Name.ToLower().Contains(text));
                        break;
                    case "por código":
                        query = query.Where(p => p.Barcode != null && p.Barcode.ToLower().Contains(text));
                        break;
                    case "por precio":
                        var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                        string cleaned = text.Replace(",", decSep).Replace(".", decSep);
                        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal price))
                        {
                            query = query.Where(p => p.Price == price);
                        }
                        else
                        {
                            query = query.Where(p => false);
                        }
                        break;
                    case "por categoría":
                        query = query.Where(p => p.Category.Name.ToLower().Contains(text));
                        break;
                }
            }

            if (int.TryParse(SelectedLimit, out int limitValue))
            {
                query = query.Take(limitValue);
            }

            var filtered = await query.ToListAsync(cts.Token);
            
            if (cts.Token.IsCancellationRequested) return;

            Products.Clear();
            foreach (var p in filtered) Products.Add(p);
        }
        catch (System.OperationCanceledException)
        {
            // Búsqueda cancelada por una más nueva, ignorar
        }
        catch (System.Exception ex)
        {
            ErrorMessageQueue.Enqueue($"Error al buscar productos: {ex.Message}");
        }
        finally
        {
            if (_filterCts == cts)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void AddNew()
    {
        SelectedProduct = new Product();
        ProductHasNoBarcode = false;
        IsEditing = true;
        IsEditingCategory = false;
    }

    [RelayCommand]
    private void AddNewCategory()
    {
        SelectedCategory = new Category();
        IsEditingCategory = true;
        IsEditing = false;
    }

    [RelayCommand]
    private void Edit(Product product)
    {
        if (product != null)
        {
            SelectedProduct = new Product 
            { 
                Id = product.Id, 
                Barcode = product.Barcode,
                Name = product.Name,
                Price = product.Price,
                CategoryId = product.CategoryId,
                IsQuickProduct = product.IsQuickProduct,
                IsBulk = product.IsBulk,
                IconKey = product.IconKey
            };
            ProductHasNoBarcode = string.IsNullOrWhiteSpace(product.Barcode);
            IsEditing = true;
        }
    }

    [RelayCommand]
    private void Save()
    {
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

        if (SelectedProduct.Id == 0)
        {
            _dbContext.Products.Add(SelectedProduct);
        }
        else
        {
            var existing = _dbContext.Products.Local.FirstOrDefault(p => p.Id == SelectedProduct.Id) 
                        ?? _dbContext.Products.Find(SelectedProduct.Id);
            if (existing != null)
            {
                existing.Barcode = SelectedProduct.Barcode;
                existing.Name = SelectedProduct.Name;
                existing.Price = SelectedProduct.Price;
                existing.CategoryId = SelectedProduct.CategoryId;
                existing.IsQuickProduct = SelectedProduct.IsQuickProduct;
                existing.IsBulk = SelectedProduct.IsBulk;
                existing.IconKey = SelectedProduct.IconKey;
                _dbContext.Products.Update(existing);
            }
        }
        
        _dbContext.SaveChanges();
        _catalogCacheService.RefreshCache();
        IsEditing = false;
        LoadData();
    }

    [RelayCommand]
    private void Delete(Product product)
    {
        if (product != null)
        {
            ProductToDelete = product;
            IsDeleteConfirmationOpen = true;
        }
    }

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (ProductToDelete != null)
        {
            _dbContext.Products.Remove(ProductToDelete);
            _dbContext.SaveChanges();
            _catalogCacheService.RefreshCache();
            LoadData();
        }
        IsDeleteConfirmationOpen = false;
        ProductToDelete = null;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteConfirmationOpen = false;
        ProductToDelete = null;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsEditing = false;
        SelectedProduct = new Product();
        LoadData();
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

    [RelayCommand]
    private void SaveCategory()
    {
        if (string.IsNullOrWhiteSpace(SelectedCategory.Name))
        {
            ErrorMessageQueue.Enqueue("Error: El nombre de la categoría es requerido.");
            return;
        }

        if (SelectedCategory.Id == 0)
        {
            _dbContext.Categories.Add(SelectedCategory);
        }
        else
        {
            var existing = _dbContext.Categories.Local.FirstOrDefault(c => c.Id == SelectedCategory.Id) 
                        ?? _dbContext.Categories.Find(SelectedCategory.Id);
            if (existing != null)
            {
                existing.Name = SelectedCategory.Name;
                _dbContext.Categories.Update(existing);
            }
        }
        
        _dbContext.SaveChanges();
        IsEditingCategory = false;
        LoadData();
    }

    [RelayCommand]
    private void CancelCategory()
    {
        IsEditingCategory = false;
        SelectedCategory = new Category();
        LoadData();
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        if (category != null)
        {
            SelectedCategory = new Category 
            { 
                Id = category.Id, 
                Name = category.Name 
            };
            IsEditingCategory = true;
            IsEditing = false;
        }
    }

    [RelayCommand]
    private void DeleteCategory(Category category)
    {
        if (category != null)
        {
            // Validate if there are products using this category
            if (_dbContext.Products.Any(p => p.CategoryId == category.Id))
            {
                ErrorMessageQueue.Enqueue("Error: No se puede eliminar una categoría que tiene productos asociados.");
                return;
            }
            
            CategoryToDelete = category;
            IsDeleteCategoryConfirmationOpen = true;
        }
    }

    [RelayCommand]
    private void ConfirmDeleteCategory()
    {
        if (CategoryToDelete != null)
        {
            _dbContext.Categories.Remove(CategoryToDelete);
            _dbContext.SaveChanges();
            LoadData();
        }
        IsDeleteCategoryConfirmationOpen = false;
        CategoryToDelete = null;
    }

    [RelayCommand]
    private void CancelDeleteCategory()
    {
        IsDeleteCategoryConfirmationOpen = false;
        CategoryToDelete = null;
    }

    [RelayCommand]
    private void ToggleManage()
    {
        IsManagingCategories = !IsManagingCategories;
        IsEditing = false;
        IsEditingCategory = false;
    }
}
