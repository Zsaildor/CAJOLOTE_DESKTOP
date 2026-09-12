using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;
using Cajolote.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cajolote.ViewModels;

public partial class EstadisticasTotalesViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;

    public EstadisticasTotalesViewModel(CajoloteDbContext dbContext)
    {
        _dbContext = dbContext;
        LoadStats();
    }

    [ObservableProperty] private decimal _ventasTotales;
    [ObservableProperty] private decimal _ventasAnio;
    [ObservableProperty] private int _transaccionesTotales;
    [ObservableProperty] private decimal _ticketPromedio;

    [ObservableProperty] private ISeries[] _ventasPorMesSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _ventasPorMesXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _ventasPorMesYAxes = Array.Empty<Axis>();
    [ObservableProperty] private ISeries[] _categorySeries = Array.Empty<ISeries>();
    [ObservableProperty] private string _categoryChartCenterTitle = "Total";
    [ObservableProperty] private string _categoryChartCenterValue = "";
    [ObservableProperty] private CategoryLegendItem? _selectedLegendItem;
    private decimal _totalRevenue;

    public ObservableCollection<CategoryLegendItem> CategoryLegendItems { get; } = new();

    partial void OnSelectedLegendItemChanged(CategoryLegendItem? value)
    {
        if (value != null)
        {
            CategoryChartCenterTitle = value.CategoryName;
            CategoryChartCenterValue = value.Revenue.ToString("C");
        }
        else
        {
            CategoryChartCenterTitle = "Total";
            CategoryChartCenterValue = _totalRevenue.ToString("C");
        }

        foreach (var series in CategorySeries)
        {
            if (series is PieSeries<double> pieSeries)
            {
                if (value != null && pieSeries.Name == value.CategoryName)
                {
                    pieSeries.Pushout = 15;
                }
                else
                {
                    pieSeries.Pushout = 0;
                }
            }
        }
    }

    [ObservableProperty] private ISeries[] _topProductsSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _topProductsXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _topProductsYAxes = Array.Empty<Axis>();

    [ObservableProperty] private ISeries[] _peakHoursSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _peakHoursXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _peakHoursYAxes = Array.Empty<Axis>();

    public ObservableCollection<ProductSaleStat> TopProducts { get; } = new();
    public ObservableCollection<PeakHourStat> PeakHours { get; } = new();

    private void LoadStats()
    {
        var today = DateTime.Today;
        
        // Calcular el inicio de la semana (Lunes actual)
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff).Date;
        
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // Fetch active sales
        var activeSales = _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Category)
            .Where(s => s.IsPaid)
            .ToList();

        // Fetch historical sales and map them to Sale objects in memory
        var historicalSales = _dbContext.HistoricalSales
            .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Category)
            .Where(s => s.IsPaid)
            .Select(hs => new Sale
            {
                Id = hs.Id,
                Date = hs.Date,
                Total = hs.Total,
                IsSynced = hs.IsSynced,
                IsPaid = hs.IsPaid,
                NoteId = hs.NoteId,
                Note = hs.Note,
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

        // Combine all records for consolidated lifetime statistics
        var allSales = activeSales.Concat(historicalSales).ToList();

        // Calculate card values
        VentasTotales = allSales.Sum(s => s.Total);
        VentasAnio = allSales.Where(s => s.Date.Year == today.Year).Sum(s => s.Total);
        TransaccionesTotales = allSales.Count;
        TicketPromedio = TransaccionesTotales > 0 ? VentasTotales / TransaccionesTotales : 0;

        // Calculate Top Products of all time
        var productGroups = allSales.SelectMany(s => s.Details)
            .GroupBy(d => new { d.Product.Name, d.Product.IsBulk })
            .Select(g => new ProductSaleStat
            {
                ProductName = g.Key.Name,
                IsBulk = g.Key.IsBulk,
                QuantitySold = g.Sum(d => g.Key.IsBulk ? 1 : d.Quantity)
            })
            .OrderByDescending(p => p.QuantitySold)
            .Take(5)
            .ToList();

        TopProducts.Clear();
        foreach (var p in productGroups) TopProducts.Add(p);

        // Chart for Top Products
        var productValues = new double[productGroups.Count];
        var productLabels = new string[productGroups.Count];
        for (int i = 0; i < productGroups.Count; i++)
        {
            productValues[i] = (double)productGroups[i].QuantitySold;
            productLabels[i] = productGroups[i].ProductName;
        }
        Array.Reverse(productValues);
        Array.Reverse(productLabels);

        TopProductsSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Values = productValues,
                Name = "Vendidos",
                Fill = new SolidColorPaint(SKColor.Parse("#00BBF9")),
                MaxBarWidth = 12,
                Rx = 3,
                Ry = 3,
                XToolTipLabelFormatter = point => $"{point.Coordinate.PrimaryValue} vendidos"
            }
        };

        TopProductsXAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                Labeler = value => value.ToString("0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 10
            }
        };

        TopProductsYAxes = new Axis[]
        {
            new Axis
            {
                Labels = productLabels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 10
            }
        };

        // Generate monthly sales chart (January to December) for the current year
        var monthsOfYear = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        var monthlySalesData = new double[12];
        var currentYear = today.Year;
        
        for (int i = 0; i < 12; i++)
        {
            int monthNum = i + 1;
            monthlySalesData[i] = (double)allSales
                .Where(s => s.Date.Year == currentYear && s.Date.Month == monthNum)
                .Sum(s => s.Total);
        }

        var columnSeries = new ColumnSeries<double>
        {
            Values = monthlySalesData,
            Name = "Ventas",
            Fill = new SolidColorPaint(SKColor.Parse("#A78BFA")),
            MaxBarWidth = 40,
            Rx = 4,
            Ry = 4
        };

        columnSeries.PointMeasured += (point) =>
        {
            if (point.Visual is null) return;
            if (point.Index == today.Month - 1)
            {
                point.Visual.Fill = new SolidColorPaint(SKColor.Parse("#A78BFA"));
            }
            else
            {
                point.Visual.Fill = new SolidColorPaint(SKColor.Parse("#A78BFA").WithAlpha(153)); // 60% opacity
            }
        };

        VentasPorMesSeries = new ISeries[]
        {
            columnSeries
        };

        VentasPorMesXAxes = new Axis[]
        {
            new Axis
            {
                Labels = monthsOfYear,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 12
            }
        };

        VentasPorMesYAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                Labeler = value => value.ToString("C0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 12
            }
        };

        // Calculate consolidated peak hours
        var peakHoursData = allSales
            .GroupBy(s => (s.Date.Hour / 2) * 2)
            .Select(g => new PeakHourStat
            {
                TimeRange = $"🕐 {g.Key:D2}:00 - {g.Key + 2:D2}:00",
                TotalSales = g.Sum(s => s.Total)
            })
            .OrderByDescending(p => p.TotalSales)
            .Take(3)
            .ToList();

        PeakHours.Clear();
        foreach (var ph in peakHoursData) PeakHours.Add(ph);

        // Chart for Peak Hours (chronological)
        var hours = new int[] { 8, 10, 12, 14, 16, 18, 20, 22 };
        var peakSalesData = new double[hours.Length];
        var peakXLabels = new string[hours.Length];

        for (int i = 0; i < hours.Length; i++)
        {
            var h = hours[i];
            peakSalesData[i] = (double)allSales
                .Where(s => s.Date.Hour >= h && s.Date.Hour < h + 2)
                .Sum(s => s.Total);
            peakXLabels[i] = $"{h:D2}:00";
        }

        PeakHoursSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = peakSalesData,
                Name = "Ventas",
                Fill = new SolidColorPaint(SKColor.Parse("#1AFFD166")), // Transparent durazno
                Stroke = new SolidColorPaint(SKColor.Parse("#FFD166")) { StrokeThickness = 3 },
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#FFD166")),
                GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 }
            }
        };

        PeakHoursXAxes = new Axis[]
        {
            new Axis
            {
                Labels = peakXLabels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 10
            }
        };

        PeakHoursYAxes = new Axis[]
        {
            new Axis
            {
                Labeler = value => value.ToString("C0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 10
            }
        };

        // Generate consolidated category breakdown
        var categoryGroups = allSales.SelectMany(s => s.Details)
            .Where(d => d.Product.Category != null)
            .GroupBy(d => d.Product.Category.Name)
            .Select(g => new
            {
                CategoryName = g.Key,
                Revenue = (double)g.Sum(d => d.Quantity * d.UnitPrice)
            })
            .OrderByDescending(g => g.Revenue)
            .ToList();

        var pastelColors = new string[]
        {
            "#A8DADC", // Soft Teal
            "#FBC4AB", // Soft Orange/Peach
            "#CDB4DB", // Soft Lavender
            "#E2F0CB", // Soft Mint
            "#FFCAD4", // Soft Pink
            "#B3C5D7", // Soft Blue-Grey
            "#FFB5A7", // Soft Salmon
            "#F4E285", // Soft Yellow
            "#D8F3DC", // Soft Mint Green
            "#E8AEB7"  // Soft Lilac
        };

        decimal totalRevenue = (decimal)categoryGroups.Sum(c => c.Revenue);
        _totalRevenue = totalRevenue;
        CategoryChartCenterTitle = "Total";
        CategoryChartCenterValue = totalRevenue.ToString("C");

        CategoryLegendItems.Clear();
        var pieSeries = new List<ISeries>();
        int colorIndex = 0;

        foreach (var cat in categoryGroups)
        {
            var colorHex = pastelColors[colorIndex % pastelColors.Length];
            colorIndex++;

            var currentCategoryName = cat.CategoryName;
            var currentCategoryRevenue = (decimal)cat.Revenue;

            CategoryLegendItems.Add(new CategoryLegendItem
            {
                CategoryName = currentCategoryName,
                Revenue = currentCategoryRevenue,
                ColorHex = colorHex
            });

            var series = new PieSeries<double>
            {
                Values = new double[] { cat.Revenue },
                Name = currentCategoryName,
                InnerRadius = 65,
                Fill = new SolidColorPaint(SKColor.Parse(colorHex))
            };

            series.DataPointerDown += (chart, point) =>
            {
                var matchingLegend = CategoryLegendItems.FirstOrDefault(l => l.CategoryName == currentCategoryName);
                if (SelectedLegendItem == matchingLegend)
                {
                    SelectedLegendItem = null;
                }
                else
                {
                    SelectedLegendItem = matchingLegend;
                }
            };

            pieSeries.Add(series);
        }
        
        CategorySeries = pieSeries.ToArray();
    }
}
