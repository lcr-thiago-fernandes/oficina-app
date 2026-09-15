using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Oficina.Api.Testes.Configuracao;

/// <summary>
/// Guarda de regressao para o piso de versao da stack que VALIDA o token.
///
/// O Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10 depende de
/// Microsoft.IdentityModel.Protocols.OpenIdConnect 7.1.2, entao sozinho ele
/// resolve toda a Microsoft.IdentityModel para 7.1.2. Os PackageReference
/// explicitos em Oficina.Api.csproj seguram em 8.2.1.
///
/// Microsoft.IdentityModel.JsonWebTokens nao e referenciado por nenhum tipo do
/// codigo de producao — o JsonWebTokenHandler roda dentro do JwtBearer —, o que
/// faz o pin dele parecer removivel numa limpeza de pacotes. Este teste existe
/// para que essa remocao falhe aqui, e nao silenciosamente em producao.
/// </summary>
public class VersaoDaStackDeValidacaoTestes
{
    [Fact]
    public void JsonWebTokenHandler_deve_vir_da_major_8_ou_superior()
    {
        var versao = typeof(JsonWebTokenHandler).Assembly.GetName().Version;

        versao.Should().NotBeNull();
        versao!.Major.Should().BeGreaterThanOrEqualTo(8,
            "o pin de Microsoft.IdentityModel.JsonWebTokens em Oficina.Api.csproj segura a "
            + "stack de validacao em 8.2.1; sem ele o JwtBearer 8.0.10 a rebaixa para 7.1.2");
    }

    [Fact]
    public void SecurityToken_deve_vir_da_major_8_ou_superior()
    {
        var versao = typeof(SecurityToken).Assembly.GetName().Version;

        versao.Should().NotBeNull();
        versao!.Major.Should().BeGreaterThanOrEqualTo(8,
            "o pin de Microsoft.IdentityModel.Tokens em Oficina.Api.csproj segura a "
            + "stack de validacao em 8.2.1; sem ele o JwtBearer 8.0.10 a rebaixa para 7.1.2");
    }
}
