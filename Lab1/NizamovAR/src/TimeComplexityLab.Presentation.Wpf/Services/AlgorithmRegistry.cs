using TimeComplexityLab.Core.Algorithms;
using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Presentation.Wpf.Services;

public static class AlgorithmRegistry
{
    public static List<IAlgorithm> CreateAll()
    {
        return new List<IAlgorithm>
        {
            // Часть I
            new ConstantFunctionAlgorithm(),
            new VectorSumAlgorithm(),
            new VectorProductAlgorithm(),
            new PolynomialNaiveAlgorithm(),
            new PolynomialHornerAlgorithm(),
            new BubbleSortAlgorithm(),
            new QuickSortAlgorithm(),
            new TimsortAlgorithm(),

            // Часть II
            new MatrixMultiplicationAlgorithm(),

            // Часть IV
            new IterativePowerAlgorithm(),
            new RecursivePowerAlgorithm(),
            new BinaryPowerAlgorithm(),

            // Индивидуальные
            new GnomeSortAlgorithm(),
            new DtwAlgorithm(),
            new HirschbergAlgorithm(),
        };
    }
}