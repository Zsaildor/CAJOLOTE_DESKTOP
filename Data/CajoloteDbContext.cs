using Microsoft.EntityFrameworkCore;
using Cajolote.Models;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cajolote.Data;

public class CajoloteDbContext : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleDetail> SaleDetails { get; set; } = null!;
    public DbSet<HistoricalSale> HistoricalSales { get; set; } = null!;
    public DbSet<HistoricalSaleDetail> HistoricalSaleDetails { get; set; } = null!;
    public DbSet<StoreProfile> StoreProfiles { get; set; } = null!;
    public DbSet<Note> Notes { get; set; } = null!;
    public DbSet<ManualDebt> ManualDebts { get; set; } = null!;

    public string DbPath { get; }

    public CajoloteDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = System.IO.Path.Join(path, "cajolote.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Simple decimal configuration
        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasColumnType("decimal(18,2)");
            
        /*modelBuilder.Entity<Product>()
            .Property(p => p.Cost)
            .HasColumnType("decimal(18,2)");*/

        modelBuilder.Entity<Sale>()
            .Property(s => s.Total)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<SaleDetail>()
            .Property(sd => sd.UnitPrice)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<HistoricalSale>()
            .Property(s => s.Total)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<HistoricalSaleDetail>()
            .Property(sd => sd.UnitPrice)
            .HasColumnType("decimal(18,2)");
            
        // Barcode index
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Barcode)
            .IsUnique();
    }

    public override int SaveChanges()
    {
        var affectedNoteIds = GetAffectedNoteIds();
        int result = base.SaveChanges();
        if (affectedNoteIds.Count > 0 && RecalculateNotes(affectedNoteIds))
        {
            base.SaveChanges();
        }
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var affectedNoteIds = GetAffectedNoteIds();
        int result = await base.SaveChangesAsync(cancellationToken);
        if (affectedNoteIds.Count > 0 && RecalculateNotes(affectedNoteIds))
        {
            await base.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    private HashSet<int> GetAffectedNoteIds()
    {
        var ids = new HashSet<int>();

        foreach (var entry in ChangeTracker.Entries<Sale>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                if (entry.Entity.NoteId.HasValue)
                {
                    ids.Add(entry.Entity.NoteId.Value);
                }
                if (entry.State == EntityState.Modified)
                {
                    var originalNoteId = entry.OriginalValues.GetValue<int?>("NoteId");
                    if (originalNoteId.HasValue)
                    {
                        ids.Add(originalNoteId.Value);
                    }
                }
            }
        }

        foreach (var entry in ChangeTracker.Entries<ManualDebt>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                ids.Add(entry.Entity.NoteId);
                if (entry.State == EntityState.Modified)
                {
                    var originalNoteId = entry.OriginalValues.GetValue<int>("NoteId");
                    ids.Add(originalNoteId);
                }
            }
        }

        return ids;
    }

    private bool RecalculateNotes(HashSet<int> noteIds)
    {
        bool changed = false;
        foreach (var id in noteIds)
        {
            var note = Notes
                .Include(n => n.Sales)
                .Include(n => n.ManualDebts)
                .FirstOrDefault(n => n.Id == id);
            
            if (note != null)
            {
                decimal correctAmount = note.Sales.Sum(s => s.Total) + note.ManualDebts.Sum(md => md.Amount);
                if (note.Amount != correctAmount)
                {
                    note.Amount = correctAmount;
                    Notes.Update(note);
                    changed = true;
                }
            }
        }
        return changed;
    }
}
