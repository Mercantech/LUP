using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<NotebookCoverAsset> NotebookCoverAssets => Set<NotebookCoverAsset>();

    internal Task<int> DeleteUnreferencedNotionCoversAsync(Guid pageId, CancellationToken ct) =>
        NotebookCoverAssets.Where(asset => asset.NotionPageId == pageId && !NotionPages.Any(page =>
            page.Id == pageId && (page.CoverId == asset.Id || page.PublishedCoverId == asset.Id))).ExecuteDeleteAsync(ct);

    private static void ConfigureNotebookCovers(ModelBuilder model)
    {
        model.Entity<NotebookCoverAsset>(entity =>
        {
            entity.ToTable("notebook_cover_assets", table => table.HasCheckConstraint("CK_notebook_cover_assets_owner",
                """
                (CASE WHEN "CollectionId" IS NULL THEN 0 ELSE 1 END)
                + (CASE WHEN "NotionPageId" IS NULL THEN 0 ELSE 1 END)
                + (CASE WHEN "NotebookId" IS NULL THEN 0 ELSE 1 END)
                + (CASE WHEN "NotebookPageId" IS NULL THEN 0 ELSE 1 END) = 1
                """.ReplaceLineEndings("\n")));
            entity.HasKey(asset => asset.Id);
            entity.Property(asset => asset.Id).ValueGeneratedNever();
            entity.Property(asset => asset.PositionY).HasDefaultValue(Mercantec.Tech.Contracts.NotebookCoverRules.DefaultPositionY)
                .ValueGeneratedNever();
            entity.HasOne<NotionCollection>().WithMany().HasForeignKey(asset => asset.CollectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<NotionPage>().WithMany().HasForeignKey(asset => asset.NotionPageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Notebook>().WithMany().HasForeignKey(asset => asset.NotebookId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<NotebookDocument>().WithMany().HasForeignKey(asset => asset.NotebookPageId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
