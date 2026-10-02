using Microsoft.EntityFrameworkCore;
namespace WebDownload.Server.Models;
public partial class DBWebDownload : DbContext
{
    public DBWebDownload(DbContextOptions<DBWebDownload> options)
    : base(options)
    {
    }
    public virtual DbSet<MediaMenu> MediaMenus { get; set; }
    public virtual DbSet<MediaFolder> MediaFolders { get; set; }
    public virtual DbSet<MediaTrack> MediaTracks { get; set; }
    public virtual DbSet<MediaSubtitle> MediaSubtitles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaMenu>(e =>
        {
            e.ToTable("MediaMenu");
            e.HasKey(x => x.RecordId);
            e.Property(x => x.RecordId).HasColumnName("recordId");
            e.Property(x => x.Menu).HasMaxLength(50).HasColumnName("menu");
            e.Property(x => x.Datetime).HasColumnType("datetime").HasColumnName("datetime");
        });

        modelBuilder.Entity<MediaFolder>(e =>
        {
            e.ToTable("MediaFolder");
            e.HasKey(x => x.RecordId);
            e.Property(x => x.RecordId).HasColumnName("recordId");
            e.Property(x => x.MenuId).HasColumnName("menuId");
            e.Property(x => x.ParentFolderId).HasColumnName("parentFolderId");
            e.Property(x => x.Name).HasMaxLength(250).HasColumnName("name");
            e.Property(x => x.RootPath).HasMaxLength(500).HasColumnName("rootPath");
            e.Property(x => x.CoverImagePath).HasMaxLength(500).HasColumnName("coverImagePath");
            e.Property(x => x.Datetime).HasColumnType("datetime").HasColumnName("datetime");

            e.HasOne(d => d.Menu).WithMany(p => p.MediaFolders)
                .HasForeignKey(d => d.MenuId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasOne(d => d.ParentFolder).WithMany(p => p.InverseParentFolder)
                .HasForeignKey(d => d.ParentFolderId);
        });

        modelBuilder.Entity<MediaTrack>(e =>
        {
            e.ToTable("MediaTrack");
            e.HasKey(x => x.RecordId);
            e.Property(x => x.RecordId).HasColumnName("recordId");
            e.Property(x => x.FolderId).HasColumnName("folderId");
            e.Property(x => x.FileName).HasMaxLength(260).HasColumnName("fileName");
            e.Property(x => x.Title).HasMaxLength(250).HasColumnName("title");

            e.HasOne(d => d.Folder).WithMany(p => p.MediaTracks).HasForeignKey(d => d.FolderId);
        });

        modelBuilder.Entity<MediaSubtitle>(e =>
        {
            e.HasKey(x => x.RecordId);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.Label).HasMaxLength(50);
            e.Property(x => x.Language).HasMaxLength(10);

            e.HasOne(d => d.MediaMetaDataRecord).WithMany(p => p.MediaSubtitles)
                .HasForeignKey(d => d.MediaMetaDataRecordId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

}
