using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SaleFiscalDocumentConfiguration : IEntityTypeConfiguration<SaleFiscalDocument>
{
    public void Configure(EntityTypeBuilder<SaleFiscalDocument> builder)
    {
        builder.ToTable("SaleFiscalDocuments");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .HasConversion(id => id.Value, value => new SaleFiscalDocumentId(value))
            .ValueGeneratedNever();

        builder.Property(document => document.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(document => document.SaleId)
            .HasConversion(id => id.Value, value => new SaleId(value))
            .IsRequired();

        builder.Property(document => document.Kind).HasConversion<int>().IsRequired();
        builder.Property(document => document.Sequence).IsRequired();
        builder.Property(document => document.Status).HasConversion<int>().IsRequired();

        builder.Property(document => document.FiscalDocumentId).IsRequired(false);

        builder.Property(document => document.ReversedDocumentId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new SaleFiscalDocumentId(value.Value) : null)
            .IsRequired(false);
        builder.Property(document => document.DocumentType).HasMaxLength(30).IsRequired(false);
        builder.Property(document => document.PointOfSale).IsRequired(false);
        builder.Property(document => document.Number).IsRequired(false);
        builder.Property(document => document.AuthorizationCode).HasMaxLength(30).IsRequired(false);
        builder.Property(document => document.AuthorizationExpiry).IsRequired(false);
        builder.Property(document => document.QrUrl).HasMaxLength(1000).IsRequired(false);
        builder.Property(document => document.RejectionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(document => document.IssuedAt).IsRequired(false);
        builder.Property(document => document.CreatedAt).IsRequired();
        builder.Property(document => document.UpdatedAt).IsRequired(false);

        // Único sobre la clave de idempotencia: dos pedidos concurrentes del mismo intento chocan
        // acá y no llegan a emitir dos comprobantes. NO incluye "una factura por venta": eso ya no
        // es cierto (hay reintentos y re-facturación) y la regla real vive en
        // SaleFiscalDocumentRules.LiveInvoice.
        builder.HasIndex(document => new { document.SaleId, document.Kind, document.Sequence }).IsUnique();
        // El callback llega con el id del servicio fiscal: sin índice es un scan.
        builder.HasIndex(document => document.FiscalDocumentId);
        builder.HasIndex(document => document.CompanyId);
        builder.HasIndex(document => document.ReversedDocumentId);

        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(document => document.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
