using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MEreservas.Models
{
    public class Reservation
    {

        public int Id { get; set; }

        [Required]
        public int RoomId { get; set; }
        public Room? Room { get; set; }

        [Required]
        public DateTime Start { get; set; }   // Data + Hora Início
        [Required]
        public DateTime End { get; set; }     // Data + Hora Fim

        [Required, StringLength(100)]
        public string Requester { get; set; } = "";

        [StringLength(200)]
        public string Subject { get; set; } = ""; // Motivo

    }
}
