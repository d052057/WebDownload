using Microsoft.EntityFrameworkCore;
using WebDownloade.Server.Models;
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
    public virtual DbSet<Rpm> Rpms { get; set; }

    public virtual DbSet<RpmTrack> RpmTracks { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaFolder>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK_folder");

            entity.ToTable("MediaFolder");

            entity.HasIndex(e => e.ParentFolderId, "IX_folder_parentFolderId");

            entity.HasIndex(e => new { e.MenuId, e.ParentFolderId, e.Name }, "UX_folder_parent_name").IsUnique();

            entity.Property(e => e.RecordId)
                .HasDefaultValueSql("(newid())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_folder_recordId")
                .HasColumnName("recordId");
            entity.Property(e => e.CoverImagePath)
                .HasMaxLength(500)
                .UseCollation("Latin1_General_100_BIN2")
                .HasColumnName("coverImagePath");
            entity.Property(e => e.Datetime)
                .HasDefaultValueSql("(getdate())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_folder_datetime")
                .HasColumnType("datetime")
                .HasColumnName("datetime");
            entity.Property(e => e.MenuId).HasColumnName("menuId");
            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .UseCollation("Latin1_General_100_BIN2")
                .HasColumnName("name");
            entity.Property(e => e.ParentFolderId).HasColumnName("parentFolderId");
            entity.Property(e => e.RootPath)
                .HasMaxLength(500)
                .HasColumnName("rootPath");

            entity.HasOne(d => d.Menu).WithMany(p => p.MediaFolders)
                .HasForeignKey(d => d.MenuId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MediaFolder_MediaMenu");

            entity.HasOne(d => d.ParentFolder).WithMany(p => p.InverseParentFolder)
                .HasForeignKey(d => d.ParentFolderId)
                .HasConstraintName("FK_MediaFolder_MediaFolder_Parent");
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
        modelBuilder.Entity<Rpm>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK_rpm");

            entity.ToTable("Rpm");

            entity.Property(e => e.RecordId)
                .HasDefaultValueSql("(newid())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_rpm_recordId")
                .HasColumnName("recordId");
            entity.Property(e => e.Artist)
                .HasMaxLength(250)
                .HasColumnName("artist");
            entity.Property(e => e.AudioType)
                .HasMaxLength(50)
                .HasColumnName("audioType");
            entity.Property(e => e.DateTime)
                .HasDefaultValueSql("(getdate())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_rpm_dateTime")
                .HasColumnType("datetime")
                .HasColumnName("dateTime");
            entity.Property(e => e.Title)
                .HasMaxLength(250)
                .HasColumnName("title");
        });

        modelBuilder.Entity<RpmTrack>(entity =>
        {
            entity.HasKey(e => e.RecordId);

            entity.ToTable("RpmTrack");

            entity.Property(e => e.RecordId)
                .HasDefaultValueSql("(newid())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_RpmTrack_recordId")
                .HasColumnName("recordId");
            entity.Property(e => e.Artist)
                .HasMaxLength(250)
                .HasColumnName("artist");
            entity.Property(e => e.DateTime)
                .HasDefaultValueSql("(getdate())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_RpmTrack_dateTime")
                .HasColumnType("datetime")
                .HasColumnName("dateTime");
            entity.Property(e => e.DurationSeconds).HasColumnName("durationSeconds");
            entity.Property(e => e.RpmId).HasColumnName("rpmId");
            entity.Property(e => e.Title)
                .HasMaxLength(250)
                .HasColumnName("title");
            entity.Property(e => e.TrackNumber).HasColumnName("trackNumber");

            entity.HasOne(d => d.Rpm).WithMany(p => p.RpmTracks)
                .HasForeignKey(d => d.RpmId)
                .HasConstraintName("FK_RpmTrack_rpm");
        });
        modelBuilder.Entity<MediaMenu>(entity =>
        {
            entity.HasKey(e => e.RecordId);

            entity.ToTable("MediaMenu");

            entity.Property(e => e.RecordId)
                .HasDefaultValueSql("(newid())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_menu_recordId")
                .HasColumnName("recordId");
            entity.Property(e => e.Datetime)
                .HasDefaultValueSql("(getdate())")
                .HasAnnotation("Relational:DefaultConstraintName", "DF_menu_datetime")
                .HasColumnType("datetime")
                .HasColumnName("datetime");
            entity.Property(e => e.Menu)
                .HasMaxLength(50)
                .HasColumnName("menu");
        });
        OnModelCreatingPartial(modelBuilder);
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

}
