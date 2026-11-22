using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<CharacterGeneratedArtwork> CharacterGeneratedArtwork => Set<CharacterGeneratedArtwork>();

    private static void ConfigureCharacterGeneratedArtwork(ModelBuilder model)
    {
        model.Entity<CharacterGeneratedArtwork>(artwork =>
        {
            artwork.ToTable("character_generated_artwork", table => table.HasCheckConstraint(
                "CK_character_generated_artwork_source",
                """
                (CASE WHEN "CollectionId" IS NULL THEN 0 ELSE 1 END)
                + (CASE WHEN "NotionPageId" IS NULL THEN 0 ELSE 1 END)
                + (CASE WHEN "NotebookId" IS NULL THEN 0 ELSE 1 END)
                + (CASE WHEN "NotebookPageId" IS NULL THEN 0 ELSE 1 END) = 1
                """.ReplaceLineEndings("\n")));
            artwork.HasKey(item => item.Id);
            artwork.Property(item => item.Id).ValueGeneratedNever();
            artwork.Property(item => item.CharacterSlug).HasMaxLength(64).IsRequired();
            artwork.Property(item => item.Kind).HasMaxLength(12).IsRequired();
            artwork.Property(item => item.SourceKind).HasMaxLength(16).IsRequired();
            artwork.HasIndex(item => new { item.CharacterSlug, item.SourceKind, item.OriginalAssetId }).IsUnique();
            artwork.HasIndex(item => new { item.CharacterSlug, item.CreatedAtUtc, item.Id });
            artwork.HasIndex(item => new { item.CreatedAtUtc, item.Id });
            artwork.HasOne<NotionCollection>().WithMany().HasForeignKey(item => item.CollectionId).OnDelete(DeleteBehavior.Cascade);
            artwork.HasOne<NotionPage>().WithMany().HasForeignKey(item => item.NotionPageId).OnDelete(DeleteBehavior.Cascade);
            artwork.HasOne<Notebook>().WithMany().HasForeignKey(item => item.NotebookId).OnDelete(DeleteBehavior.Cascade);
            artwork.HasOne<NotebookDocument>().WithMany().HasForeignKey(item => item.NotebookPageId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
