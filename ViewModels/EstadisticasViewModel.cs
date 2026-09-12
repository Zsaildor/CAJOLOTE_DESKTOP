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
using CommunityToolkit.Mvvm.Input;

namespace Cajolote.ViewModels;

public class ProductSaleStat
{
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public bool IsBulk { get; set; }
    public string QuantitySoldDisplay => IsBulk ? $"{QuantitySold:0.000} kg" : $"{QuantitySold:0}";
}

public class PeakHourStat
{
    public string TimeRange { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
}

public class WeekPeriod
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string DisplayText => $"Semana {StartDate:dd/MM} - {EndDate:dd/MM}";
}

public partial class EstadisticasViewModel : ObservableObject
{
    private readonly CajoloteDbContext _dbContext;

    public EstadisticasViewModel(CajoloteDbContext dbContext)
    {
        _dbContext = dbContext;
        LoadStats();
    }

    [ObservableProperty] private decimal _ventasHoy;
    [ObservableProperty] private decimal _ventasSemana;
    [ObservableProperty] private decimal _ventasMes;
    [ObservableProperty] private decimal _ticketPromedio;

    [ObservableProperty] private ISeries[] _ventasPorDiaSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _ventasPorDiaXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _ventasPorDiaYAxes = Array.Empty<Axis>();
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

    // Week navigation properties
    [ObservableProperty] private int _selectedWeekIndex = -1;
    [ObservableProperty] private string _selectedWeekLabel = string.Empty;
    [ObservableProperty] private bool _canGoToPreviousWeek;
    [ObservableProperty] private bool _canGoToNextWeek;

    public List<WeekPeriod> Weeks { get; private set; } = new();

    partial void OnSelectedWeekIndexChanged(int value)
    {
        UpdateWeeklyChart();
    }

    [RelayCommand]
    private void PreviousWeek()
    {
        if (SelectedWeekIndex > 0)
        {
            SelectedWeekIndex--;
        }
    }

    [RelayCommand]
    private void NextWeek()
    {
        if (SelectedWeekIndex < Weeks.Count - 1)
        {
            SelectedWeekIndex++;
        }
    }

    private void LoadStats()
    {
        var today = DateTime.Today;
        
        // Calcular el inicio de la semana (Lunes actual)
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff).Date;
        
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        var sales = _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Category)
            .Where(s => s.IsPaid) // Solo contar como ganancia las ventas y deudas que ya fueron pagadas
            .ToList();

        if (startOfWeek < startOfMonth)
        {
            var histSales = _dbContext.HistoricalSales
                .Include(s => s.Details)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Where(s => s.IsPaid && s.Date >= startOfWeek && s.Date < startOfMonth)
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

            sales = sales.Concat(histSales).ToList();
        }

        VentasHoy = sales.Where(s => s.Date >= today).Sum(s => s.Total);
        VentasSemana = sales.Where(s => s.Date >= startOfWeek).Sum(s => s.Total);
        VentasMes = sales.Where(s => s.Date >= startOfMonth).Sum(s => s.Total);
        
        var totalSalesCount = sales.Count;
        TicketPromedio = totalSalesCount > 0 ? sales.Sum(s => s.Total) / totalSalesCount : 0;

        var productGroups = sales.SelectMany(s => s.Details)
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

        // Generar Horas Pico
        var peakHoursData = sales
            .GroupBy(s => (s.Date.Hour / 2) * 2) // Bloques de 2 horas
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
            peakSalesData[i] = (double)sales
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

        // Generar Gráfica de Pastel para Categorías
        var categoryGroups = sales.SelectMany(s => s.Details)
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

        // Inicializar semanas del mes
        Weeks = GetWeeksForMonth(today);
        var currentWeek = Weeks.FirstOrDefault(w => today >= w.StartDate && today <= w.EndDate) ?? Weeks.LastOrDefault();
        if (currentWeek != null)
        {
            SelectedWeekIndex = Weeks.IndexOf(currentWeek);
        }
        else
        {
            UpdateWeeklyChart();
        }
    }

    private List<WeekPeriod> GetWeeksForMonth(DateTime date)
    {
        var weeks = new List<WeekPeriod>();
        var startOfMonth = new DateTime(date.Year, date.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        // Lunes de la semana que contiene el primer día del mes
        int diff = (7 + (startOfMonth.DayOfWeek - DayOfWeek.Monday)) % 7;
        var currentMonday = startOfMonth.AddDays(-diff);

        while (currentMonday <= endOfMonth)
        {
            weeks.Add(new WeekPeriod
            {
                StartDate = currentMonday,
                EndDate = currentMonday.AddDays(6)
            });
            currentMonday = currentMonday.AddDays(7);
        }

        return weeks;
    }

    private void UpdateWeeklyChart()
    {
        if (SelectedWeekIndex < 0 || SelectedWeekIndex >= Weeks.Count) return;

        var week = Weeks[SelectedWeekIndex];
        SelectedWeekLabel = week.DisplayText;
        CanGoToPreviousWeek = SelectedWeekIndex > 0;
        CanGoToNextWeek = SelectedWeekIndex < Weeks.Count - 1;

        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        // Consultar la tabla de ventas activa
        var endDateExclusive = week.EndDate.AddDays(1);
        var salesForWeek = _dbContext.Sales
            .Where(s => s.IsPaid && s.Date >= week.StartDate && s.Date < endDateExclusive)
            .ToList();

        // Si la semana comenzó en el mes anterior, complementar con registros de la tabla histórica
        if (week.StartDate < startOfMonth)
        {
            var histSalesForWeek = _dbContext.HistoricalSales
                .Where(s => s.IsPaid && s.Date >= week.StartDate && s.Date < startOfMonth)
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

            salesForWeek = salesForWeek.Concat(histSalesForWeek).ToList();
        }

        var daysOfWeek = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
        var salesData = new double[7];
        var XLabels = new string[7];

        for (int i = 0; i < 7; i++)
        {
            var date = week.StartDate.AddDays(i);
            salesData[i] = (double)salesForWeek.Where(s => s.Date.Date == date).Sum(s => s.Total);
            XLabels[i] = $"{daysOfWeek[i]} {date.Day}";
        }

        var columnSeries = new ColumnSeries<double>
        {
            Values = salesData,
            Name = "Ventas",
            Fill = new SolidColorPaint(SKColor.Parse("#9B5DE5")), // fallback
            MaxBarWidth = 40,
            Rx = 4,
            Ry = 4
        };

        columnSeries.PointMeasured += (point) =>
        {
            if (point.Visual is null) return;
            var date = week.StartDate.AddDays(point.Index);
            if (date.Date == DateTime.Today)
            {
                point.Visual.Fill = new SolidColorPaint(SKColor.Parse("#9B5DE5"));
            }
            else
            {
                point.Visual.Fill = new SolidColorPaint(SKColor.Parse("#9B5DE5").WithAlpha(153)); // 60% opacity
            }
        };

        VentasPorDiaSeries = new ISeries[]
        {
            columnSeries
        };

        VentasPorDiaXAxes = new Axis[]
        {
            new Axis
            {
                Labels = XLabels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 12
            }
        };

        VentasPorDiaYAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                Labeler = value => value.ToString("C0"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B5A68")),
                TextSize = 12
            }
        };
    }
}
