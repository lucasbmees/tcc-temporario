using Microsoft.EntityFrameworkCore;
using TccSharkTank.Application.Abstractions.Persistence;
using TccSharkTank.Domain.Entities;

namespace TccSharkTank.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public UsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<UsuUsuario?> GetByIdAsync(long usuId, CancellationToken cancellationToken)
    {
        return _db.UsuUsuarios
            .Include(u => u.Cargo)
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.Id == usuId, cancellationToken);
    }

    public Task<UsuUsuario?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return _db.UsuUsuarios
            .Include(u => u.Cargo)
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<UsuUsuario?> GetByCpfAsync(string cpf, CancellationToken cancellationToken)
    {
        return _db.UsuUsuarios
            .Include(u => u.Cargo)
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.Cpf == cpf, cancellationToken);
    }

    public Task<UsuUsuario?> GetByTelefoneAsync(string telefone, CancellationToken cancellationToken)
    {
        return _db.UsuUsuarios
            .Include(u => u.Cargo)
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.Telefone == telefone, cancellationToken);
    }

    public Task<List<UsuUsuario>> ListAsync(CancellationToken cancellationToken)
    {
        return _db.UsuUsuarios
            .Include(u => u.Cargo)
            .Include(u => u.Perfil)
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(UsuUsuario usuario, CancellationToken cancellationToken) => _db.UsuUsuarios.AddAsync(usuario, cancellationToken).AsTask();

    public void Update(UsuUsuario usuario) => _db.UsuUsuarios.Update(usuario);
}

public sealed class CargoRepository : ICargoRepository
{
    private readonly AppDbContext _db;

    public CargoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<UsuCargo?> GetByIdAsync(int cargoId, CancellationToken cancellationToken)
        => _db.UsuCargos.FirstOrDefaultAsync(c => c.Id == cargoId, cancellationToken);

    public Task<UsuCargo?> GetByNomeAsync(string nome, CancellationToken cancellationToken)
        => _db.UsuCargos.FirstOrDefaultAsync(c => c.Nome == nome, cancellationToken);

    public Task<List<UsuCargo>> ListAsync(CancellationToken cancellationToken)
        => _db.UsuCargos.OrderBy(c => c.Id).ToListAsync(cancellationToken);
}

public sealed class IdeiaRepository : IIdeiaRepository
{
    private readonly AppDbContext _db;

    public IdeiaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<IdaIdeia?> GetByIdAsync(long idaId, CancellationToken cancellationToken)
    {
        return _db.IdaIdeias
            .Include(i => i.Status)
            .Include(i => i.Categoria)
            .Include(i => i.Info)
            .Include(i => i.Documentos)
            .FirstOrDefaultAsync(i => i.Id == idaId, cancellationToken);
    }

    public Task<List<IdaIdeia>> ListAsync(int? categoriaId, CancellationToken cancellationToken)
    {
        var query = _db.IdaIdeias
            .Include(i => i.Status)
            .Include(i => i.Categoria)
            .Include(i => i.Info)
            .Include(i => i.Documentos)
            .AsQueryable();

        if (categoriaId.HasValue)
        {
            query = query.Where(i => i.CategoriaId == categoriaId.Value);
        }

        return query.OrderByDescending(i => i.Id).ToListAsync(cancellationToken);
    }

    public Task AddAsync(IdaIdeia ideia, CancellationToken cancellationToken) => _db.IdaIdeias.AddAsync(ideia, cancellationToken).AsTask();

    public void Update(IdaIdeia ideia) => _db.IdaIdeias.Update(ideia);
}

public sealed class PropostaRepository : IPropostaRepository
{
    private readonly AppDbContext _db;

    public PropostaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<PrpProposta?> GetByIdAsync(long prpId, CancellationToken cancellationToken)
    {
        return _db.PrpPropostas
            .Include(p => p.Ideia)
            .Include(p => p.Infos).ThenInclude(i => i.Aceite)
            .FirstOrDefaultAsync(p => p.Id == prpId, cancellationToken);
    }

    public Task<List<PrpProposta>> ListByUsuarioAsync(long usuarioId, CancellationToken cancellationToken)
    {
        return _db.PrpPropostas
            .Include(p => p.Ideia)
            .Include(p => p.Infos).ThenInclude(i => i.Aceite)
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    // ← NOVO: lista propostas de uma ideia específica (para o empreendedor)
    public Task<List<PrpProposta>> ListByIdeiaAsync(long ideiaId, CancellationToken cancellationToken)
    {
        return _db.PrpPropostas
            .Include(p => p.Ideia)
            .Include(p => p.Infos).ThenInclude(i => i.Aceite)
            .Where(p => p.IdeiaId == ideiaId && p.Status)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(PrpProposta proposta, CancellationToken cancellationToken) => _db.PrpPropostas.AddAsync(proposta, cancellationToken).AsTask();

    public void Update(PrpProposta proposta) => _db.PrpPropostas.Update(proposta);
}

public sealed class NotificacaoRepository : INotificacaoRepository
{
    private readonly AppDbContext _db;

    public NotificacaoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<NtfNotificacao?> GetByIdAsync(long ntfId, CancellationToken cancellationToken)
    {
        return _db.NtfNotificacoes
            .Include(n => n.Tipo)
            .FirstOrDefaultAsync(n => n.Id == ntfId, cancellationToken);
    }

    public Task<List<NtfNotificacao>> ListByUsuarioAsync(long usuarioId, CancellationToken cancellationToken)
    {
        return _db.NtfNotificacoes
            .Include(n => n.Tipo)
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CreateDate)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(NtfNotificacao notificacao, CancellationToken cancellationToken) => _db.NtfNotificacoes.AddAsync(notificacao, cancellationToken).AsTask();

    public void Update(NtfNotificacao notificacao) => _db.NtfNotificacoes.Update(notificacao);
}

public sealed class LogRepository : ILogRepository
{
    private readonly AppDbContext _db;

    public LogRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(TrnLog log, CancellationToken cancellationToken) => _db.TrnLogs.AddAsync(log, cancellationToken).AsTask();
}

public sealed class LookupRepository : ILookupRepository
{
    private readonly AppDbContext _db;

    public LookupRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<IdaStatus?> GetIdeiaStatusByIdAsync(int statusId, CancellationToken cancellationToken)
        => _db.IdaStatuses.FirstOrDefaultAsync(s => s.Id == statusId, cancellationToken);

    public Task<IdaCategoria?> GetIdeiaCategoriaByIdAsync(int categoriaId, CancellationToken cancellationToken)
        => _db.IdaCategorias.FirstOrDefaultAsync(c => c.Id == categoriaId, cancellationToken);

    public Task<PrpAceite?> GetPropostaAceiteByIdAsync(int aceiteId, CancellationToken cancellationToken)
        => _db.PrpAceites.FirstOrDefaultAsync(a => a.Id == aceiteId, cancellationToken);

    public Task<NtfTipo?> GetNotificacaoTipoByIdAsync(int tipoId, CancellationToken cancellationToken)
        => _db.NtfTipos.FirstOrDefaultAsync(t => t.Id == tipoId, cancellationToken);

    public Task<TrnTipo?> GetLogTipoByIdAsync(int tipoId, CancellationToken cancellationToken)
        => _db.TrnTipos.FirstOrDefaultAsync(t => t.Id == tipoId, cancellationToken);
}