using MEreservas.Models;

namespace MEreservas.Data
{
    public class SeedData
    {

        public static void Initialize(AppDbContext db)
        {
            if (!db.Rooms.Any())
            {
                db.Database.EnsureCreated();
            }
            // Nada a fazer: a seed das salas já está no OnModelCreating (HasData).
        }

    }
}
