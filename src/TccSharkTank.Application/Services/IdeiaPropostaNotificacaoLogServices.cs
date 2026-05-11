using TccSharkTank.Application.Abstractions.Persistence;
using TccSharkTank.Application.Abstractions.System;
using TccSharkTank.Application.Common;
using TccSharkTank.Application.Contracts;
using TccSharkTank.Domain.Entities;

namespace TccSharkTank.Application.Services;

public interface ILogService
{
    Task RegistrarAsync(string tipoNome, long? usuarioId, long? ideiaId, long? propostaId, string descricao, CancellationToken cancellationToken);
}

public interface IIdeiaService
{
    Task<IdeiaDetailsResponse> CadastrarAsync(long usuarioId, CreateIdeiaRequest request, CancellationToken cancellationToken);
    Task<List<IdeiaDetailsResponse>> ListarAsync(int? categoriaId, CancellationToken cancellationToken);
    Task<IdeiaDetailsResponse> DetalhesAsync(long idaId, CancellationToken cancellationToken);
    Task<IdeiaDetailsResponse> EditarAsync(long idaId, long usuarioId, UpdateIdeiaRequest request, CancellationToken cancellationToken);
    Task<IdeiaDetailsResponse> AlterarStatusAsync(long idaId, ChangeIdeiaStatusRequest request, CancellationToken cancellationToken);
    Task<IdeiaDocumentoResponse> UploadDocumentoAsync(long idaId, long usuarioId, Stream pdfStream, string fileName, CancellationToken cancellationToken);
}

public interface IPropostaService
{
    Task<PropostaResponse> EnviarInicialAsync(long ideiaId, long usuarioId, CreatePropostaRequest request, CancellationToken cancellationToken);
    Task<PropostaResponse> ResponderAsync(long propostaId, long usuarioId, ResponderPropostaRequest request, CancellationToken cancellationToken);
    Task<List<PropostaResponse>> ListarMinhasAsync(long usuarioId, CancellationToken cancellationToken);
    Task<PropostaResponse> EncerrarAsync(long propostaId, long usuarioId, CancellationToken cancellationToken);
    Task<List<PropostaResponse>> ListarDaIdeiaAsync(long ideiaId, long donoId, CancellationToken cancellationToken); // ← NOVO
}

public interface INotificacaoService
{
    Task<NotificacaoResponse> DispararAsync(DispararNotificacaoRequest request, CancellationToken cancellationToken);
    Task<List<NotificacaoResponse>> ListarMinhasAsync(long usuarioId, CancellationToken cancellationToken);
    Task<NotificacaoResponse> MarcarLidaAsync(long notificacaoId, long usuarioId, CancellationToken cancellationToken);
}

public sealed class LogService : ILogService
{
    private readonly ILogRepository _logs;
    private readonly ILookupRepository _lookup;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public LogService(ILogRepository logs, ILookupRepository lookup, IUnitOfWork uow, IClock clock)
    {
        _logs = logs;
        _lookup = lookup;
        _uow = uow;
        _clock = clock;
    }

