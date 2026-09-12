using Cajolote.Data;
using Cajolote.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Cajolote.Services;

public static class SalesArchiver
{
    public static void ArchiveOldSalesIfNeeded(CajoloteDbContext context)
    {
        var today = DateTime.Today;
        var startOfCurrentMonth = new DateTime(today.Year, today.Month, 1);

        // Only archive paid sales older than the start of the current month
        var salesToArchive = context.Sales
            .Include(s => s.Details)
            .Where(s => s.Date < startOfCurrentMonth && s.IsPaid)
            .ToList();

        if (!salesToArchive.Any())
            return;

        using var transaction = context.Database.BeginTransaction();
        try
        {
            var salesIdsToArchive = salesToArchive.Select(s => s.Id).ToList();
            var existingHistoricalIds = context.HistoricalSales
                .Where(hs => salesIdsToArchive.Contains(hs.Id))
                .Select(hs => hs.Id)
                .ToHashSet();

            foreach (var sale in salesToArchive)
            {
                // Prevent duplicate key conflicts
                if (existingHistoricalIds.Contains(sale.Id))
                    continue;

                var historicalSale = new HistoricalSale
                {
                    Id = sale.Id,
                    Date = sale.Date,
                    Total = sale.Total,
                    IsSynced = sale.IsSynced,
                    IsPaid = sale.IsPaid,
                    NoteId = sale.NoteId
                };
                context.HistoricalSales.Add(historicalSale);

                foreach (var detail in sale.Details)
                {
                    var historicalDetail = new HistoricalSaleDetail
                    {
                        Id = detail.Id,
                        HistoricalSaleId = sale.Id,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        UnitPrice = detail.UnitPrice
                    };
                    context.HistoricalSaleDetails.Add(historicalDetail);
                }
            }

            // Remove from active sales (cascade deletes active SaleDetails)
            context.Sales.RemoveRange(salesToArchive);
            context.SaveChanges();
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }
}
