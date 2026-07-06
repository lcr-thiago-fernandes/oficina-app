using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.OrdensServico;

namespace Oficina.Aplicacao;

public static class DependencyInjectionAplicacao
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        // Auth (movido do Plano 2)
        services.AddScoped<LoginUseCase>();
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
        services.AddScoped<CriarOrdemUseCase>();
        services.AddScoped<AbrirOrdemDeServicoUseCase>();
        services.AddScoped<ObterOrdemPorIdUseCase>();
        services.AddScoped<ListarOrdensUseCase>();
        services.AddScoped<IniciarDiagnosticoUseCase>();
        services.AddScoped<EnviarOrcamentoParaAprovacaoUseCase>();
        services.AddScoped<IniciarExecucaoUseCase>();
        services.AddScoped<FinalizarOrdemUseCase>();
        services.AddScoped<EntregarOrdemUseCase>();
        services.AddScoped<AdicionarItemServicoUseCase>();
        services.AddScoped<RemoverItemServicoUseCase>();
        services.AddScoped<AdicionarItemPecaUseCase>();
        services.AddScoped<RemoverItemPecaUseCase>();
        services.AddScoped<ObterTempoMedioExecucaoUseCase>();

        // Consulta pública (sem auth)
        services.AddScoped<ConsultarOrdemPorNumeroUseCase>();
        services.AddScoped<AprovarOrcamentoPorClienteUseCase>();
        services.AddScoped<RejeitarOrcamentoPorClienteUseCase>();

        // Validators (auto-discovery via assembly)
        services.AddValidatorsFromAssembly(typeof(DependencyInjectionAplicacao).Assembly);

        return services;
    }
}
