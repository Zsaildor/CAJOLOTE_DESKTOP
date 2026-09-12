using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cajolote.ViewModels;

public class CategoryLegendItem
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public string RevenueDisplay => Revenue.ToString("C");
    public string ColorHex { get; set; } = "#00000000";
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;
    private decimal _totalRevenue;

    public DashboardViewModel(CajoloteDbContext dbContext)
    {
        _dbContext = dbContext;
        LoadStatistics();
    }

    [ObservableProperty]
    private int _todaySalesCount;

    [ObservableProperty]
    private string _categoryChartCenterTitle = "Total";

    [ObservableProperty]
    private string _categoryChartCenterValue = "";

    [ObservableProperty]
    private CategoryLegendItem? _selectedLegendItem;

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

        // Lift/push out the selected category slice
        foreach (var series in TodayCategorySeries)
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

    public ObservableCollection<CategoryLegendItem> CategoryLegendItems { get; } = new();

    [ObservableProperty]
    private decimal _todayTotalAmount;

    [ObservableProperty]
    private int _todayProductsSold;

    [ObservableProperty]
    private decimal _averageTicketToday;

    [ObservableProperty]
    private ISeries[] _salesPerHourSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _salesPerHourXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _salesPerHourYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] _todayCategorySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _topProductsSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _topProductsXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _topProductsYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private bool _hasUnpaidSalesToday;

    [ObservableProperty]
    private ISeries[] _unpaidSalesSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _unpaidSalesXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _unpaidSalesYAxes = Array.Empty<Axis>();

    public ObservableCollection<ProductSaleStat> TodayTopProducts { get; } = new();

    public void LoadStatistics()
    {
        var today = DateTime.Today;
        
        var todaysSales = _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Category)
            .Where(s => s.Date >= today && s.IsPaid)
            .ToList();

        TodaySalesCount = todaysSales.Count;
        TodayTotalAmount = todaysSales.Sum(s => s.Total);
        TodayProductsSold = (int)todaysSales.SelectMany(s => s.Details)
            .Sum(d => d.Product != null && d.Product.IsBulk ? 1 : d.Quantity);
        
        AverageTicketToday = TodaySalesCount > 0 ? TodayTotalAmount / TodaySalesCount : 0;

        // Group today's sales in 2-hour blocks from 8:00 to 22:00
        var hours = new int[] { 8, 10, 12, 14, 16, 18, 20, 22 };
        var salesData = new double[hours.Length];
        var xLabels = new string[hours.Length];

        for (int i = 0; i < hours.Length; i++)
        {
            var h = hours[i];
            salesData[i] = (double)todaysSales
                .Where(s => s.Date.Hour >= h && s.Date.Hour < h + 2)
                .Sum(s => s.Total);
            xLabels[i] = $"{h:D2}:00";
        }

        SalesPerHourSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = salesData,
                Name = "Ventas",
                Fill = new SolidColorPaint(SKColor.Parse("#1A9B5DE5")), // Transparent purple
                Stroke = new SolidColorPaint(SKColor.Parse("#9B5DE5")) { StrokeThickness = 3 },
                GeometrySize = 10,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#9B5DE5")),
                GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 }
            }
        };

        SalesPerHourXAxes = new Axis[]
        {
            new Axis
            {
                Labels = xLabels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 12
            }
        };

        SalesPerHourYAxes = new Axis[]
        {
            new Axis
            {
                Labeler = value => value.ToString("C0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 12
            }
        };

        // Revert to Today's categories (PieSeries without legend in XAML)
        var categoryGroups = todaysSales.SelectMany(s => s.Details)
            .Where(d => d.Product != null && d.Product.Category != null)
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
        TodayCategorySeries = pieSeries.ToArray();

        // Today's top products (Horizontal Bar Chart / RowSeries)
        var topProductsToday = todaysSales.SelectMany(s => s.Details)
            .Where(d => d.Product != null)
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

        TodayTopProducts.Clear();
        foreach (var p in topProductsToday)
        {
            TodayTopProducts.Add(p);
        }

        var productValues = new double[topProductsToday.Count];
        var productLabels = new string[topProductsToday.Count];

        for (int i = 0; i < topProductsToday.Count; i++)
        {
            productValues[i] = (double)topProductsToday[i].QuantitySold;
            productLabels[i] = topProductsToday[i].ProductName;
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
                Labeler = value => value.ToString("0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#495057")),
                TextSize = 10
            }
        };

        TopProductsYAxes = new Axis[]
        {
            new Axis
            {
                Labels = productLabels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#495057")),
                TextSize = 10
            }
        };

        // Query today's unpaid sales
        var todaysUnpaidSales = _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Date >= today && !s.IsPaid)
            .ToList();

        HasUnpaidSalesToday = todaysUnpaidSales.Count > 0;

        if (HasUnpaidSalesToday)
        {
            var unpaidSalesData = new double[hours.Length];
            var unpaidXLabels = new string[hours.Length];

            for (int i = 0; i < hours.Length; i++)
            {
                var h = hours[i];
                unpaidSalesData[i] = (double)todaysUnpaidSales
                    .Where(s => s.Date.Hour >= h && s.Date.Hour < h + 2)
                    .Sum(s => s.Total);
                unpaidXLabels[i] = $"{h:D2}:00";
            }

            UnpaidSalesSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = unpaidSalesData,
                    Name = "Fiado",
                    Fill = new SolidColorPaint(SKColor.Parse("#1AF15BB5")), // Soft transparent red
                    Stroke = new SolidColorPaint(SKColor.Parse("#F15BB5")) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#F15BB5")),
                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 }
                }
            };

            UnpaidSalesXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = unpaidXLabels,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                    TextSize = 11
                }
            };

            UnpaidSalesYAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("C0"),
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                    TextSize = 11
                }
            };
        }
        else
        {
            UnpaidSalesSeries = Array.Empty<ISeries>();
            UnpaidSalesXAxes = Array.Empty<Axis>();
            UnpaidSalesYAxes = Array.Empty<Axis>();
        }
    }
}
