using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class CriarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly IClienteGateway _clientes;
    private readonly IPublicadorEventoOs _publicador;

    public CriarOrdemUseCase(
        IOrdemDeServicoGateway gateway, IClienteGateway clientes, IPublicadorEventoOs publicador)
    {
        _gateway = gateway;
        _clientes = clientes;
        _publicador = publicador;
    }

    public async Task<OrdemDeServico> ExecutarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var cliente = await _clientes.ObterPorIdAsync(req.ClienteId, ct)
            ?? throw new OrdemInvalidaException("Cliente não encontrado.");

        if (!cliente.Ativo)
            throw new OrdemInvalidaException("Cliente inativo.");

        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Id == req.VeiculoId)
            ?? throw new OrdemInvalidaException("Veículo não pertence ao cliente informado.");

        var os = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _gateway.AdicionarAsync(os, ct);

        // Publica o evento de criação (statusAnterior=null → statusNovo="Recebida"),
        // que é o único ponto do domínio a produzir esse valor e alimenta o painel
        // obrigatório de volume diário de OS (Task 10, revisão — ACHADO 1).
        await _publicador.ExecutarTransicaoComTelemetriaAsync(os, async () =>
        {
            await _gateway.SalvarAsync(ct);
        });

        // Recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
        return await _gateway.ObterPorIdAsync(os.Id, ct)
            ?? throw new InvalidOperationException("OS não encontrada após criação.");
    }
}
