using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Clientes.Presenters;

public static class ClientePresenter
{
    public static ClienteResponse Apresentar(Cliente c) => new(
        c.Id,
        c.Nome,
        c.Documento.Tipo.ToString(),
        c.Documento.Valor,
        c.Documento.Mascarado(),
        c.Email.Valor,
        c.Telefone.Valor,
        c.Ativo,
        c.CriadoEm,
        c.Veiculos.Select(ApresentarVeiculo).ToList());

    public static VeiculoResponse ApresentarVeiculo(Veiculo v) =>
        new(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano);

    public static PaginaClientes ApresentarPagina(IReadOnlyList<Cliente> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);
}
