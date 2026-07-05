using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Catalogo.Presenters;

public static class ServicoPresenter
{
    public static ServicoResponse Apresentar(Servico servico) => new(
        servico.Id, servico.Nome, servico.Descricao, servico.PrecoBase,
        servico.TempoEstimadoMinutos, servico.Ativo, servico.CriadoEm);

    public static PaginaServicos ApresentarPagina(IReadOnlyList<Servico> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);
}
