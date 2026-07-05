using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class OrdemInvalidaException : Exception
{
    public OrdemInvalidaException(string mensagem) : base(mensagem) { }
}

public class CriarOrdemUseCase
{
    private readonly IOrdemDeServicoRepositorio _repo;
    private readonly IClienteGateway _clientes;

    public CriarOrdemUseCase(IOrdemDeServicoRepositorio repo, IClienteGateway clientes)
    {
        _repo = repo;
        _clientes = clientes;
    }

    public async Task<OrdemResponse> ExecutarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var cliente = await _clientes.ObterPorIdAsync(req.ClienteId, ct)
            ?? throw new OrdemInvalidaException("Cliente não encontrado.");

        if (!cliente.Ativo)
            throw new OrdemInvalidaException("Cliente inativo.");

        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Id == req.VeiculoId)
            ?? throw new OrdemInvalidaException("Veículo não pertence ao cliente informado.");

        var os = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _repo.AdicionarAsync(os, ct);
        await _repo.SalvarAsync(ct);
        os.LimparEventos();

        // Recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
        var carregada = await _repo.ObterPorIdAsync(os.Id, ct)
            ?? throw new InvalidOperationException("OS não encontrada após criação.");
        return MapeadorOrdem.Mapear(carregada);
    }
}
