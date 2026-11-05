using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Chatbot;
using PrensEcommerce.Api.Features.Chatbot.Escalate;
using PrensEcommerce.Api.Features.Chatbot.SendMessage;

namespace PrensEcommerce.Api.Tests.Chatbot;

public class ChatbotTests
{
    [Theory]
    [InlineData("Hola", ChatIntent.Greeting)]
    [InlineData("¡Buenos días!", ChatIntent.Greeting)]
    [InlineData("¿Dónde está mi pedido?", ChatIntent.OrderStatus)]
    [InlineData("estado de pr-20261102-7k3qzd", ChatIntent.OrderStatus)]
    [InlineData("¿Cuántos puntos tengo?", ChatIntent.Loyalty)]
    [InlineData("¿Cuánto cuesta el envío?", ChatIntent.Shipping)]
    [InlineData("¿Puedo pagar con tarjeta?", ChatIntent.Payment)]
    [InlineData("Quiero devolver un producto", ChatIntent.Returns)]
    [InlineData("Quiero hablar con un asesor", ChatIntent.HumanAgent)]
    [InlineData("¿Tienen audífonos bluetooth?", ChatIntent.ProductSearch)]
    [InlineData("Quiero comprar un ordenador", ChatIntent.ProductSearch)]
    [InlineData("lámpara de escritorio", ChatIntent.ProductSearch)]
    [InlineData("Muchas gracias", ChatIntent.Thanks)]
    [InlineData("¿Qué puedes hacer?", ChatIntent.Help)]
    [InlineData("???", ChatIntent.Unknown)]
    public void Classifies_common_questions(string message, ChatIntent expected)
    {
        Assert.Equal(expected, ChatIntentClassifier.Classify(message));
    }

    [Fact]
    public void Search_terms_drop_filler_words_accents_and_plurals()
    {
        Assert.Equal(["audifono", "bluetooth"], ChatIntentClassifier.SearchTerms("¿Tienen audífonos Bluetooth baratos?"));
        Assert.Equal(["lampara", "escritorio"], ChatIntentClassifier.SearchTerms("Busco lámparas de escritorio"));
        Assert.Empty(ChatIntentClassifier.SearchTerms("¿Qué ofertas tienen?"));
    }

    [Fact]
    public void Order_number_is_found_in_any_case()
    {
        Assert.Equal("PR-20261102-7K3QZD", ChatIntentClassifier.OrderNumber("mi pedido pr-20261102-7k3qzd no llega"));
        Assert.Null(ChatIntentClassifier.OrderNumber("mi pedido no llega"));
    }

    [Fact]
    public void Search_pattern_tolerates_accents()
    {
        Assert.Equal("%__d_f___%", SendChatMessage.RulesAssistant.AccentTolerantPattern("audifono"));
        Assert.Equal("%l_mp_r_%", SendChatMessage.RulesAssistant.AccentTolerantPattern("lampara"));
    }

    [Fact]
    public void Escalation_builds_ticket_with_transcript_and_category()
    {
        var command = new EscalateChat.Command(
        [
            new(FromBot: true, "¡Hola! ¿En qué te ayudo?"),
            new(FromBot: false, "Mi pedido no llega"),
            new(FromBot: true, "Tu pedido está enviado."),
        ], ChatIntent.OrderStatus);

        var ticket = EscalateChat.Handler.BuildTicket(command);

        Assert.Equal("Chat: Mi pedido no llega", ticket.Subject);
        Assert.Equal(TicketCategory.Order, ticket.Category);
        Assert.Contains("Cliente: Mi pedido no llega", ticket.Message);
        Assert.Contains("Asistente: Tu pedido está enviado.", ticket.Message);
    }

    [Fact]
    public void Escalation_subject_is_truncated()
    {
        var longQuestion = new string('x', 200);
        var ticket = EscalateChat.Handler.BuildTicket(new([new(false, longQuestion)], null));

        Assert.Equal(TicketCategory.Other, ticket.Category);
        Assert.True(ticket.Subject.Length <= 200);
        Assert.EndsWith("…", ticket.Subject);
    }

    [Fact]
    public void Status_labels_are_in_spanish()
    {
        Assert.Equal("Enviado", SendChatMessage.RulesAssistant.StatusLabel(OrderStatus.Shipped));
    }
}

public class ClaudeConversationTests
{
    [Fact]
    public void Conversation_starts_with_user_alternates_and_ends_with_new_message()
    {
        var command = new SendChatMessage.Command("¿y en azul?",
        [
            new(FromBot: true, "¡Hola! Soy el asistente."),
            new(FromBot: false, "Busco audífonos"),
            new(FromBot: true, "Encontré estos productos."),
            new(FromBot: true, "¿Algo más?"),
        ]);

        var messages = ClaudeAssistant.BuildConversation(command);

        Assert.Equal(3, messages.Count);
        Assert.IsType<Anthropic.Models.Messages.UserMessageParam>(messages[0].Value);
        Assert.IsType<Anthropic.Models.Messages.AssistantMessageParam>(messages[1].Value);
        Assert.IsType<Anthropic.Models.Messages.UserMessageParam>(messages[2].Value);
    }

    [Fact]
    public void Conversation_without_history_is_the_message()
    {
        var messages = ClaudeAssistant.BuildConversation(new SendChatMessage.Command("hola"));
        Assert.Single(messages);
    }
}
