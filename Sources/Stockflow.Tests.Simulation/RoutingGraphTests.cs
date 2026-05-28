using Stockflow.Simulation.Component;
using Stockflow.Simulation.Grid;
using Stockflow.Simulation.Routing;
using Stockflow.Tests.Simulation.Helpers;

namespace Stockflow.Tests.Simulation;

public class RoutingGraphTests
{
    [Fact]
    public void Connect_SamePortTwice_ThrowsInvalidOperation()
    {
        var graph = new RoutingGraph();
        var compA = new StubComponent(1, new GridCoord(0, 0));
        var compB = new StubComponent(2, new GridCoord(1, 0));
        var compC = new StubComponent(3, new GridCoord(2, 0));
        var port  = new PortId(1);

        graph.Connect(compA, port, compB, new PortId(0));

        Assert.Throws<InvalidOperationException>(
            () => graph.Connect(compA, port, compC, new PortId(0)));
    }

    [Fact]
    public void Connect_AfterDisconnect_Succeeds()
    {
        var graph = new RoutingGraph();
        var compA = new StubComponent(1, new GridCoord(0, 0));
        var compB = new StubComponent(2, new GridCoord(1, 0));
        var compC = new StubComponent(3, new GridCoord(2, 0));
        var port  = new PortId(1);

        graph.Connect(compA, port, compB, new PortId(0));
        graph.Disconnect(compA, port);
        graph.Connect(compA, port, compC, new PortId(0)); // must not throw

        Assert.Equal(compC, graph.GetNext(compA, port)!.Value.To);
    }
}
