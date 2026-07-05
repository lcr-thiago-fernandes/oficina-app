using Oficina.Adaptadores.Clientes.Presenters;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;

namespace Oficina.Adaptadores.Clientes.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class ClienteController
{
    private readonly CriarClienteUseCase _criar;
    private readonly ObterClientePorIdUseCase _obter;
    private readonly BuscarClientePorDocumentoUseCase _buscar;
    private readonly ListarClientesUseCase _listar;
    private readonly AtualizarClienteUseCase _atualizar;
    private readonly RemoverClienteUseCase _remover;
    private readonly AdicionarVeiculoUseCase _adicionarVeiculo;
    private readonly AtualizarVeiculoUseCase _atualizarVeiculo;
    private readonly RemoverVeiculoUseCase _removerVeiculo;
    private readonly ListarVeiculosUseCase _listarVeiculos;

    public ClienteController(
        CriarClienteUseCase criar,
        ObterClientePorIdUseCase obter,
        BuscarClientePorDocumentoUseCase buscar,
        ListarClientesUseCase listar,
        AtualizarClienteUseCase atualizar,
        RemoverClienteUseCase remover,
        AdicionarVeiculoUseCase adicionarVeiculo,
        AtualizarVeiculoUseCase atualizarVeiculo,
        RemoverVeiculoUseCase removerVeiculo,
        ListarVeiculosUseCase listarVeiculos)
    {
        _criar = criar;
        _obter = obter;
        _buscar = buscar;
        _listar = listar;
        _atualizar = atualizar;
        _remover = remover;
        _adicionarVeiculo = adicionarVeiculo;
        _atualizarVeiculo = atualizarVeiculo;
        _removerVeiculo = removerVeiculo;
        _listarVeiculos = listarVeiculos;
    }

    public async Task<ClienteResponse> CriarAsync(CriarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _criar.ExecutarAsync(req, ct);
        return ClientePresenter.Apresentar(cliente);
    }

    public async Task<ClienteResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var cliente = await _obter.ExecutarAsync(id, ct);
        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public async Task<ClienteResponse?> BuscarPorDocumentoAsync(string documento, CancellationToken ct)
    {
        var cliente = await _buscar.ExecutarAsync(documento, ct);
        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public async Task<PaginaClientes> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(pagina, tamanhoPagina, ct);
        return ClientePresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<ClienteResponse?> AtualizarAsync(Guid id, AtualizarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _atualizar.ExecutarAsync(id, req, ct);
        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken ct) => _remover.ExecutarAsync(id, ct);

    public async Task<VeiculoResponse?> AdicionarVeiculoAsync(Guid clienteId, AdicionarVeiculoRequest req, CancellationToken ct)
    {
        var veiculo = await _adicionarVeiculo.ExecutarAsync(clienteId, req, ct);
        return veiculo is null ? null : ClientePresenter.ApresentarVeiculo(veiculo);
    }

    public async Task<VeiculoResponse?> AtualizarVeiculoAsync(Guid clienteId, string placa, AtualizarVeiculoRequest req, CancellationToken ct)
    {
        var veiculo = await _atualizarVeiculo.ExecutarAsync(clienteId, placa, req, ct);
        return veiculo is null ? null : ClientePresenter.ApresentarVeiculo(veiculo);
    }

    public Task<bool> RemoverVeiculoAsync(Guid clienteId, string placa, CancellationToken ct) =>
        _removerVeiculo.ExecutarAsync(clienteId, placa, ct);

    public async Task<IReadOnlyList<VeiculoResponse>?> ListarVeiculosAsync(Guid clienteId, CancellationToken ct)
    {
        var veiculos = await _listarVeiculos.ExecutarAsync(clienteId, ct);
        return veiculos?.Select(ClientePresenter.ApresentarVeiculo).ToList();
    }
}
