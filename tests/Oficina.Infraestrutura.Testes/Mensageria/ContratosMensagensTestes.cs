using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using MassTransit;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Mensageria;

public class ContratosMensagensTestes
{
    public static TheoryData<Type> TiposDeContrato => new()
    {
        typeof(GerarOrcamento), typeof(OrcamentoGerado), typeof(OrcamentoFalhou),
        typeof(OrcamentoAprovado), typeof(OrcamentoRejeitado),
        typeof(PagamentoConfirmado), typeof(PagamentoRecusado), typeof(EstornarPagamento),
        typeof(PagamentoEstornado), typeof(EstornoFalhou), typeof(CancelarOrcamento),
        typeof(IniciarExecucao), typeof(ExecucaoEnfileirada), typeof(ExecucaoRecusada),
        typeof(StatusExecucaoAlterado), typeof(ExecucaoFinalizada),
    };

    private static string PastaContratos => Path.Combine(AppContext.BaseDirectory, "Contratos");

    [Fact]
    public void PastaDeContratos_TemExatamenteOs16Arquivos_EUmTipoParaCada()
    {
        var arquivos = Directory.GetFiles(PastaContratos, "*.json")
            .Select(Path.GetFileNameWithoutExtension);

        arquivos.Should().BeEquivalentTo(TiposDeContrato.Select(d => ((Type)d[0]).Name));
        arquivos.Should().HaveCount(16);
    }

    [Theory]
    [MemberData(nameof(TiposDeContrato))]
    public void Contrato_RoundTripDoJson_PreservaPropriedadesEValores(Type tipo)
    {
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(PastaContratos, $"{tipo.Name}.json")));
        var obj = JsonSerializer.Deserialize(original!.ToJsonString(), tipo, OpcoesJsonContrato.Criar());
        var reserializado = JsonSerializer.SerializeToNode(obj, tipo, OpcoesJsonContrato.Criar());

        ComparadorJson.Equivalentes(original, reserializado)
            .Should().BeTrue($"o round-trip de {tipo.Name} deve preservar o JSON.\nOriginal: {original}\nReserializado: {reserializado}");
    }

    [Theory]
    [MemberData(nameof(TiposDeContrato))]
    public void Contrato_TemMessageUrnEEntityNameDoSpec(Type tipo)
    {
        tipo.GetCustomAttribute<MessageUrnAttribute>()!.Urn.ToString()
            .Should().Be($"urn:message:oficina:saga:{tipo.Name}");
        tipo.GetCustomAttribute<EntityNameAttribute>()!.EntityName
            .Should().Be($"oficina-saga-{tipo.Name}");
    }

    [Theory]
    [MemberData(nameof(TiposDeContrato))]
    public void Contrato_ImplementaIMensagemDaSaga(Type tipo)
    {
        typeof(IMensagemDaSaga).IsAssignableFrom(tipo).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(GerarOrcamento))]
    [InlineData(typeof(EstornarPagamento))]
    [InlineData(typeof(CancelarOrcamento))]
    [InlineData(typeof(IniciarExecucao))]
    public void Comando_EhExcluidoDaTopologia(Type tipo)
    {
        tipo.GetCustomAttribute<ExcludeFromTopologyAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void StatusExecucaoAlterado_SerializaEnumComoString()
    {
        var json = JsonSerializer.Serialize(
            new StatusExecucaoAlterado { StatusExecucao = StatusExecucao.EmDiagnostico },
            OpcoesJsonContrato.Criar());

        json.Should().Contain("\"statusExecucao\":\"EmDiagnostico\"");
    }

    [Fact]
    public void Configurar_ComEnumConverterJaPresente_NaoDuplicaEDevolveMesmaInstancia()
    {
        var opcoes = OpcoesJsonContrato.Criar();
        var mesma = OpcoesJsonContrato.Configurar(opcoes);

        mesma.Should().BeSameAs(opcoes);
        opcoes.Converters.OfType<System.Text.Json.Serialization.JsonStringEnumConverter>().Should().HaveCount(1);
    }

    [Fact]
    public void IMensagemDaSaga_TemExcludeFromTopologyEExcludeFromImplementedTypes()
    {
        typeof(IMensagemDaSaga).GetCustomAttribute<ExcludeFromTopologyAttribute>().Should().NotBeNull();
        typeof(IMensagemDaSaga).GetCustomAttribute<ExcludeFromImplementedTypesAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void ComparadorJson_PropriedadeAusenteEquivaleANull()
    {
        ComparadorJson.Equivalentes(JsonNode.Parse("{\"a\":1}"), JsonNode.Parse("{\"a\":1,\"b\":null}")).Should().BeTrue();
        ComparadorJson.Equivalentes(JsonNode.Parse("{\"a\":1}"), JsonNode.Parse("{\"a\":2}")).Should().BeFalse();
    }

    private static class ComparadorJson
    {
        public static bool Equivalentes(JsonNode? a, JsonNode? b)
        {
            if (a is null || b is null) return a is null && b is null;
            switch (a)
            {
                case JsonObject oa when b is JsonObject ob:
                    foreach (var chave in oa.Select(p => p.Key).Union(ob.Select(p => p.Key)))
                    {
                        oa.TryGetPropertyValue(chave, out var va);
                        ob.TryGetPropertyValue(chave, out var vb);
                        if (!Equivalentes(va, vb)) return false;
                    }
                    return true;
                case JsonArray aa when b is JsonArray ab:
                    return aa.Count == ab.Count && aa.Zip(ab).All(p => Equivalentes(p.First, p.Second));
                case JsonValue va2 when b is JsonValue vb2:
                    if (va2.TryGetValue<string>(out var sa) && vb2.TryGetValue<string>(out var sb))
                        return string.Equals(sa, sb, StringComparison.Ordinal);
                    if (va2.GetValueKind() == JsonValueKind.Number && vb2.GetValueKind() == JsonValueKind.Number)
                        return decimal.Parse(va2.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture)
                            == decimal.Parse(vb2.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
                    return va2.ToJsonString() == vb2.ToJsonString();
                default:
                    return false;
            }
        }
    }
}
