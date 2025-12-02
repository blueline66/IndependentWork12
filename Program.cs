using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine("IndependentWork12 — PLINQ test\n");

        var sizes = new[] { 1_000_000, 5_000_000, 10_000_000 };
        var rng = new Random(12345);

        foreach (var n in sizes)
        {
            Console.WriteLine($"=== Size: {n:N0} ===");

            var data = GenerateList(n, rng);

            Console.WriteLine("Operation A: heavy transform");
            RunAndPrint(data, item => HeavyTransform(item), "OpA");

            Console.WriteLine("Operation B: primality test");
            RunAndPrint(data, item => IsProbablyPrime(item), "OpB");

            Console.WriteLine();
        }

        Console.WriteLine("=== PLINQ Side Effects Demo ===");
        var small = GenerateList(1_000_000, rng);
        SideEffectsDemo(small);

        Console.WriteLine("Done. Press any key...");
        Console.ReadKey();
    }

    static List<int> GenerateList(int n, Random rng)
    {
        var list = new List<int>(n);
        for (int i = 0; i < n; i++)
            list.Add(rng.Next(1, 1_000_001));
        return list;
    }

    static double HeavyTransform(int x)
    {
        double v = x;
        for (int i = 0; i < 8; i++)
        {
            v = Math.Sqrt(v + 1) * Math.Log(v + 2);
            v = Math.Abs(v);
        }
        return v;
    }

    static bool IsProbablyPrime(int value)
    {
        if (value < 2) return false;
        if (value % 2 == 0) return value == 2;
        int limit = (int)Math.Sqrt(value);
        for (int i = 3; i <= limit; i += 2)
            if (value % i == 0) return false;
        return true;
    }

    static void RunAndPrint<TInput, TOutput>(List<TInput> data, Func<TInput, TOutput> op, string tag)
    {
        Console.WriteLine($"Warmup {tag}...");
        var warm = data.Take(Math.Min(10_000, data.Count)).Select(op).ToArray();

        var sw = Stopwatch.StartNew();
        var resultSeq = data.Where(x => true).Select(op).ToArray();
        sw.Stop();
        Console.WriteLine($"LINQ: {sw.Elapsed.TotalSeconds:F3} s");

        sw.Restart();
        var resultPar = data.AsParallel()
                            .WithExecutionMode(System.Linq.ParallelExecutionMode.ForceParallelism)
                            .Where(x => true)
                            .Select(op)
                            .ToArray();
        sw.Stop();
        Console.WriteLine($"PLINQ: {sw.Elapsed.TotalSeconds:F3} s");

        bool sameCount = resultSeq.Length == resultPar.Length;
        Console.WriteLine($"Consistency: {(sameCount ? "OK" : "Mismatch")}\n");
    }

    static void SideEffectsDemo(List<int> data)
    {
        int incorrectCounter = 0;
        try
        {
            data.AsParallel().ForAll(x =>
            {
                if (x % 2 == 0)
                    incorrectCounter++;
            });
            Console.WriteLine($"Without sync: expected ~{data.Count(x => x % 2 == 0)}, actual: {incorrectCounter}");
        }
        catch (AggregateException ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }

        int safeCounterInterlocked = 0;
        data.AsParallel().ForAll(x =>
        {
            if (x % 2 == 0)
                Interlocked.Increment(ref safeCounterInterlocked);
        });
        Console.WriteLine($"Interlocked: {safeCounterInterlocked}");

        var counts = data.AsParallel().Aggregate(
            () => 0,
            (local, x) => local + (x % 2 == 0 ? 1 : 0),
            (left, right) => left + right,
            total => total
        );
        Console.WriteLine($"Aggregate: {counts}\n");
    }
}