    public async Task RegistrarAsync(string tipoNome, long? usuarioId, long? ideiaId, long? propostaId, string descricao, CancellationToken cancellationToken)
    {
        var tipoId = tipoNome.Trim().ToLowerInvariant() switch
        {
            "cadastro" => 1,
            "edição" => 2,
            "edicao" => 2,
            "proposta" => 3,
            "login" => 4,
            _ => 0
        };

        if (tipoId == 0 || await _lookup.GetLogTipoByIdAsync(tipoId, cancellationToken) is null)
        {
            throw new AppException("Tipo de log inválido.", 400);
        }

        var log = new TrnLog
        {
            TipoId = tipoId,
            UsuarioId = usuarioId,
            IdeiaId = ideiaId,
            PropostaId = propostaId,
            CreateDate = _clock.UtcNow,
            Descricao = descricao
        };

        await _logs.AddAsync(log, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

public sealed class IdeiaService : IIdeiaService
{
    private readonly IIdeiaRepository _ideias;
    private readonly ILookupRepository _lookup;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IFileStorage _fileStorage;
    private readonly ILogService _logs;

    public IdeiaService(IIdeiaRepository ideias, ILookupRepository lookup, IUnitOfWork uow, IClock clock, IFileStorage fileStorage, ILogService logs)
    {
        _ideias = ideias;
        _lookup = lookup;
        _uow = uow;
        _clock = clock;
        _fileStorage = fileStorage;
        _logs = logs;
    }

    public async Task<IdeiaDetailsResponse> CadastrarAsync(long usuarioId, CreateIdeiaRequest request, CancellationToken cancellationToken)
    {
        if (await _lookup.GetIdeiaCategoriaByIdAsync(request.CategoriaId, cancellationToken) is null)
        {
            throw new AppException("Categoria inválida.", 400);
        }

        var ideia = new IdaIdeia
        {
            UsuarioId = usuarioId,
            StatusId = 1,
            MotivoStatus = null,
            CategoriaId = request.CategoriaId,
            Nome = request.Nome.Trim()
        };

        ideia.Info = new IdaInfo
        {
            Id = 0,
            Cnpj = request.Cnpj.Trim(),
            Descricao = request.Descricao,
            LinkVideo = request.LinkVideo,
            Imagem = request.Imagem,
            Fatia = request.Fatia,
            CreateDate = _clock.UtcNow,
            UpdateDate = _clock.UtcNow
        };

        await _ideias.AddAsync(ideia, cancellationToken);
        await _logs.RegistrarAsync("cadastro", usuarioId, ideiaId: null, propostaId: null, descricao: $"Cadastro de ideia {ideia.Nome}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await DetalhesAsync(ideia.Id, cancellationToken);
    }

    public async Task<List<IdeiaDetailsResponse>> ListarAsync(int? categoriaId, CancellationToken cancellationToken)
    {
        var ideias = await _ideias.ListAsync(categoriaId, cancellationToken);
        return ideias.Select(MapIdeia).ToList();
    }

    public async Task<IdeiaDetailsResponse> DetalhesAsync(long idaId, CancellationToken cancellationToken)
    {
        var ideia = await _ideias.GetByIdAsync(idaId, cancellationToken);
        if (ideia is null)
        {
            throw new AppException("Ideia não encontrada.", 404);
        }

        return MapIdeia(ideia);
    }

    public async Task<IdeiaDetailsResponse> EditarAsync(long idaId, long usuarioId, UpdateIdeiaRequest request, CancellationToken cancellationToken)
    {
        var ideia = await _ideias.GetByIdAsync(idaId, cancellationToken);
        if (ideia is null)
        {
            throw new AppException("Ideia não encontrada.", 404);
        }

        if (ideia.UsuarioId != usuarioId)
        {
            throw new AppException("Você não tem permissão para editar esta ideia.", 403);
        }

        if (request.CategoriaId.HasValue)
        {
            if (await _lookup.GetIdeiaCategoriaByIdAsync(request.CategoriaId.Value, cancellationToken) is null)
            {
                throw new AppException("Categoria inválida.", 400);
            }

            ideia.CategoriaId = request.CategoriaId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Nome))
        {
            ideia.Nome = request.Nome.Trim();
        }

        ideia.Info ??= new IdaInfo
        {
            Id = 0,
            Cnpj = request.Cnpj?.Trim() ?? "",
            CreateDate = _clock.UtcNow,
            UpdateDate = _clock.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(request.Cnpj))
        {
            ideia.Info.Cnpj = request.Cnpj.Trim();
        }

        if (request.Descricao is not null)
        {
            ideia.Info.Descricao = request.Descricao;
        }
        if (request.LinkVideo is not null)
        {
            ideia.Info.LinkVideo = request.LinkVideo;
        }
        if (request.Imagem is not null)
        {
            ideia.Info.Imagem = request.Imagem;
        }
        if (request.Fatia.HasValue)
        {
            ideia.Info.Fatia = request.Fatia.Value;
        }

        ideia.Info.UpdateDate = _clock.UtcNow;

        _ideias.Update(ideia);
        await _logs.RegistrarAsync("edição", usuarioId, ideiaId: ideia.Id, propostaId: null, descricao: $"Edição de ideia {ideia.Nome}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await DetalhesAsync(idaId, cancellationToken);
    }

    public async Task<IdeiaDetailsResponse> AlterarStatusAsync(long idaId, ChangeIdeiaStatusRequest request, CancellationToken cancellationToken)
    {
        var ideia = await _ideias.GetByIdAsync(idaId, cancellationToken);
        if (ideia is null)
        {
            throw new AppException("Ideia não encontrada.", 404);
        }

        if (await _lookup.GetIdeiaStatusByIdAsync(request.StatusId, cancellationToken) is null)
        {
            throw new AppException("Status inválido.", 400);
        }

        ideia.StatusId = request.StatusId;
        ideia.MotivoStatus = request.Motivo;

        _ideias.Update(ideia);
        await _logs.RegistrarAsync("edição", usuarioId: null, ideiaId: ideia.Id, propostaId: null, descricao: $"Alteração de status da ideia {ideia.Nome} para {request.StatusId}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await DetalhesAsync(idaId, cancellationToken);
    }

    public async Task<IdeiaDocumentoResponse> UploadDocumentoAsync(long idaId, long usuarioId, Stream pdfStream, string fileName, CancellationToken cancellationToken)
    {
        var ideia = await _ideias.GetByIdAsync(idaId, cancellationToken);
        if (ideia is null)
        {
            throw new AppException("Ideia não encontrada.", 404);
        }

        if (ideia.UsuarioId != usuarioId)
        {
            throw new AppException("Você não tem permissão para enviar documento nesta ideia.", 403);
        }

        var storedPath = await _fileStorage.SavePdfAsync(pdfStream, fileName, cancellationToken);

        var doc = new IdaDocumento
        {
            IdeiaId = idaId,
            Arquivo = storedPath
        };

        ideia.Documentos.Add(doc);
        _ideias.Update(ideia);
        await _logs.RegistrarAsync("edição", usuarioId: null, ideiaId: ideia.Id, propostaId: null, descricao: $"Upload de documento para ideia {ideia.Nome}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new IdeiaDocumentoResponse(doc.Id, doc.Arquivo);
    }

    private static IdeiaDetailsResponse MapIdeia(IdaIdeia i)
    {
        return new IdeiaDetailsResponse(
            IdaId: i.Id,
            IdaUsuarioId: i.UsuarioId,
            IdaNome: i.Nome,
            IdaCategoriaId: i.CategoriaId,
            CategoriaNome: i.Categoria?.Nome ?? i.CategoriaId.ToString(),
            IdaStatusId: i.StatusId,
            StatusNome: i.Status?.Nome ?? i.StatusId.ToString(),
            IdaMotivoStatus: i.MotivoStatus,
            Info: i.Info is null
                ? null
                : new IdeiaInfoResponse(
                    IdaInfoCnpj: i.Info.Cnpj,
                    IdaInfoDescricao: i.Info.Descricao,
                    IdaInfoLinkVideo: i.Info.LinkVideo,
                    IdaInfoImagem: i.Info.Imagem,
                    IdaInfoFatia: i.Info.Fatia,
                    CreateDate: i.Info.CreateDate,
                    UpdateDate: i.Info.UpdateDate),
            Documentos: i.Documentos.Select(d => new IdeiaDocumentoResponse(d.Id, d.Arquivo)).ToList()
        );
    }
}

public sealed class PropostaService : IPropostaService
{
    private readonly IPropostaRepository _propostas;
    private readonly IIdeiaRepository _ideias;
    private readonly ILookupRepository _lookup;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogService _logs;

    public PropostaService(IPropostaRepository propostas, IIdeiaRepository ideias, ILookupRepository lookup, IUnitOfWork uow, IClock clock, ILogService logs)
    {
        _propostas = propostas;
        _ideias = ideias;
        _lookup = lookup;
        _uow = uow;
        _clock = clock;
        _logs = logs;
    }

    public async Task<PropostaResponse> EnviarInicialAsync(long ideiaId, long usuarioId, CreatePropostaRequest request, CancellationToken cancellationToken)
    {
        var ideia = await _ideias.GetByIdAsync(ideiaId, cancellationToken);
        if (ideia is null)
        {
            throw new AppException("Ideia não encontrada.", 404);
        }

        var aceitePendente = 3;
        if (await _lookup.GetPropostaAceiteByIdAsync(aceitePendente, cancellationToken) is null)
        {
            throw new AppException("Configuração de aceite inválida.", 500);
        }

        var proposta = new PrpProposta
        {
            IdeiaId = ideiaId,
            UsuarioId = usuarioId,
            Status = true
        };

        proposta.Infos.Add(new PrpInfo
        {
            Id = 0,
            Mensagem = request.Mensagem,
            Valor = request.Valor,
            FatiaPret = request.FatiaPret,
            AceiteId = aceitePendente,
            Retorno = null,
            CreateDate = _clock.UtcNow,
            UpdateDate = _clock.UtcNow
        });

        await _propostas.AddAsync(proposta, cancellationToken);
        await _logs.RegistrarAsync("proposta", usuarioId: usuarioId, ideiaId: ideiaId, propostaId: null, descricao: $"Envio de proposta para ideia {ideia.Nome}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapAsync(proposta.Id, cancellationToken);
    }

    public async Task<PropostaResponse> ResponderAsync(long propostaId, long usuarioId, ResponderPropostaRequest request, CancellationToken cancellationToken)
    {
        var proposta = await _propostas.GetByIdAsync(propostaId, cancellationToken);
        if (proposta is null)
        {
            throw new AppException("Proposta não encontrada.", 404);
        }

        var ideia = proposta.Ideia ?? await _ideias.GetByIdAsync(proposta.IdeiaId, cancellationToken);
        if (ideia is null)
        {
            throw new AppException("Ideia não encontrada.", 404);
        }

        if (ideia.UsuarioId != usuarioId)
        {
            throw new AppException("Você não tem permissão para responder esta proposta.", 403);
        }

        if (await _lookup.GetPropostaAceiteByIdAsync(request.AceiteId, cancellationToken) is null)
        {
            throw new AppException("Aceite inválido.", 400);
        }

        proposta.Infos.Add(new PrpInfo
        {
            Id = 0,
            Mensagem = null,
            Valor = 0,
            FatiaPret = 0,
            AceiteId = request.AceiteId,
            Retorno = request.Retorno,
            CreateDate = _clock.UtcNow,
            UpdateDate = _clock.UtcNow
        });

        _propostas.Update(proposta);
        await _logs.RegistrarAsync("proposta", usuarioId: usuarioId, ideiaId: ideia.Id, propostaId: proposta.Id, descricao: $"Resposta de proposta {proposta.Id} para ideia {ideia.Nome}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapAsync(proposta.Id, cancellationToken);
    }

    public async Task<List<PropostaResponse>> ListarMinhasAsync(long usuarioId, CancellationToken cancellationToken)
    {
        var propostas = await _propostas.ListByUsuarioAsync(usuarioId, cancellationToken);
        return propostas.Select(Map).ToList();
    }

    public async Task<PropostaResponse> EncerrarAsync(long propostaId, long usuarioId, CancellationToken cancellationToken)
    {
        var proposta = await _propostas.GetByIdAsync(propostaId, cancellationToken);
        if (proposta is null)
        {
            throw new AppException("Proposta não encontrada.", 404);
        }

        if (proposta.UsuarioId != usuarioId)
        {
            throw new AppException("Você não tem permissão para encerrar esta proposta.", 403);
        }

        proposta.Status = false;
        _propostas.Update(proposta);
        await _logs.RegistrarAsync("proposta", usuarioId: usuarioId, ideiaId: proposta.IdeiaId, propostaId: proposta.Id, descricao: $"Encerramento da proposta {proposta.Id}", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return Map(proposta);
    }

    // ── NOVO: lista propostas recebidas em uma ideia do empreendedor ──────────
    public async Task<List<PropostaResponse>> ListarDaIdeiaAsync(long ideiaId, long donoId, CancellationToken cancellationToken)
    {
        var ideia = await _ideias.GetByIdAsync(ideiaId, cancellationToken)
            ?? throw new AppException("Ideia não encontrada.", 404);

        if (ideia.UsuarioId != donoId)
            throw new AppException("Sem permissão para ver as propostas desta ideia.", 403);

        var propostas = await _propostas.ListByIdeiaAsync(ideiaId, cancellationToken);
        return propostas.Select(Map).ToList();
    }
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<PropostaResponse> MapAsync(long prpId, CancellationToken cancellationToken)
    {
        var proposta = await _propostas.GetByIdAsync(prpId, cancellationToken);
        if (proposta is null)
        {
            throw new AppException("Proposta não encontrada.", 404);
        }
        return Map(proposta);
    }

    private static PropostaResponse Map(PrpProposta p)
    {
        return new PropostaResponse(
            PrpId: p.Id,
            PrpIdeiaId: p.IdeiaId,
            PrpUsuarioId: p.UsuarioId,
            PrpStatus: p.Status,
            Infos: p.Infos
                .OrderBy(i => i.CreateDate)
                .Select(i => new PropostaInfoResponse(
                    Mensagem: i.Mensagem,
                    Valor: i.Valor,
                    FatiaPret: i.FatiaPret,
                    AceiteId: i.AceiteId,
                    AceiteNome: i.Aceite?.Nome ?? i.AceiteId.ToString(),
                    Retorno: i.Retorno,
                    CreateDate: i.CreateDate,
                    UpdateDate: i.UpdateDate))
                .ToList()
        );
    }
}

public sealed class NotificacaoService : INotificacaoService
{
    private readonly INotificacaoRepository _notificacoes;
    private readonly ILookupRepository _lookup;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public NotificacaoService(INotificacaoRepository notificacoes, ILookupRepository lookup, IUnitOfWork uow, IClock clock)
    {
        _notificacoes = notificacoes;
        _lookup = lookup;
        _uow = uow;
        _clock = clock;
    }

    public async Task<NotificacaoResponse> DispararAsync(DispararNotificacaoRequest request, CancellationToken cancellationToken)
    {
        var tipo = await _lookup.GetNotificacaoTipoByIdAsync(request.TipoId, cancellationToken);
        if (tipo is null)
        {
            throw new AppException("Tipo de notificação inválido.", 400);
        }

        var notif = new NtfNotificacao
        {
            UsuarioId = request.UsuarioId,
            TipoId = request.TipoId,
            Mensagem = request.Mensagem,
            Lida = false,
            CreateDate = _clock.UtcNow
        };

        await _notificacoes.AddAsync(notif, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new NotificacaoResponse(notif.Id, notif.TipoId, tipo.Nome, notif.Mensagem, notif.Lida, notif.CreateDate);
    }

    public async Task<List<NotificacaoResponse>> ListarMinhasAsync(long usuarioId, CancellationToken cancellationToken)
    {
        var items = await _notificacoes.ListByUsuarioAsync(usuarioId, cancellationToken);
        return items
            .OrderByDescending(n => n.CreateDate)
            .Select(n => new NotificacaoResponse(n.Id, n.TipoId, n.Tipo?.Nome ?? n.TipoId.ToString(), n.Mensagem, n.Lida, n.CreateDate))
            .ToList();
    }

    public async Task<NotificacaoResponse> MarcarLidaAsync(long notificacaoId, long usuarioId, CancellationToken cancellationToken)
    {
        var n = await _notificacoes.GetByIdAsync(notificacaoId, cancellationToken);
        if (n is null)
        {
            throw new AppException("Notificação não encontrada.", 404);
        }

        if (n.UsuarioId != usuarioId)
        {
            throw new AppException("Você não tem permissão para alterar esta notificação.", 403);
        }

        n.Lida = true;
        _notificacoes.Update(n);
        await _uow.SaveChangesAsync(cancellationToken);
        return new NotificacaoResponse(n.Id, n.TipoId, n.Tipo?.Nome ?? n.TipoId.ToString(), n.Mensagem, n.Lida, n.CreateDate);
    }
}