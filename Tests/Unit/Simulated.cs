using OwlTree;

namespace Unit;

public class SimulatedTests
{
    [Fact]
    public void TestSetAndGet()
    {
        Simulator s = new();
        s.curTick = new Tick(1);

        Simulated<int> a = new();
        a.Initialize(5, s);

        a.Value = 10;

        Assert.True(a.Value == 10, "Simulated value did not equal 10, got " + a.Value.ToString() + " instead");
    }

    [Fact]
    public void TestForwardSimulation()
    {
        Simulator s = new();
        s.curTick = new Tick(1);

        Simulated<int> a = new();
        a.Initialize(5, s);

        a.Value = 10;
        s.curTick = s.curTick.Next();
        Assert.True(a.Value == 10, "Simulated value did not equal 10, got " + a.Value.ToString() + " instead");

        a.Value += 1;
        s.curTick = s.curTick.Next();
        Assert.True(a.Value == 11, "Simulated value did not equal 11, got " + a.Value.ToString() + " instead");

        a.Value += 1;
        s.curTick = s.curTick.Next();
        Assert.True(a.Value == 12, "Simulated value did not equal 12, got " + a.Value.ToString() + " instead");
    }

    [Fact]
    public void ResimulationTest()
    {
        Simulator s = new();
        s.curTick = new Tick(1);

        Simulated<int> a = new();
        a.Initialize(5, s);

        a.Value = 10;
        s.curTick = s.curTick.Next();
        Assert.True(a.Value == 10, "Simulated value did not equal 10, got " + a.Value.ToString() + " instead");

        a.Value += 1;
        s.curTick = s.curTick.Next();
        Assert.True(a.Value == 11, "Simulated value did not equal 11, got " + a.Value.ToString() + " instead");

        a.Value += 1;
        s.curTick = s.curTick.Next();
        Assert.True(a.Value == 12, "Simulated value did not equal 12, got " + a.Value.ToString() + " instead");

        s.Resimulate(new Tick(2));
        Assert.True(a.Value == 10, "Simulated value did not equal 10, got " + a.Value.ToString() + " instead");
        s.Resimulate(new Tick(1));
        Assert.True(a.Value == 0, "Simulated value did not equal 0, got " + a.Value.ToString() + " instead");
    }
}

public class Simulator : ISimulator
{
    public event Tick.Delegate? OnResimulation;

    public void Resimulate(Tick t)
    {
        curTick = t;
        OnResimulation?.Invoke(curTick);
    }

    void ISimulator.AddSimulated(ISimulated simulated)
    {
        OnResimulation += simulated.OnResimulation;
    }

    public Tick curTick;

    Tick ISimulator.GetPresentTick()
    {
        return curTick;
    }

    void ISimulator.RemoveSimulated(ISimulated simulated)
    {
        OnResimulation -= simulated.OnResimulation;
    }
}