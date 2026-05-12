using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AdicionarVeiculoUseCase
{
    private readonly IClienteRepositorio _repo;
    public AdicionarVeiculoUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<VeiculoResponse?> ExecutarAsync(Guid clienteId, AdicionarVeiculoRequest req, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var veiculo = Veiculo.Criar(Placa.Criar(req.Placa), req.Marca, req.Modelo, req.Ano);
        cliente.AdicionarVeiculo(veiculo);
        _repo.MarcarVeiculoComoNovo(veiculo);

        await _repo.SalvarAsync(ct);
        return MapeadorClienteResponse.MapearVeiculo(veiculo);
    }
}
