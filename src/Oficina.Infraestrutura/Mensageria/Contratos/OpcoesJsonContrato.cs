using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

/// <summary>Opcoes de serializacao JSON do contrato das mensagens da saga (camelCase, enums como string).</summary>
public static class OpcoesJsonContrato
{
    public static JsonSerializerOptions Criar() =>
        Configurar(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configurar(JsonSerializerOptions opcoes)
    {
        if (!opcoes.Converters.OfType<JsonStringEnumConverter>().Any())
            opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }
}
