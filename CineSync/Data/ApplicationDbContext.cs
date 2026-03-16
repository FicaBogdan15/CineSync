using CineSync.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CineSync.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<Director> Directors { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Actor> Actors { get; set; }
        public DbSet<Cast> Casts { get; set; }
        public DbSet<Review> Reviews { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Platform> Platforms { get; set; }
        public DbSet<AvailableOn> AvailableOnPlatforms { get; set; }

        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        public DbSet<MovieReaction> MovieReactions { get; set; }
        public DbSet<WatchList> WatchLists { get; set; }
        public DbSet<WatchListItem> WatchListItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // =========================
            // UNIQUE CONSTRAINTS
            // =========================

            builder.Entity<Review>()
                .HasIndex(r => new { r.MovieId, r.UserId })
                .IsUnique();

            builder.Entity<MovieReaction>()
                .HasIndex(mr => new { mr.MovieId, mr.UserId })
                .IsUnique();

            builder.Entity<WatchList>()
                .HasIndex(w => w.UserId)
                .IsUnique();

            builder.Entity<WatchListItem>()
                .HasIndex(wli => new { wli.WatchListId, wli.MovieId })
                .IsUnique();

            // =========================
            // REVIEW RELATIONS
            // =========================

            builder.Entity<Review>()
                .HasOne(r => r.Movie)
                .WithMany(m => m.Reviews)
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // MOVIE REACTION RELATIONS
            // =========================

            builder.Entity<MovieReaction>()
                .HasOne(mr => mr.Movie)
                .WithMany(m => m.MovieReactions)
                .HasForeignKey(mr => mr.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MovieReaction>()
                .HasOne(mr => mr.User)
                .WithMany(u => u.MovieReactions)
                .HasForeignKey(mr => mr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // WATCHLIST RELATIONS
            // =========================

            builder.Entity<WatchList>()
                .HasOne(w => w.User)
                .WithOne(u => u.WatchList)
                .HasForeignKey<WatchList>(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WatchListItem>()
                .HasOne(wli => wli.WatchList)
                .WithMany(w => w.Items)
                .HasForeignKey(wli => wli.WatchListId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WatchListItem>()
                .HasOne(wli => wli.Movie)
                .WithMany(m => m.WatchListItems)
                .HasForeignKey(wli => wli.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}