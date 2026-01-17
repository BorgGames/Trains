using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Trains.Persistence;

public sealed class TrainsDbContext : IdentityDbContext, IDataProtectionKeyContext {
    public TrainsDbContext(DbContextOptions<TrainsDbContext> options) : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<PuzzleEntity> Puzzles => Set<PuzzleEntity>();
    public DbSet<PuzzleVoteEntity> PuzzleVotes => Set<PuzzleVoteEntity>();
    public DbSet<PuzzleSolveEntity> PuzzleSolves => Set<PuzzleSolveEntity>();

    protected override void OnModelCreating(ModelBuilder builder) {
        base.OnModelCreating(builder);

        builder.Entity<PuzzleEntity>(b => {
            b.HasKey(x => x.Id);

            b.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Property(x => x.PuzzleJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.SolutionHistoryJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.ThumbnailSvg).HasColumnType("text").IsRequired();
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.IsPublished);
        });

        builder.Entity<PuzzleVoteEntity>(b => {
            b.HasKey(x => new { x.PuzzleId, x.UserId });
            b.Property(x => x.Difficulty).IsRequired();
            b.Property(x => x.Score).IsRequired();
            b.HasIndex(x => x.PuzzleId);
            b.HasIndex(x => x.UserId);

            b.HasOne<PuzzleEntity>()
                .WithMany()
                .HasForeignKey(x => x.PuzzleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PuzzleSolveEntity>(b => {
            b.HasKey(x => new { x.PuzzleId, x.UserId });
            b.HasIndex(x => x.PuzzleId);
            b.HasIndex(x => x.UserId);

            b.HasOne<PuzzleEntity>()
                .WithMany()
                .HasForeignKey(x => x.PuzzleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
