using System.Security.Cryptography;
using System.Text;

namespace Oficina.Adaptadores.OrdensServico.Webhooks;

// Comparação de token do webhook em tempo constante (evita timing attack) e
// fail-closed: sem token configurado, nenhuma requisição é autorizada.
public static class ValidadorTokenWebhook
{
    public static bool EhTokenValido(string? recebido, string? esperado)
    {
        if (string.IsNullOrEmpty(esperado)) return false; // fail-closed
        if (string.IsNullOrEmpty(recebido)) return false;

        var a = Encoding.UTF8.GetBytes(recebido);
        var b = Encoding.UTF8.GetBytes(esperado);
        if (a.Length != b.Length) return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
