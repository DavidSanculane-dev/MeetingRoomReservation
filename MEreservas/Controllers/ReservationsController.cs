
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
        private readonly MasterPasswordService _masterPassword;

        public ReservationsController(AppDbContext db, ReservationService service, AppDbContext context, MasterPasswordService masterPassword)
        {
            _db = db; _service = service; _ctx = context;
            _masterPassword = masterPassword;
        }


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

        [HttpGet]
        public IActionResult MasterPasswordPrompt(string actionType, int? id)
        {
            ViewBag.ActionType = actionType;
            ViewBag.ReservationId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MasterPasswordPrompt(string actionType, int reservationId, string masterPassword)
        {
            const string MASTER_PASSWORD = "SenhaSuperSecreta!!";

            // Senha inválida
            if (masterPassword != MASTER_PASSWORD)
            {
                ViewBag.Error = "Senha incorreta!";            // <- Nome correto para a View
                ViewBag.ActionType = actionType;
                ViewBag.ReservationId = reservationId;
                return View();                                 // <- Devolve a mesma View
            }

            // Senha correta -> ativa bypass
            TempData["AdminBypass"] = true;

            // Redirecionamento correto
            if (actionType == "edit")
                return RedirectToAction("Edit", new { id = reservationId });

            if (actionType == "delete")
                return RedirectToAction("Delete", new { id = reservationId });

            return BadRequest("Ação inválida");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomId,Start,End,Requester,Subject")] Reservation r)
        {
            // 1) Preenche o dono ANTES de validar
            r.CreatedBy = User.Identity?.Name ?? "unknown";

            // 2) Remove eventual erro do ModelState para CreatedBy (porque não vem do form)
            if (ModelState.ContainsKey(nameof(Reservation.CreatedBy)))
                ModelState.Remove(nameof(Reservation.CreatedBy));

            // 3) Regras de negócio
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

            string currentUser = User.Identity?.Name;

            bool adminBypass = TempData.ContainsKey("AdminBypass") && (bool)TempData["AdminBypass"];

            if (reserva.CreatedBy == currentUser || adminBypass)
            {
                TempData.Keep("AdminBypass");

                ViewBag.Rooms = new SelectList(
                    _db.Rooms.AsNoTracking().OrderBy(r => r.Name),
                    "Id", "Name", reserva.RoomId);

                return View(reserva);
            }

            TempData["ReservationId"] = id;

            return RedirectToAction("MasterPasswordPrompt", new { actionType = "edit", id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,[Bind("Id,RoomId,Start,End,Requester,Subject")] Reservation formModel,string HoraInicio, string HoraFim)
        {
            if (id != formModel.Id) return BadRequest();

            var entity = await _db.Reservations.FindAsync(id);
            if (entity == null) return NotFound();

            string? currentUser = User.Identity?.Name;
            bool adminBypass = TempData.ContainsKey("AdminBypass") && (bool)TempData["AdminBypass"];

            // Bloquear edição se não for dono nem bypass
            if (entity.CreatedBy != currentUser && !adminBypass)
            {
                TempData["ReservationId"] = id;
                return RedirectToAction("MasterPasswordPrompt", new { actionType = "edit", id });
            }

            TempData.Keep("AdminBypass");

            ViewBag.Rooms = new SelectList(
                _db.Rooms.AsNoTracking().OrderBy(r => r.Name),
                "Id", "Name", formModel.RoomId);

            // Validar horas
            if (!DateTime.TryParse(HoraInicio, out var startTmp) ||
                !DateTime.TryParse(HoraFim, out var endTmp))
            {
                ModelState.AddModelError(string.Empty, "Horas inválidas.");
                return View(formModel);
            }

            var date = formModel.Start.Date;
            var newStart = date.Add(startTmp.TimeOfDay);
            var newEnd = date.Add(endTmp.TimeOfDay);

            if (newEnd <= newStart)
            {
                ModelState.AddModelError(string.Empty,
                    "A hora de fim deve ser posterior à hora de início.");
                return View(formModel);
            }

            // Conflitos de reserva
            bool conflict = await _db.Reservations
                .AnyAsync(r => r.RoomId == formModel.RoomId &&
                               r.Id != formModel.Id &&
                               ((newStart < r.End) && (newEnd > r.Start)));

            if (conflict)
            {
                ModelState.AddModelError(string.Empty,
                    "Conflito: já existe uma reserva neste intervalo.");
                return View(formModel);
            }

            // Atualizar reserva
            entity.RoomId = formModel.RoomId;
            entity.Start = newStart;
            entity.End = newEnd;
            entity.Requester = formModel.Requester?.Trim();
            entity.Subject = formModel.Subject?.Trim();

            await _db.SaveChangesAsync();

            TempData.Remove("AdminBypass");

            return RedirectToAction(nameof(Index));
        }

        // ---------------------
        // APAGAR RESERVA (CONFIRMAÇÃO)
        // ---------------------

        // GET: Reservations/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var reservation = await _db.Reservations
                .Include(r => r.Room)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            string currentUser = User.Identity?.Name;

            bool adminBypass = TempData.ContainsKey("AdminBypass") && (bool)TempData["AdminBypass"];

            if (reservation.CreatedBy == currentUser || adminBypass)
            {
                TempData.Keep("AdminBypass");
                return View(reservation);
            }

            TempData["ReservationId"] = id;
            return RedirectToAction("MasterPasswordPrompt", new { actionType = "delete" });
        }

        // POST: Reservations/DeleteConfirmed (View usa asp-action="DeleteConfirmed")
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation == null)
            {
                TempData["Warning"] = "A reserva já não existe.";
                return RedirectToAction(nameof(Index));
            }

            string currentUser = User.Identity?.Name;
            bool adminBypass = TempData.ContainsKey("AdminBypass") && (bool)TempData["AdminBypass"];

            if (reservation.CreatedBy != currentUser && !adminBypass)
            {
                TempData["ReservationId"] = id;
                return RedirectToAction("MasterPasswordPrompt", new { actionType = "delete" });
            }

            try
            {
                _db.Reservations.Remove(reservation);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Reserva eliminada com sucesso.";
            }
            catch
            {
                TempData["Error"] = "Erro ao eliminar a reserva.";
            }

            TempData.Remove("AdminBypass");

            return RedirectToAction(nameof(Index));
        }

    }
}
