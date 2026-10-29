using System.Diagnostics;

namespace FractalPlatform.Domain.Algorithms;

public static class RecursiveAlgorithms
{
    // 1. Ханойская башня (Задача Реве)
    public static int Hanoi(int n, char from, char to, char aux, ref long steps)
    {
        steps++;
        if (n <= 0) return 0;
        Hanoi(n - 1, from, aux, to, ref steps);
        Hanoi(n - 1, aux, to, from, ref steps);
        return (int)steps;
    }

    // 2. Функция Tak
    public static int Tak(int x, int y, int z, ref long steps)
    {
        steps++;
        if (y >= x) return z;
        return Tak(
            Tak(x - 1, y, z, ref steps),
            Tak(y - 1, z, x, ref steps),
            Tak(z - 1, x, y, ref steps),
            ref steps
        );
    }

    // 3. Последовательность Хофштадтера (Q)
    public static int Hofstadter(int n, ref long steps)
    {
        steps++;
        if (n <= 0) return 0;
        if (n == 1) return 1;
        return Hofstadter(n - Hofstadter(n - 1, ref steps), ref steps)
             + Hofstadter(n - 2, ref steps);
    }

    // Метод для запуска бенчмарка
    public static object Benchmark(string algo, int n)
    {
        var sw = Stopwatch.StartNew();
        long steps = 0;

        try
        {
            if (algo == "hanoi") Hanoi(n, 'A', 'C', 'B', ref steps);
            else if (algo == "tak") Tak(n * 3, n * 2, n, ref steps);
            else if (algo == "hofstadter") Hofstadter(n, ref steps);
        }
        catch (StackOverflowException)
        {
            return new { algo, n, error = "Stack Overflow", steps, timeMs = -1 };
        }

        sw.Stop();
        return new { algo, n, steps, timeMs = sw.Elapsed.TotalMilliseconds };
    }

    public static void Reve(int n, char from, char to, char aux1, char aux2, ref long steps)
    {
        steps++;
        if (n <= 0) return;
        int k = n / 2;
        Reve(k, from, aux1, aux2, to, ref steps);
        Hanoi(n - k, from, to, aux2, ref steps); // Используем старый метод Hanoi для 3 стержней
        Reve(k, aux1, to, from, aux2, ref steps);
    }

    public static int Ackermann(int m, int n, ref long steps)
    {
        steps++;
        if (m == 0) return n + 1;
        if (m > 0 && n == 0) return Ackermann(m - 1, 1, ref steps);
        return Ackermann(m - 1, Ackermann(m, n - 1, ref steps), ref steps);
    }
}