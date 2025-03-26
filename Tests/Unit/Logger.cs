using FileInitializer;
using OwlTree;

namespace Unit;

public class LoggerTests
{
    [Fact]
    public void SimpleWrite()
    {
        Logs.InitPath("logs/Logger/SimpleWrite");
        Logs.InitFiles("logs/Logger/SimpleWrite/out.log");

        var logger = new Logger((str) => File.AppendAllText("logs/Logger/SimpleWrite/out.log", str), Logger.Includes().All());

        logger.Write("aaa");
        logger.Write("bbb");
        logger.Write("ccc");

        var text = File.ReadAllText("logs/Logger/SimpleWrite/out.log");

        Assert.True(text != null, "Log file failed to be read");

        var lines = text.Split("\n");

        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 5)
                Assert.True(lines[i] == "aaa", $"6th line is not 'aaa', is instead '{lines[i]}'");
            if (i == 9)
                Assert.True(lines[i] == "bbb", $"10th line is not 'bbb', is instead '{lines[i]}'");
            if (i == 13)
                Assert.True(lines[i] == "ccc", $"14th line is not 'ccc', is instead '{lines[i]}'");
        }
    }
}