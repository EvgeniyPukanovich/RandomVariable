using System.Collections.Generic;

namespace RandomVariable
{
    public class RandomVariableStatistic
    {
        public double? ExpectedValue { get; set; }
        public double? Variance { get; set; }
        public Dictionary<double, double> ProbabilityDistribution { get; set; }
    }
}