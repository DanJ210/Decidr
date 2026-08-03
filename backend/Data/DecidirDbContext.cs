using backend.Data.Entities;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class DecidirDbContext : DbContext
{
    public DecidirDbContext(DbContextOptions<DecidirDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CaseEntity> Cases => Set<CaseEntity>();
    public DbSet<CaseVoteEntity> CaseVotes => Set<CaseVoteEntity>();
    public DbSet<CaseCommentEntity> CaseComments => Set<CaseCommentEntity>();
    public DbSet<CaseEvidenceEntity> CaseEvidence => Set<CaseEvidenceEntity>();
    public DbSet<UserRewardEntity> UserRewards => Set<UserRewardEntity>();
    public DbSet<FriendRequestEntity> FriendRequests => Set<FriendRequestEntity>();
    public DbSet<RewardBadgeEntity> RewardBadges => Set<RewardBadgeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.UserName).IsRequired().HasMaxLength(64);
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(128);
            e.Property(u => u.Role).HasConversion<string>();
            e.HasIndex(u => u.UserName).IsUnique();
        });

        modelBuilder.Entity<CaseEntity>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).IsRequired().HasMaxLength(256);
            e.Property(c => c.Category).IsRequired().HasMaxLength(64);
            e.Property(c => c.Summary).IsRequired().HasMaxLength(1024);
            e.Property(c => c.SideAUserName).IsRequired().HasMaxLength(64);
            e.Property(c => c.SideAClaim).IsRequired().HasMaxLength(2048);
            e.Property(c => c.SideBUserName).HasMaxLength(64);
            e.Property(c => c.SideBClaim).HasMaxLength(2048);
            e.Property(c => c.Status).HasConversion<string>();
            e.Property(c => c.WinnerSide).HasConversion<string?>();

            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(c => c.SideAUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(c => c.SideBUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(c => c.InvitedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(c => new { c.Status, c.CreatedAtUtc });
            e.HasIndex(c => new { c.InvitedUserId, c.Status });
        });

        modelBuilder.Entity<CaseVoteEntity>(e =>
        {
            e.HasKey(v => new { v.CaseId, v.UserId });
            e.Property(v => v.Side).HasConversion<string>();

            e.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(v => v.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CaseCommentEntity>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.UserName).IsRequired().HasMaxLength(64);
            e.Property(c => c.Message).IsRequired().HasMaxLength(1024);

            e.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(c => c.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(c => new { c.CaseId, c.CreatedAtUtc });
        });

        modelBuilder.Entity<CaseEvidenceEntity>(e =>
        {
            e.HasKey(item => item.Id);
            e.Property(item => item.Side).HasConversion<string>();
            e.Property(item => item.AddedByUserName).IsRequired().HasMaxLength(64);
            e.Property(item => item.Type).HasConversion<string>();
            e.Property(item => item.Title).IsRequired().HasMaxLength(160);
            e.Property(item => item.ResourceUrl).IsRequired().HasMaxLength(2048);
            e.Property(item => item.MimeType).IsRequired().HasMaxLength(128);

            e.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(item => item.AddedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(item => new { item.CaseId, item.Side, item.CreatedAtUtc });
        });

        modelBuilder.Entity<UserRewardEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.BadgeCode).IsRequired().HasMaxLength(64);
            e.Property(r => r.SourceType).IsRequired().HasMaxLength(64);
            e.Property(r => r.Reason).IsRequired().HasMaxLength(512);
            // Prevent duplicate awards for the same badge on the same source
            e.HasIndex(r => new { r.UserId, r.BadgeCode, r.SourceType, r.SourceId }).IsUnique();

            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RewardBadgeEntity>()
                .WithMany()
                .HasForeignKey(r => r.BadgeCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FriendRequestEntity>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Status).HasConversion<string>();

            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(f => f.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(f => f.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(f => new { f.ToUserId, f.Status });
            e.HasIndex(f => new { f.FromUserId, f.Status });
        });

        modelBuilder.Entity<RewardBadgeEntity>(e =>
        {
            e.HasKey(b => b.Code);
            e.Property(b => b.Code).HasMaxLength(64);
            e.Property(b => b.Label).IsRequired().HasMaxLength(128);
            e.Property(b => b.IconKey).IsRequired().HasMaxLength(64);
            e.Property(b => b.Tier).IsRequired().HasMaxLength(32);
            e.Property(b => b.Description).IsRequired().HasMaxLength(512);

            e.HasData(
                new RewardBadgeEntity { Code = "VOTE_PARTICIPATION", Label = "Community Juror", IconKey = "jury", Tier = "Bronze", Description = "Awarded for participating in community voting." },
                new RewardBadgeEntity { Code = "VOTE_WINNER_MATCH", Label = "Sharp Eye", IconKey = "target", Tier = "Silver", Description = "Awarded when your vote matches the winning side." },
                new RewardBadgeEntity { Code = "POST_PARTICIPATION", Label = "Case Contributor", IconKey = "quill", Tier = "Bronze", Description = "Awarded for posting a side in a case." },
                new RewardBadgeEntity { Code = "CASE_VICTOR", Label = "Court Victor", IconKey = "crown", Tier = "Gold", Description = "Awarded to the winning side poster when a case is closed." }
            );
        });
    }
}
