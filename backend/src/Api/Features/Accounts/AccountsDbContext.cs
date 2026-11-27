using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfile", table =>
                table.HasCheckConstraint("CK_UserProfile_DisplayName_Length", "char_length(\"DisplayName\") BETWEEN 1 AND 80"));

            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(80).IsRequired();
            entity.Property(profile => profile.Locale).HasMaxLength(8).IsRequired();
            entity.HasIndex(profile => profile.DeletedAt);

            entity
                .HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Favorite>(entity =>
        {
            entity.ToTable("Favorite", table => table.HasCheckConstraint(
                "CK_Favorite_TargetType",
                "\"TargetType\" IN ('neighborhood', 'place')"));

            entity.HasKey(favorite => new { favorite.UserId, favorite.TargetType, favorite.TargetId });
            entity.Property(favorite => favorite.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(favorite => favorite.TargetId).HasMaxLength(64).IsRequired();
            entity.HasIndex(favorite => new { favorite.UserId, favorite.CreatedAt });

            entity
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(favorite => favorite.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
