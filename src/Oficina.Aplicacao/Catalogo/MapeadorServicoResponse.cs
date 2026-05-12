using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

internal static class MapeadorServicoResponse
{
    public static ServicoResponse Mapear(Servico s) => new(
        s.Id, s.Nome, s.Descricao, s.PrecoBase, s.TempoEstimadoMinutos, s.Ativo, s.CriadoEm);
}
