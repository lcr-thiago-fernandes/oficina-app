using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class AdicionarItemServicoUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    private readonly IServicoRepositorio _servicos;

    public AdicionarItemServicoUseCase(IOrdemDeServicoRepositorio ordens, IServicoRepositorio servicos)
    {
        _ordens = ordens;
        _servicos = servicos;
    }

    public async Task<ItemServicoResponse?> ExecutarAsync(Guid ordemId, AdicionarItemServicoRequest req, CancellationToken ct)
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

        return MapeadorOrdem.MapearServ(item);
    }
}
