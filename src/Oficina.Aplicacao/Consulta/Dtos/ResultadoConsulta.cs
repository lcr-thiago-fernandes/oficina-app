namespace Oficina.Aplicacao.Consulta.Dtos;

public abstract record ResultadoConsulta
{
    public sealed record Sucesso(ConsultaPublicaResponse Response) : ResultadoConsulta;
    public sealed record NaoEncontrada() : ResultadoConsulta;
    public sealed record DocumentoNaoConfere() : ResultadoConsulta;   // tratado como 404 também (não vaza)
}
