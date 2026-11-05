using System.ComponentModel.DataAnnotations;

namespace PrensEcommerce.Api.Features.Chatbot;

/// <summary>Configuración del asistente (sección <c>Chatbot</c> de appsettings).</summary>
public sealed class ChatbotOptions
{
    public const string SectionName = "Chatbot";

    /// <summary>"claude" (IA, necesita <c>Anthropic:ApiKey</c>) o "rules" (sin IA externa).</summary>
    [Required]
    public string Engine { get; set; } = "claude";

    /// <summary>Modelo de Claude que responde a los clientes.</summary>
    [Required]
    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>Esfuerzo de razonamiento: low (rápido y económico, suficiente para chat), medium o high.</summary>
    [RegularExpression("^(low|medium|high)$")]
    public string Effort { get; set; } = "low";

    /// <summary>Máximo de rondas de herramientas por mensaje (cada ronda es una llamada a la API).</summary>
    [Range(1, 8)]
    public int MaxToolRounds { get; set; } = 4;

    [Range(256, 8192)]
    public int MaxTokens { get; set; } = 2048;

    public bool UsesClaude => string.Equals(Engine, "claude", StringComparison.OrdinalIgnoreCase);
}
