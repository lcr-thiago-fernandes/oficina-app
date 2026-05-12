using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

internal static class MapeadorClienteResponse
{
    public static ClienteResponse Mapear(Cliente c) => new(
        c.Id,
        c.Nome,
        c.Documento.Tipo.ToString(),
        c.Documento.Valor,
        c.Documento.Mascarado(),
        c.Email.Valor,
        c.Telefone.Valor,
        c.Ativo,
        c.CriadoEm,
        c.Veiculos.Select(MapearVeiculo).ToList());

    public static VeiculoResponse MapearVeiculo(Veiculo v) =>
        new(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano);
}
