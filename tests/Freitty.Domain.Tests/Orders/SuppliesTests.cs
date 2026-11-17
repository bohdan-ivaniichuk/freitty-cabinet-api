using Freitty.Domain.Catalog;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class SuppliesTests
{
    [Fact]
    public void SupplyLineCopiesTheItemsPrice()
    {
        Order order = TestOrders.Ready();
        var stretchWrap = new SupplyItem("WRAP-01", "Packaging", 12.50m);

        order.AddSupply(stretchWrap, 3, TestOrders.At(1)).MustSucceed();

        SupplyLine line = Assert.Single(order.SupplyLines);
        Assert.Equal(stretchWrap.Id, line.SupplyItemId);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(12.50m, line.UnitPrice);
    }

    [Fact]
    public void LineTotalIsQuantityTimesUnitPrice()
    {
        Order order = TestOrders.Ready();

        order.AddSupply(new SupplyItem("WRAP-01", "Packaging", 12.50m), 3, TestOrders.At(1)).MustSucceed();

        Assert.Equal(37.50m, order.SupplyLines.Single().LineTotal);
    }

    [Fact]
    public void SupplySubtotalAddsUpTheLines()
    {
        Order order = TestOrders.Ready();

        order.AddSupply(new SupplyItem("WRAP-01", "Packaging", 12.50m), 3, TestOrders.At(1)).MustSucceed();
        order.AddSupply(new SupplyItem("CORNER-02", "Protection", 2.25m), 4, TestOrders.At(2)).MustSucceed();

        Assert.Equal(46.50m, order.SupplySubtotal);
    }

    [Fact]
    public void SameItemCanBeAddedTwice()
    {
        Order order = TestOrders.Ready();
        var stretchWrap = new SupplyItem("WRAP-01", "Packaging", 12.50m);
        order.AddSupply(stretchWrap, 3, TestOrders.At(1)).MustSucceed();

        Result result = order.AddSupply(stretchWrap, 2, TestOrders.At(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 3, 2 }, order.SupplyLines.Select(line => line.Quantity));
        Assert.All(order.SupplyLines, line => Assert.Equal(stretchWrap.Id, line.SupplyItemId));
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.InProgress)]
    public void SupplyIsAddedInAnyStatusButCompleted(OrderStatus status)
    {
        Order order = TestOrders.InStatus(status);

        Result result = order.AddSupply(new SupplyItem("WRAP-01", "Packaging", 12.50m), 1, TestOrders.At(1));

        Assert.True(result.IsSuccess);
        Assert.Single(order.SupplyLines);
    }

    [Fact]
    public void AddingSupplyDoesNotStartAReadyOrder()
    {
        Order order = TestOrders.Ready();

        order.AddSupply(new SupplyItem("WRAP-01", "Packaging", 12.50m), 1, TestOrders.At(1)).MustSucceed();

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Null(order.StartedAt);
    }

    [Fact]
    public void SupplyIsRefusedOnACompletedOrder()
    {
        Order order = TestOrders.Completed();

        Result result = order.AddSupply(new SupplyItem("WRAP-01", "Packaging", 12.50m), 1, TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "order.already_completed");
        Assert.Empty(order.SupplyLines);
    }
}
