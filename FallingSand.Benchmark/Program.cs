using Rubedo.Benchmark;

namespace Rubedo;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRun.Prepare();
        BenchmarkRun.Run();
    }
}