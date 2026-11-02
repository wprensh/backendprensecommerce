using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Tests.Support;

public class SupportTicketTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static SupportTicket NewTicket() =>
        new() { Number = 42, CustomerId = Guid.NewGuid(), Subject = "Mi pedido no llega" };

    [Fact]
    public void Code_is_padded_number()
    {
        Assert.Equal("TK-000042", NewTicket().Code);
    }

    [Fact]
    public void Staff_reply_waits_for_customer_and_customer_reply_reopens()
    {
        var ticket = NewTicket();

        ticket.AddMessage(Guid.NewGuid(), fromStaff: true, "Lo revisamos");
        Assert.Equal(TicketStatus.WaitingCustomer, ticket.Status);

        ticket.AddMessage(ticket.CustomerId, fromStaff: false, "Gracias");
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(2, ticket.Messages.Count);
    }

    [Fact]
    public void Resolving_sets_ClosedAt_and_enables_rating_once()
    {
        var ticket = NewTicket();
        Assert.False(ticket.CanBeRated);

        ticket.ChangeStatus(TicketStatus.Resolved, Now);
        Assert.Equal(Now, ticket.ClosedAt);
        Assert.True(ticket.CanBeRated);

        ticket.SatisfactionScore = 5;
        Assert.False(ticket.CanBeRated);
    }

    [Fact]
    public void Reopening_clears_ClosedAt()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Resolved, Now);

        ticket.AddMessage(ticket.CustomerId, fromStaff: false, "Sigue sin llegar");

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ClosedAt);
    }

    [Fact]
    public void Internal_note_does_not_change_status()
    {
        var ticket = NewTicket();
        ticket.AddMessage(Guid.NewGuid(), fromStaff: true, "Revisar con almacén", isInternal: true);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.True(ticket.Messages.Single().IsInternal);
    }
}
