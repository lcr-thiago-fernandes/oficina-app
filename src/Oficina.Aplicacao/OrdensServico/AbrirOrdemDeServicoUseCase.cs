using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Abre uma OS de forma consolidada: cria/reaproveita cliente (por documento) e
// veículo (por placa), monta a OS com serviços/peças, reserva o estoque das peças e
// inicia a saga, tudo numa única unidade de trabalho dentro de uma transação
// serializável (duas aberturas disputando a última unidade: só uma reserva).
// Os gateways compartilham o mesmo OficinaDbContext scoped, então um único
// SalvarAsync commita cliente+veículo+OS+itens+reservas+mensagem do outbox.
public class AbrirOrdemDeServicoUseCase
{
    private readonly IClienteGateway _clientes;
    private readonly IServicoGateway _servicos;
    private readonly IPecaGateway _pecas;
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPublicadorEventoOs _publicador;
    private readonly ISagaOrdemServicoGateway _saga;

    public AbrirOrdemDeServicoUseCase(
        IClienteGateway clientes,
        IServicoGateway servicos,
        IPecaGateway pecas,
        IOrdemDeServicoGateway ordens,
        IPublicadorEventoOs publicador,
        ISagaOrdemServicoGateway saga)
    {
        _clientes = clientes;
        _servicos = servicos;
        _pecas = pecas;
        _ordens = ordens;
        _publicador = publicador;
        _saga = saga;
    }

    public async Task<OrdemDeServico> ExecutarAsync(AbrirOrdemRequest req, CancellationToken ct)
    {
        Guid ordemId = default;

        await _ordens.EmTransacaoSerializadaAsync(async tx =>
        {
            // 1) find-or-create cliente por documento
            var documento = Documento.Criar(req.ClienteDados.Documento);
            var cliente = await _clientes.ObterPorDocumentoAsync(documento, tx);
            var clienteNovo = cliente is null;
            if (cliente is null)
            {
                cliente = Cliente.Criar(
                    req.ClienteDados.Nome,
                    documento,
                    Email.Criar(req.ClienteDados.Email),
                    Telefone.Criar(req.ClienteDados.Telefone));
            }
            else if (!cliente.Ativo)
            {
                throw new OrdemInvalidaException("Cliente inativo.");
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
                await _clientes.AdicionarAsync(cliente, tx);
            else if (veiculoNovo)
                _clientes.MarcarVeiculoComoNovo(veiculo);

            // 3) cria a OS
            var ordem = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
            ordemId = ordem.Id;
            await _ordens.AdicionarAsync(ordem, tx);

            // 4-6) itens + reserva + saga + persistência, instrumentados: publica o evento de
            // criação (statusAnterior=null → statusNovo="Recebida") depois de SalvarAsync
            // bem-sucedido, ou o evento de falha se uma falha de processamento interromper a
            // abertura da OS (Task 10, revisão — ACHADO 1).
            await _publicador.ExecutarTransicaoComTelemetriaAsync(ordem, async () =>
            {
                // 4) serviços
                foreach (var s in req.Servicos ?? Enumerable.Empty<ItemServicoDto>())
                {
                    var servico = await _servicos.ObterPorIdAsync(s.ServicoId, tx)
                        ?? throw new OrdemInvalidaException($"Serviço {s.ServicoId} não encontrado.");
                    if (!servico.Ativo)
                        throw new OrdemInvalidaException($"Serviço '{servico.Nome}' está inativo.");

                    var item = ordem.AdicionarItemServico(servico.Id, servico.Nome, servico.PrecoBase, s.Quantidade);
                    _ordens.MarcarItemServicoComoNovo(item);
                }

                // 5) peças (cada Peca carregada uma vez; reaproveitada na reserva)
                var pecas = new Dictionary<Guid, Peca>();
                foreach (var p in req.Pecas ?? Enumerable.Empty<ItemPecaDto>())
                {
                    if (!pecas.TryGetValue(p.PecaId, out var peca))
                    {
                        peca = await _pecas.ObterPorIdAsync(p.PecaId, tx)
                            ?? throw new OrdemInvalidaException($"Peça {p.PecaId} não encontrada.");
                        if (!peca.Ativo)
                            throw new OrdemInvalidaException($"Peça '{peca.Nome}' está inativa.");
                        pecas.Add(peca.Id, peca);
                    }

                    var item = ordem.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, p.Quantidade);
                    _ordens.MarcarItemPecaComoNovo(item);
                }

                // 5b) reserva do estoque, uma por peça com a soma dos itens (o domínio recusa
                //     uma segunda reserva da mesma OS na mesma peça). Saldo insuficiente lança
                //     antes de qualquer SalvarAsync. A movimentação tem Id Guid pré-setado:
                //     marcada explicitamente como nova, senão o EF gera UPDATE.
                foreach (var grupo in ordem.ItensPeca.GroupBy(i => i.PecaId))
                {
                    var reserva = pecas[grupo.Key].Reservar(grupo.Sum(i => i.Quantidade), ordem.Id);
                    _pecas.MarcarMovimentacaoComoNova(reserva);
                }

                // 5c) inicia a saga (só enfileira no outbox; persiste no SalvarAsync abaixo) e marca
                //     a OS como coordenada por saga na mesma unidade de trabalho — é o que separa
                //     uma OS da Fase 4 (cancelamento pela saga) de uma da Fase 3 (síncrono).
                ordem.MarcarCoordenadaPorSaga();
                await _saga.IniciarAsync(ordem.Id, tx);

                // 6) única unidade de trabalho — commita cliente + veículo + OS + itens + reservas + outbox
                await _ordens.SalvarAsync(tx);
            });
        }, ct);

        // 7) recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
        return await _ordens.ObterPorIdAsync(ordemId, ct)
            ?? throw new InvalidOperationException("OS não encontrada após abertura.");
    }
}
