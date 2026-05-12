namespace Oficina.Aplicacao.Clientes.Dtos;

public sealed record VeiculoResponse(
    Guid Id,
    string Placa,
    string Marca,
    string Modelo,
    int Ano);
