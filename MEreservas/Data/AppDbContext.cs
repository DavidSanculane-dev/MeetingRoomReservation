using Microsoft.EntityFrameworkCore;
using MEreservas.Models;

namespace MEreservas.Data
{
    public class AppDbContext : DbContext
    {

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Reservation> Reservations => Set<Reservation>();

        [Obsolete]
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Room)
                .WithMany(rm => rm.Reservations)
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Garanta inícios anteriores aos fins
            modelBuilder.Entity<Reservation>()
                .HasCheckConstraint("CK_Reservation_StartEnd", "\"End\" > \"Start\"");

            // Opcional: índice para pesquisas por intervalo
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => new { r.RoomId, r.Start, r.End });

            // Seed de salas
            modelBuilder.Entity<Room>().HasData(
                new Room { Id = 1, Name = "Sala Manuel Mota", Color = "#1976d2" },
                new Room { Id = 2, Name = "Sala António Vieira", Color = "#2e7d32" },
                new Room { Id = 3, Name = "Sala Reunião Manutenção", Color = "#d32f2f" }
            );
        }


    }
}
