namespace RandomVariable
{
    public interface IRandomVariableStatisticCalculator
    {
        RandomVariableStatistic CalculateStatistic(string expression, params StatisticKind[] statisticForCalculate);
    }
}