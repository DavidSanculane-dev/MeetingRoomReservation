namespace MEreservas.Models
{
    public class Room
    {

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#1976d2"; // Cor para o calendário
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();


    }
}
