using Microsoft.EntityFrameworkCore;
using lionheart.WellBeing;

namespace lionheart.Data
{
    public class UserContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<WellnessState> WellnessStates { get; set; }
        public UserContext(DbContextOptions<UserContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=lionheart.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserID);

            modelBuilder.Entity<WellnessState>()
                .HasKey(w => w.StateID);

            modelBuilder.Entity<WellnessState>()
                .HasOne<User>()
                .WithMany(u => u.WellnessStates)
                .HasForeignKey(w => w.UserID);
        }
    }
}
