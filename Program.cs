using System;
using System.Collections.Generic;
using System.Text;

namespace RandomVariable
{
    class Program
    {
        public static void Main()
        {
            RandomVariableStatisticCalculator randomVariableStatisticCalculator = new RandomVariableStatisticCalculator();
            var es = randomVariableStatisticCalculator.CalculateStatistic("-2d3+1d4",StatisticKind.Variance);
            Console.WriteLine(es.Variance);
        }
    }
}
