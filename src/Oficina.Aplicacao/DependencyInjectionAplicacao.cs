using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Autoatendimento;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Saga;

namespace Oficina.Aplicacao;

public static class DependencyInjectionAplicacao
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        // Auth — so o bootstrap do admin; a emissao de token saiu da API.
        services.AddScoped<BootstrapAdminUseCase>();

        // Clientes
        services.AddScoped<CriarClienteUseCase>();
        services.AddScoped<AtualizarClienteUseCase>();
        services.AddScoped<RemoverClienteUseCase>();
        services.AddScoped<ObterClientePorIdUseCase>();
        services.AddScoped<BuscarClientePorDocumentoUseCase>();
        services.AddScoped<ListarClientesUseCase>();
        services.AddScoped<AdicionarVeiculoUseCase>();
        services.AddScoped<AtualizarVeiculoUseCase>();
        services.AddScoped<RemoverVeiculoUseCase>();
        services.AddScoped<ListarVeiculosUseCase>();

        // Catalogo
        services.AddScoped<CriarServicoUseCase>();
        services.AddScoped<AtualizarServicoUseCase>();
        services.AddScoped<RemoverServicoUseCase>();
        services.AddScoped<ObterServicoPorIdUseCase>();
        services.AddScoped<ListarServicosUseCase>();

        // Estoque
        services.AddScoped<CriarPecaUseCase>();
        services.AddScoped<AtualizarPecaUseCase>();
        services.AddScoped<RemoverPecaUseCase>();
        services.AddScoped<ObterPecaPorIdUseCase>();
        services.AddScoped<ListarPecasUseCase>();
        services.AddScoped<RegistrarMovimentacaoUseCase>();
        services.AddScoped<ListarMovimentacoesUseCase>();

        // Ordens de Serviço
        services.AddScoped<AbrirOrdemDeServicoUseCase>();
        services.AddScoped<ObterOrdemPorIdUseCase>();
        services.AddScoped<ListarOrdensUseCase>();
        services.AddScoped<EntregarOrdemUseCase>();
        services.AddScoped<ObterTempoMedioExecucaoUseCase>();
        services.AddScoped<CancelarOrdemUseCase>();
        services.AddScoped<ObterSagaDaOrdemUseCase>();

        // Passos locais (OS + estoque) da saga orquestrada
        services.AddScoped<IPassosSagaOrdemServico, PassosSagaOrdemServico>();

        // Consulta pública (sem auth) — apenas leitura de status (anti-enumeração)
        services.AddScoped<ConsultarOrdemPorNumeroUseCase>();

        // Autoatendimento do cliente
        services.AddScoped<ListarOrdensDoClienteUseCase>();
        services.AddScoped<ListarVeiculosDoClienteUseCase>();

        // Validators (auto-discovery via assembly)
        services.AddValidatorsFromAssembly(typeof(DependencyInjectionAplicacao).Assembly);

        return services;
    }
}
