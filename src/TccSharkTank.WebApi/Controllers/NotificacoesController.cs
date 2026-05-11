using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TccSharkTank.Application.Abstractions.Security;
using TccSharkTank.Application.Contracts;
using TccSharkTank.Application.Services;

namespace TccSharkTank.WebApi.Controllers;

[ApiController]
[Route("api/notificacoes")]
[Authorize]
public sealed class NotificacoesController : ControllerBase
{
    private readonly INotificacaoService _notificacoes;
    private readonly ICurrentUser _currentUser;

    public NotificacoesController(INotificacaoService notificacoes, ICurrentUser currentUser)
    {
        _notificacoes = notificacoes;
        _currentUser = currentUser;
    }

    [HttpGet("minhas")]
    public Task<List<NotificacaoResponse>> Minhas(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new TccSharkTank.Application.Common.AppException("Não autenticado.", 401);
        return _notificacoes.ListarMinhasAsync(userId, cancellationToken);
    }

    [HttpPost("{id:long}/lida")]
    public Task<NotificacaoResponse> MarcarLida([FromRoute] long id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new TccSharkTank.Application.Common.AppException("Não autenticado.", 401);
        return _notificacoes.MarcarLidaAsync(id, userId, cancellationToken);
    }

    // ── NOVO: qualquer usuário autenticado pode disparar notificação para outro ──
    [HttpPost("disparar")]
    public Task<NotificacaoResponse> Disparar([FromBody] DispararNotificacaoRequest request, CancellationToken cancellationToken)
        => _notificacoes.DispararAsync(request, cancellationToken);
}

// Admin controller permanece igual
[ApiController]
[Route("api/admin/notificacoes")]
[Authorize(Roles = "adm")]
public sealed class AdminNotificacoesController : ControllerBase
{
    private readonly INotificacaoService _notificacoes;

    public AdminNotificacoesController(INotificacaoService notificacoes)
    {
        _notificacoes = notificacoes;
    }

    [HttpPost]
    public Task<NotificacaoResponse> Disparar([FromBody] DispararNotificacaoRequest request, CancellationToken cancellationToken)
        => _notificacoes.DispararAsync(request, cancellationToken);
}