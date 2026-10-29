using Fiap.TechChallenge.Execution.Domain.Aggregates.OrdemProducaoAggregate;
using Fiap.TechChallenge.Execution.Domain.Exceptions;
using Fiap.TechChallenge.Execution.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Repositories;

/// <summary>
/// Implementação PostgreSQL/EF Core do repositório do agregado OrdemProducao com transações ACID e concorrência otimista.
/// </summary>
public class OrdemProducaoRepository : IOrdemProducaoRepository
{
    private readonly AppDbContext _context;

    public OrdemProducaoRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<OrdemProducao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await QueryCompleta()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<OrdemProducao?> ObterPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default)
    {
        return await QueryCompleta()
            .FirstOrDefaultAsync(o => o.OrdemServicoId == ordemServicoId, cancellationToken);
    }

    public async Task<bool> ExistePorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default)
    {
        return await _context.OrdensProducao
            .AsNoTracking()
            .AnyAsync(o => o.OrdemServicoId == ordemServicoId, cancellationToken);
    }

    public async Task<IReadOnlyList<OrdemProducao>> ObterFilaProducaoAsync(
        string? filialId = null,
        StatusOrdemProducao? status = null,
        CancellationToken cancellationToken = default)
    {
        var statusFiltro = status ?? StatusOrdemProducao.NaFila;

        var query = _context.OrdensProducao
            .AsNoTracking()
            .Include(o => o.Diagnostico)
            .Include(o => o.Tarefas)
            .AsSplitQuery()
            .Where(o => o.Status == statusFiltro);

        if (!string.IsNullOrWhiteSpace(filialId))
        {
            var filialNormalizada = filialId.Trim();
            query = query.Where(o => o.FilialId == filialNormalizada);
        }

        return await query
            .OrderBy(o => o.DataLiberacao)
            .ThenBy(o => o.DataAbertura)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrdemProducao>> ObterDiagnosticosPendentesAsync(string? filialId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.OrdensProducao
            .AsNoTracking()
            .Include(o => o.Tarefas)
            .Where(o => o.Status == StatusOrdemProducao.AguardandoDiagnostico);

        if (!string.IsNullOrWhiteSpace(filialId))
        {
            var filialNormalizada = filialId.Trim();
            query = query.Where(o => o.FilialId == filialNormalizada);
        }

        return await query
            .OrderBy(o => o.DataAbertura)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TarefaProducao>> ObterTarefasConcluidasPorPeriodoAsync(
        DateTime? de = null,
        DateTime? ate = null,
        Guid? idMecanico = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TarefasProducao
            .AsNoTracking()
            .Where(t => t.Status == StatusTarefaProducao.Concluida);

        if (de.HasValue)
        {
            var deUtc = de.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(de.Value, DateTimeKind.Utc)
                : de.Value.ToUniversalTime();
            query = query.Where(t => t.DataHoraFim >= deUtc);
        }

        if (ate.HasValue)
        {
            var ateUtc = ate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(ate.Value, DateTimeKind.Utc)
                : ate.Value.ToUniversalTime();
            query = query.Where(t => t.DataHoraFim <= ateUtc);
        }

        if (idMecanico.HasValue && idMecanico.Value != Guid.Empty)
        {
            query = query.Where(t => t.IdMecanicoResponsavel == idMecanico.Value);
        }

        return await query
            .OrderBy(t => t.DataHoraFim)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarAsync(OrdemProducao ordem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        await _context.OrdensProducao.AddAsync(ordem, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarAsync(OrdemProducao ordem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        var entry = _context.Entry(ordem);
        if (entry.State == EntityState.Detached)
        {
            var versaoOriginal = ordem.Versao;
            _context.OrdensProducao.Attach(ordem);
            entry.Property(o => o.Versao).OriginalValue = versaoOriginal;
            ordem.IncrementarVersao();
            entry.State = EntityState.Modified;
        }
        else
        {
            var versaoOriginal = entry.Property(o => o.Versao).OriginalValue;
            if (ordem.Versao == versaoOriginal)
            {
                ordem.IncrementarVersao();
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoConcorrenciaException(ordem.Id, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uk_diagnostico_ordem_producao"
        })
        {
            throw new ConflitoConcorrenciaException(ordem.Id, ex);
        }
    }

    private IQueryable<OrdemProducao> QueryCompleta()
    {
        return _context.OrdensProducao
            .Include(o => o.Diagnostico)
                .ThenInclude(d => d!.ServicosSugeridos)
            .Include(o => o.Diagnostico)
                .ThenInclude(d => d!.PecasSugeridas)
            .Include(o => o.Tarefas)
            .Include(o => o.Historico.OrderBy(h => h.DataHora))
            .AsSplitQuery();
    }
}
