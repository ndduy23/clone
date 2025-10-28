using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace BookDb.Models
{
    public class AppDbContext : IdentityDbContext<User>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentPage> DocumentPages { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            base.OnConfiguring(builder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>()
                .HasMany(d => d.Pages)
                .WithOne(p => p.Document)
                .HasForeignKey(p => p.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Bookmark>()
                .HasOne(b => b.DocumentPage)
                .WithOne(p => p.Bookmark)
                .HasForeignKey<Bookmark>(b => b.DocumentPageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}