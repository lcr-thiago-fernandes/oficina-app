using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.Consulta;

public class ConsultarOrdemPorNumeroUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    private readonly IClienteGateway _clientes;

    public ConsultarOrdemPorNumeroUseCase(
        IOrdemDeServicoRepositorio ordens, IClienteGateway clientes)
    {
        _ordens = ordens;
        _clientes = clientes;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(long numero, string documentoBruto, CancellationToken ct)
    {
        // Valida documento sem lançar — assinatura uniforme
        Documento? doc = null;
        try { doc = Documento.Criar(documentoBruto); }
        catch (DocumentoInvalidoException) { return new ResultadoConsulta.NaoEncontrada(); }

        var os = await _ordens.ObterPorNumeroAsync(numero, ct);
        if (os is null) return new ResultadoConsulta.NaoEncontrada();

        var cliente = await _clientes.ObterPorIdAsync(os.ClienteId, ct);
        if (cliente is null) return new ResultadoConsulta.NaoEncontrada();

        if (!cliente.Documento.Equals(doc))
            return new ResultadoConsulta.DocumentoNaoConfere();

        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Id == os.VeiculoId)
            ?? throw new InvalidOperationException("Inconsistência: veículo da OS não pertence ao cliente.");

        var itens = new List<ItemConsultaResponse>();
        itens.AddRange(os.ItensServico.Select(i =>
            new ItemConsultaResponse("Servico", i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));
        itens.AddRange(os.ItensPeca.Select(i =>
            new ItemConsultaResponse("Peca", i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));

        var resp = new ConsultaPublicaResponse(
            os.Numero, os.Status.ToString(),
            cliente.Nome, cliente.Documento.Mascarado(),
            veiculo.Placa.Valor, $"{veiculo.Marca} {veiculo.Modelo} ({veiculo.Ano})",
            os.TotalServicos, os.TotalPecas, os.TotalGeral,
            os.CriadaEm, os.EnviadaAprovacaoEm, os.OrcamentoAprovadoEm, os.OrcamentoRejeitadoEm,
            os.IniciadaEm, os.FinalizadaEm, os.EntregueEm,
            itens);

        return new ResultadoConsulta.Sucesso(resp);
    }
}
