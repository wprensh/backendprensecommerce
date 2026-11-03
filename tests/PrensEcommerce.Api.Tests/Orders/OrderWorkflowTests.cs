using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Orders.Admin;

namespace PrensEcommerce.Api.Tests.Orders;

public sealed class OrderWorkflowTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void Allows_forward_transitions(OrderStatus from, OrderStatus to) =>
        Assert.True(OrderWorkflow.CanMove(from, to));

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    [InlineData(OrderStatus.Paid, OrderStatus.Paid)]
    public void Rejects_skips_backward_moves_and_final_states(OrderStatus from, OrderStatus to) =>
        Assert.False(OrderWorkflow.CanMove(from, to));

    [Fact]
    public void Every_status_has_a_spanish_label_and_transition_entry()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            Assert.NotEqual(status.ToString(), OrderWorkflow.Label(status));
            _ = OrderWorkflow.Next(status);
        }
    }
}
