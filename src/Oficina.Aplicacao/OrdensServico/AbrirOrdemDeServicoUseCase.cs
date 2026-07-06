using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Abre uma OS de forma consolidada: cria/reaproveita cliente (por documento) e
// veículo (por placa), monta a OS com serviços/peças e persiste tudo numa única
// unidade de trabalho. Os gateways compartilham o mesmo OficinaDbContext scoped,
// então um único SalvarAsync commita cliente+veículo+OS+itens atomicamente.
public class AbrirOrdemDeServicoUseCase
{
    private readonly IClienteGateway _clientes;
    private readonly IServicoGateway _servicos;
    private readonly IPecaGateway _pecas;
    private readonly IOrdemDeServicoGateway _ordens;

    public AbrirOrdemDeServicoUseCase(
        IClienteGateway clientes,
        IServicoGateway servicos,
        IPecaGateway pecas,
        IOrdemDeServicoGateway ordens)
    {
        _clientes = clientes;
        _servicos = servicos;
        _pecas = pecas;
        _ordens = ordens;
    }

    public async Task<OrdemDeServico> ExecutarAsync(AbrirOrdemRequest req, CancellationToken ct)
    {
        // 1) find-or-create cliente por documento
        var documento = Documento.Criar(req.ClienteDados.Documento);
        var cliente = await _clientes.ObterPorDocumentoAsync(documento, ct);
        var clienteNovo = cliente is null;
        if (cliente is null)
        {
            cliente = Cliente.Criar(
                req.ClienteDados.Nome,
                documento,
                Email.Criar(req.ClienteDados.Email),
                Telefone.Criar(req.ClienteDados.Telefone));
        }

        // 2) find-or-add veículo por placa
        var placa = Placa.Criar(req.VeiculoDados.Placa);
        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Placa.Equals(placa));
        var veiculoNovo = veiculo is null;
        if (veiculo is null)
        {
            veiculo = Veiculo.Criar(placa, req.VeiculoDados.Marca, req.VeiculoDados.Modelo, req.VeiculoDados.Ano);
            cliente.AdicionarVeiculo(veiculo);
        }

        // Persistência do cliente/veículo no change tracker (sem SalvarAsync ainda):
        //  - cliente novo: AdicionarAsync insere o grafo completo (cliente + veículo);
        //  - cliente existente com veículo novo: MarcarVeiculoComoNovo marca o veículo
        //    (workaround de change detection para Id pré-setado em navigation collection).
        if (clienteNovo)
            await _clientes.AdicionarAsync(cliente, ct);
        else if (veiculoNovo)
            _clientes.MarcarVeiculoComoNovo(veiculo);

        // 3) cria a OS
        var ordem = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _ordens.AdicionarAsync(ordem, ct);

        // 4) serviços
        foreach (var s in req.Servicos)
        {
            var servico = await _servicos.ObterPorIdAsync(s.ServicoId, ct)
                ?? throw new OrdemInvalidaException($"Serviço {s.ServicoId} não encontrado.");
            if (!servico.Ativo)
                throw new OrdemInvalidaException($"Serviço '{servico.Nome}' está inativo.");

            var item = ordem.AdicionarItemServico(servico.Id, servico.Nome, servico.PrecoBase, s.Quantidade);
            _ordens.MarcarItemServicoComoNovo(item);
        }

        // 5) peças
        foreach (var p in req.Pecas)
        {
            var peca = await _pecas.ObterPorIdAsync(p.PecaId, ct)
                ?? throw new OrdemInvalidaException($"Peça {p.PecaId} não encontrada.");
            if (!peca.Ativo)
                throw new OrdemInvalidaException($"Peça '{peca.Nome}' está inativa.");

            var item = ordem.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, p.Quantidade);
            _ordens.MarcarItemPecaComoNovo(item);
        }

        // 6) única unidade de trabalho — commita cliente + veículo + OS + itens
        await _ordens.SalvarAsync(ct);

        // 7) recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
        return await _ordens.ObterPorIdAsync(ordem.Id, ct)
            ?? throw new InvalidOperationException("OS não encontrada após abertura.");
    }
}
