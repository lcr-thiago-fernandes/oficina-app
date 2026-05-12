using Oficina.Dominio.Auth;

namespace Oficina.Dominio.ServicosCompartilhados;

public interface IGeradorTokenJwt
{
    TokenJwt Gerar(Usuario usuario);
}

public sealed record TokenJwt(string AccessToken, int ExpiraEmSegundos);
