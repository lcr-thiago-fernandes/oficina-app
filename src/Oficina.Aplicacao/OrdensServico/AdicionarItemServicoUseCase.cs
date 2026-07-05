using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class AdicionarItemServicoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IServicoGateway _servicos;

    public AdicionarItemServicoUseCase(IOrdemDeServicoGateway ordens, IServicoGateway servicos)
    {
        _ordens = ordens;
        _servicos = servicos;
    }

    public async Task<ItemServico?> ExecutarAsync(Guid ordemId, AdicionarItemServicoRequest req, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        var servico = await _servicos.ObterPorIdAsync(req.ServicoId, ct)
            ?? throw new OrdemInvalidaException("Serviço não encontrado.");
        if (!servico.Ativo)
            throw new OrdemInvalidaException("Serviço inativo.");

        var item = ordem.AdicionarItemServico(servico.Id, servico.Nome, servico.PrecoBase, req.Quantidade);
        _ordens.MarcarItemServicoComoNovo(item);
        await _ordens.SalvarAsync(ct);

        return item;
    }
}
