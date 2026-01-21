
using MEreservas.Services;
using Microsoft.AspNetCore.Mvc;

namespace MEreservas.Controllers.Api
{
    [ApiController]
    public class ReservationsApiController : ControllerBase
    {
        private readonly ReservationService _service;
        public ReservationsApiController(ReservationService service) => _service = service;

        // GET /api/reservations/events?start=...&end=...&roomId=2

        [HttpGet("/api/reservations/events")]
        public async Task<IActionResult> GetEvents([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] int? roomId)
        {
            if (start == default || end == default || start >= end)
                return BadRequest("Parâmetros 'start' e 'end' são obrigatórios e 'start' deve ser anterior a 'end'.");

            var data = await _service.GetByRangeAsync(start, end, roomId);

            static string ColorForRoom(int rid)
            {
                string[] colors = { "#0d6efd", "#6610f2", "#6f42c1", "#d63384", "#dc3545", "#fd7e14", "#ffc107", "#198754", "#20c997", "#0dcaf0" };
                return colors[(rid - 1) % colors.Length];
            }

            var events = data.Select(r => new {
                id = r.Id,
                title = r.Subject,
                start = r.Start,
                end = r.End,
                roomId = r.RoomId,
                roomName = r.Room?.Name ?? $"Sala {r.RoomId}",
                color = r.Room?.Color ?? ColorForRoom(r.RoomId)
            });

            return Ok(events);
        }
    }
}

