
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MEreservas.Data;
using MEreservas.Models;
using MEreservas.Services;


namespace MEreservas.Controllers
{
    public class ReservationsController : Controller
    {

        private readonly AppDbContext _db;
        private readonly ReservationService _service;
        private readonly AppDbContext _ctx;

        public ReservationsController(AppDbContext db, ReservationService service, AppDbContext context)
        {
            _db = db; _service = service; _ctx = context;
        }

        //public IActionResult Index()
        //{
        //    ViewBag.Rooms = new SelectList(_db.Rooms.AsNoTracking().ToList(), "Id", "Name");
        //    return View();
        //}


        public async Task<IActionResult> Index()
        {
            var rooms = await _ctx.Rooms
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.Name
                })
                .ToListAsync();

            ViewBag.Rooms = rooms;
            return View();
        }



        public IActionResult About()
        {
            return View();
        }


        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Rooms = new SelectList(_db.Rooms.AsNoTracking().ToList(), "Id", "Name");
            return View(new Reservation { Start = DateTime.Today.AddHours(9), End = DateTime.Today.AddHours(10) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomId,Start,End,Requester,Subject")] Reservation r)
        {
            if (r.End <= r.Start)
                ModelState.AddModelError(nameof(r.End), "Hora fim deve ser maior que a hora início.");

            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = new SelectList(_db.Rooms.AsNoTracking().ToList(), "Id", "Name", r.RoomId);
                return View(r);
            }

            try
            {
                await _service.CreateAsync(r);
                TempData["ok"] = "Reserva criada com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.Rooms = new SelectList(_db.Rooms.AsNoTracking().ToList(), "Id", "Name", r.RoomId);
                return View(r);
            }
        }


        // ---------------------
        // EDITAR RESERVA (GET)
        // ---------------------
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reserva = await _db.Reservations.FindAsync(id);

            if (reserva == null)
                return NotFound();

            ViewBag.Rooms = new SelectList(_db.Rooms.ToList(), "Id", "Name", reserva.RoomId);

            return View(reserva);
        }

        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RoomId,Start,End,Requester,Subject")] Reservation formModel,
                                              string HoraInicio, string HoraFim)
        {
            if (id != formModel.Id) return BadRequest();

            ViewBag.Rooms = new SelectList(_ctx.Rooms.AsNoTracking().OrderBy(r => r.Name), "Id", "Name", formModel.RoomId);

            if (!DateTime.TryParse(HoraInicio, out var startTimeTmp) ||
                !DateTime.TryParse(HoraFim, out var endTimeTmp))
            {
                ModelState.AddModelError(string.Empty, "Horas inválidas.");
                return View(formModel);
            }

            // Usa apenas a Data de Start do form para compor Start/End finais
            var date = formModel.Start.Date;
            var newStart = date.Add(startTimeTmp.TimeOfDay);
            var newEnd = date.Add(endTimeTmp.TimeOfDay);

            if (newEnd <= newStart)
            {
                ModelState.AddModelError(string.Empty, "A hora de fim deve ser posterior à hora de início.");
                return View(formModel);
            }

            // Conflitos
            bool conflict = await _ctx.Reservations
                .AnyAsync(r => r.RoomId == formModel.RoomId && r.Id != formModel.Id &&
                               ((newStart < r.End) && (newEnd > r.Start)));
            if (conflict)
            {
                ModelState.AddModelError(string.Empty, "Conflito: já existe uma reserva no intervalo selecionado para esta sala.");
                return View(formModel);
            }

            var entity = await _ctx.Reservations.FindAsync(id);
            if (entity == null) return NotFound();

            entity.RoomId = formModel.RoomId;
            entity.Start = newStart;
            entity.End = newEnd;
            entity.Requester = formModel.Requester?.Trim();
            entity.Subject = formModel.Subject?.Trim();

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ---------------------
        // APAGAR RESERVA (CONFIRMAÇÃO)
        // ---------------------

        // GET: Reservations/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            // Carrega a reserva com a Sala para mostrar na View
            var reservation = await _ctx.Reservations
                .Include(r => r.Room)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }

        // POST: Reservations/DeleteConfirmed (View usa asp-action="DeleteConfirmed")
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _ctx.Reservations.FindAsync(id);
            if (reservation == null)
            {
                // Já foi removido ou não existe
                TempData["Warning"] = "A reserva já não existe.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _ctx.Reservations.Remove(reservation);
                await _ctx.SaveChangesAsync();
                TempData["Success"] = "Reserva eliminada com sucesso.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "Ocorreu um erro de concorrência ao eliminar a reserva. Tente novamente.";
            }
            catch (Exception)
            {
                TempData["Error"] = "Ocorreu um erro ao eliminar a reserva.";
            }

            return RedirectToAction(nameof(Index));
        }



    }
}
