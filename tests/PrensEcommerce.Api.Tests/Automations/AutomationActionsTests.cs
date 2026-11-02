using System.Text.Json.Nodes;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Automations.Engine;

namespace PrensEcommerce.Api.Tests.Automations;

public class AutomationActionsTests
{
    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult(EmailDelivery.Simulated);
        }
    }

    private static User Customer(bool acceptsMarketing = true) =>
        new() { Name = "Ana", Email = "ana@test.com", AcceptsMarketing = acceptsMarketing };

    private static AutomationRule Rule(AutomationTrigger trigger, AutomationAction action, string configuration) =>
        new() { Name = "r", Trigger = trigger, Action = action, Configuration = configuration };

    [Fact]
    public async Task SendEmail_renders_builtin_template_with_order_data()
    {
        var sender = new RecordingSender();
        var payload = new JsonObject { ["referenceId"] = Guid.Parse("01a19dd4-6438-7523-928b-b37fa490c4df"), ["amount"] = 159980m };
        var context = new AutomationContext(
            Rule(AutomationTrigger.OrderPlaced, AutomationAction.SendEmail, """{"template":"order-confirmation"}"""), Customer(), payload, "Prens");

        var outcome = await new SendEmailAction(sender).ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(AutomationRunStatus.Succeeded, outcome.Status);
        var mail = Assert.Single(sender.Sent);
        Assert.Equal("Hemos recibido tu pedido 01A19DD4", mail.Subject);
        Assert.Contains("$ 159.980", mail.Body, StringComparison.Ordinal);
        Assert.True(outcome.Result!["simulated"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SendEmail_skips_marketing_triggers_without_consent_but_not_transactional_ones()
    {
        var sender = new RecordingSender();
        var custom = """{"subject":"Hola {{nombre}}","body":"Tienes {{unidades}} productos"}""";

        var marketing = await new SendEmailAction(sender).ExecuteAsync(
            new AutomationContext(Rule(AutomationTrigger.CartAbandoned, AutomationAction.SendEmail, custom), Customer(false), [], "Prens"),
            CancellationToken.None);
        var transactional = await new SendEmailAction(sender).ExecuteAsync(
            new AutomationContext(Rule(AutomationTrigger.UserRegistered, AutomationAction.SendEmail, custom), Customer(false), [], "Prens"),
            CancellationToken.None);

        Assert.Equal(AutomationRunStatus.Skipped, marketing.Status);
        Assert.Equal(AutomationRunStatus.Succeeded, transactional.Status);
        Assert.Equal("Hola Ana", Assert.Single(sender.Sent).Subject);
    }

    [Fact]
    public async Task SendEmail_fails_when_rule_has_no_content()
    {
        var outcome = await new SendEmailAction(new RecordingSender()).ExecuteAsync(
            new AutomationContext(Rule(AutomationTrigger.OrderPlaced, AutomationAction.SendEmail, "{}"), Customer(), [], "Prens"),
            CancellationToken.None);

        Assert.Equal(AutomationRunStatus.Failed, outcome.Status);
    }

    [Fact]
    public async Task AddTag_normalizes_and_is_idempotent()
    {
        var user = Customer();
        var context = new AutomationContext(Rule(AutomationTrigger.OrderPaid, AutomationAction.AddTag, """{"tag":" VIP "}"""), user, [], "Prens");
        var action = new AddTagAction();

        var first = await action.ExecuteAsync(context, CancellationToken.None);
        var second = await action.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(AutomationRunStatus.Succeeded, first.Status);
        Assert.Equal(AutomationRunStatus.Skipped, second.Status);
        Assert.Equal(["vip"], user.Tags);
    }
}
