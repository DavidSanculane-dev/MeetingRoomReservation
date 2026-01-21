
using MEreservas.Data;
using MEreservas.Models;
using Microsoft.EntityFrameworkCore;

namespace MEreservas.Services
{
    public class ReservationService
    {
        private readonly AppDbContext _db;

        public ReservationService(AppDbContext db) => _db = db;

        /// <summary>
        /// Verifica se existe conflito de horário para a mesma sala.
        /// Regra: conflito se (start < EndExistente) e (end > StartExistente).
        /// </summary>
        public async Task<bool> HasConflictAsync(int roomId, DateTime start, DateTime end, int? ignoreId = null)
        {
            // Garantir que o intervalo é válido
            if (end <= start) return true;

            return await _db.Reservations
                .AnyAsync(r => r.RoomId == roomId
                               && (ignoreId == null || r.Id != ignoreId)
                               && start < r.End
                               && end > r.Start);
        }

        /// <summary>
        /// Normaliza as datas garantindo Kind consistente (evita bugs de timezone).
        /// Aqui tratamos como "Unspecified" (horário local do servidor).
        /// </summary>
        private static (DateTime Start, DateTime End) Normalize(DateTime start, DateTime end)
        {
            DateTime NormalizeKind(DateTime dt)
            {
                if (dt.Kind == DateTimeKind.Unspecified) return dt;
                // Remove o Kind para tratar tudo como "local sem offset" internamente
                return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            }

            return (NormalizeKind(start), NormalizeKind(end));
        }

        /// <summary>
        /// Valida dados essenciais de uma reserva.
        /// </summary>
        private static void Validate(Reservation r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.RoomId <= 0) throw new ArgumentException("Sala inválida.", nameof(r.RoomId));
            if (string.IsNullOrWhiteSpace(r.Requester)) throw new ArgumentException("Solicitante é obrigatório.", nameof(r.Requester));
            if (string.IsNullOrWhiteSpace(r.Subject)) throw new ArgumentException("Motivo/Assunto é obrigatório.", nameof(r.Subject));
            if (r.End <= r.Start) throw new ArgumentException("A hora de fim deve ser posterior à hora de início.");
        }

        /// <summary>
        /// Cria uma nova reserva (com verificação de conflito).
        /// </summary>
        public async Task<Reservation> CreateAsync(Reservation r)
        {
            var (s, e) = Normalize(r.Start, r.End);
            r.Start = s;
            r.End = e;

            Validate(r);

            if (await HasConflictAsync(r.RoomId, r.Start, r.End))
                throw new InvalidOperationException("Já existe uma reserva neste intervalo para esta sala.");

            _db.Reservations.Add(r);
            await _db.SaveChangesAsync();
            return r;
        }

        /// <summary>
        /// Atualiza uma reserva existente (com verificação de conflito e existência).
        /// </summary>
        public async Task<Reservation> UpdateAsync(Reservation updated)
        {
            var existing = await _db.Reservations.FirstOrDefaultAsync(x => x.Id == updated.Id);
            if (existing == null) throw new KeyNotFoundException("Reserva não encontrada.");

            var (s, e) = Normalize(updated.Start, updated.End);

            if (e <= s)
                throw new ArgumentException("A hora de fim deve ser posterior à hora de início.");

            // Verifica conflito ignorando a própria reserva
            if (await HasConflictAsync(updated.RoomId, s, e, ignoreId: updated.Id))
                throw new InvalidOperationException("Conflito: já existe uma reserva no intervalo selecionado para esta sala.");

            existing.RoomId = updated.RoomId;
            existing.Start = s;
            existing.End = e;
            existing.Requester = updated.Requester?.Trim();
            existing.Subject = updated.Subject?.Trim();

            await _db.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Elimina uma reserva por Id.
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            var entity = await _db.Reservations.FindAsync(id);
            if (entity == null) return; // idempotente

            _db.Reservations.Remove(entity);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Obtém uma reserva por Id (com Sala).
        /// </summary>
        public async Task<Reservation?> GetByIdAsync(int id)
        {
            return await _db.Reservations
                .Include(x => x.Room)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        /// <summary>
        /// Lista reservas no intervalo visível. Aplica filtro opcional por sala.
        /// </summary>
        public async Task<List<Reservation>> GetByRangeAsync(DateTime start, DateTime end, int? roomId = null)
        {
            if (start == default || end == default || start >= end)
                throw new ArgumentException("Parâmetros 'start' e 'end' são obrigatórios e 'start' deve ser anterior a 'end'.");

            var (s, e) = Normalize(start, end);

            var query = _db.Reservations
                .Include(x => x.Room)
                .AsNoTracking()
                .Where(x => x.Start < e && x.End > s);

            if (roomId.HasValue)
                query = query.Where(x => x.RoomId == roomId.Value);

            return await query
                .OrderBy(x => x.Start)
                .ToListAsync();
        }

        /// <summary>
        /// (Opcional) Lista reservas futuras a partir de hoje, para uma sala específica.
        /// </summary>
        public async Task<List<Reservation>> GetUpcomingByRoomAsync(int roomId, int take = 10)
        {
            var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            return await _db.Reservations
                .Include(x => x.Room)
                .AsNoTracking()
                .Where(x => x.RoomId == roomId && x.End > now)
                .OrderBy(x => x.Start)
                .Take(take)
                .ToListAsync();
        }
    }
}

