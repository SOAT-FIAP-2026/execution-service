namespace Fiap.TechChallenge.Execution.Application.DTOs;

public sealed record FinalizarDiagnosticoRequest(
    string LaudoTecnico,
    IReadOnlyList<ServicoSugeridoRequest>? ServicosSugeridos = null,
    IReadOnlyList<PecaSugeridaRequest>? PecasSugeridas = null);

public sealed record ServicoSugeridoRequest(
    Guid IdServico,
    string? Descricao = null);

public sealed record PecaSugeridaRequest(
    Guid IdPeca,
    int Quantidade,
    string? Descricao = null);

public sealed record FinalizarTarefaRequest(
    string? Observacoes = null);

public sealed record CancelarProducaoRequest(
    string Motivo);

