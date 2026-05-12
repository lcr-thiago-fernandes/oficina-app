namespace Oficina.Aplicacao.Clientes.Dtos;

public sealed record CriarClienteRequest(string Nome, string Documento, string Email, string Telefone);
