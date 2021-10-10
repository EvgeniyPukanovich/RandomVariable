using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RandomVariable
{
    public class RandomVariableStatisticCalculator : IRandomVariableStatisticCalculator
    {
        private string unaryMinus = "$";
        public RandomVariableStatisticCalculator()
        {
        }

        public RandomVariableStatistic CalculateStatistic(string expression, params StatisticKind[] statisticForCalculate)
        {
            double expectedValue = 0;
            double variance = 0;
            Dictionary<double, double> probabilityDistribution = null;

            if (statisticForCalculate.Contains(StatisticKind.ExpectedValue))
                expectedValue = GetExpectedValue(expression);

            if (statisticForCalculate.Contains(StatisticKind.ProbabilityDistribution) || statisticForCalculate.Contains(StatisticKind.Variance))
            {
                Parser.unaryMinus = unaryMinus;
                string simplifiedExpr = Parser.Parse(expression).Eval();
                if (statisticForCalculate.Contains(StatisticKind.Variance))
                    variance = GetVariance(simplifiedExpr.Replace("-", "+").Replace(unaryMinus, ""));
                if (statisticForCalculate.Contains(StatisticKind.ProbabilityDistribution))
                    probabilityDistribution = GetProbabylityDistribution(simplifiedExpr);
            }

            return new RandomVariableStatistic() { ExpectedValue = expectedValue, Variance = variance, ProbabilityDistribution = probabilityDistribution };
        }

        private Dictionary<double, double> GetProbabylityDistribution(string expression)
        {
            string pattern = @"(\+)|(\-)";
            string[] terms = Regex.Split(expression, pattern);
            List<string> randomVars = new List<string>();
            //separate numbers and random vars
            for (int i = 0; i < terms.Length; i++)
            {
                if (terms[i].Contains("d"))
                {
                    randomVars.Add(terms[i]);
                    terms[i] = "0";
                }
            }

            DataTable dt = new DataTable();
            //evaluate terms
            var objValue = dt.Compute(string.Join("", terms), "");
            double addition = Convert.ToDouble(objValue);

            List<List<(double val, double prob)>> probs = new List<List<(double val, double prob)>>();

            foreach (var randomVar in randomVars)
                probs.Add(GetProbDistrOfVarWithCoefs(randomVar));

            List<(double val, double prob)> prob = probs[0];

            for (int i = 1; i < probs.Count; i++)
                prob = GetProbabilityDistributionOfTwoFuncs(prob, probs[i]);

            for (int i = 0; i < prob.Count; i++)
                prob[i] = (prob[i].val + addition, prob[i].prob);

            Dictionary<double, double> probabylityDistribution = new Dictionary<double, double>();
            foreach (var item in prob)
                probabylityDistribution.Add(item.val, item.prob);

            return probabylityDistribution;
        }

        private List<(double val, double prob)> GetProbabilityDistributionOfTwoFuncs(List<(double val, double prob)> prob1,
            List<(double val, double prob)> prob2)
        {
            //we can sum probability distributions by formula given in this book:
            //https://www.sciencedirect.com/science/article/pii/S089571770500004X
            double lowestVal = double.MaxValue;

            for (int i = 0; i < prob1.Count; i++)
            {
                if (prob1[i].val < lowestVal)
                    lowestVal = prob1[i].val;
            }

            for (int i = 0; i < prob2.Count; i++)
            {
                if (prob2[i].val < lowestVal)
                    lowestVal = prob2[i].val;
            }
            //all possible sums of values
            HashSet<double> supportValues = new HashSet<double>();

            for (int i = 0; i < prob1.Count; i++)
            {
                for (int j = 0; j < prob2.Count; j++)
                    supportValues.Add(prob1[i].val + prob2[j].val);
            }

            List<(double val, double prob)> sumProb = new List<(double val, double prob)>();

            foreach (var item in supportValues)
            {
                double prob = GetProbabilityOfTwoVars(item, lowestVal, prob1, prob2);
                sumProb.Add((item, prob));
            }

            return sumProb;
        }

        private double GetProbabilityOfTwoVars(double value, double lowestValue,
            List<(double val, double prob)> prob1, List<(double val, double prob)> prob2)
        {
            //according to formula
            double upper = value - lowestValue;
            int currentUpper = (int)upper;
            double res = 0;

            for (int i = (int)lowestValue; i <= upper; i++)
            {
                double probability1 = GetValueProbability(i, prob1);
                double probability2 = GetValueProbability(currentUpper, prob2);

                if (probability1 != 0 && probability2 != 0)
                    res += probability1 * probability2;

                currentUpper--;
            }
            return res;
        }

        private double GetValueProbability(double value, List<(double val, double prob)> prob)
        {
            foreach (var item in prob)
            {
                if (item.val == value)
                    return item.prob;
            }
            return 0;
        }

        private List<(double val, double prob)> GetProbDistrOfVarWithCoefs(string randomVar)
        {
            string pattern = @"(\*)|(/)";
            string[] mults = Regex.Split(randomVar.Replace(unaryMinus, "-1*"), pattern);
            List<string> rawCoef = new List<string>();
            string randVar = "";
            //separate coefs and random vriable
            for (int i = 0; i < mults.Length; i++)
            {
                if (mults[i].Contains('d'))
                {
                    randVar = mults[i];
                    if (i != 0)
                        rawCoef.RemoveAt(i - 1);
                    continue;
                }
                rawCoef.Add(mults[i]);
            }

            List<(double val, double prob)> results = GetProbDistrOFRandVar(randVar);

            DataTable dt = new DataTable();
            //evaluate coefficients
            string coef = string.Join("", rawCoef);
            double resCoef = 1;
            if (coef != "")
            {
                var objValue = dt.Compute(coef, "");
                resCoef = Convert.ToDouble(objValue);
            }
            //change final results by multiplying values by coefs
            for (int i = 0; i < results.Count; i++)
                results[i] = (results[i].val * resCoef, results[i].prob);

            return results;
        }

        private List<(double val, double prob)> GetProbDistrOFRandVar(string randVar)
        {
            string[] raw = randVar.Split('d');
            int throws = int.Parse(raw[0]);
            int sides = int.Parse(raw[1]);
            int upperBound = throws * sides;
            int lowerBound = throws;

            List<(double val, double prob)> results = new List<(double val, double prob)>();
            double totalOutcomes = Math.Pow(sides, throws);

            while (lowerBound <= upperBound)
            {
                double waysToGet = GetWaysToObtainValue(lowerBound, throws, sides);
                double prob = waysToGet / totalOutcomes;

                //we use symmetry here (normal distribution)
                results.Add((upperBound, prob));
                if (upperBound != lowerBound)
                    results.Add((lowerBound, prob));

                lowerBound++;
                upperBound--;
            }

            return results;
        }

        private double GetWaysToObtainValue(int value, int throws, int sides)
        {
            //this is the implementation of formula given here:
            //https://stats.stackexchange.com/questions/3614/how-to-easily-determine-the-results-distribution-for-multiple-dice
            int k = 0;
            double res = 0;
            while (sides * k <= value - throws)
            {
                for (int j = 0; j <= value - throws; j++)
                {
                    if (sides * k + j == value - throws)
                    {
                        res += GetCombinations(throws, k) * GetCombinations(-throws, j) * Math.Pow(-1, j + k);
                    }
                }
                k++;
            }
            return res;
        }

        private double GetCombinations(int n, int k)
        {
            double val = 1;
            for (int i = 0; i < k; i++)
                val *= (double)(n - i) / (i + 1);
            return val;
        }

        private double GetExpectedValue(string expression)
        {
            //set culture for decimal point
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            //get random variables from expression
            List<string> randomVariables = expression.Split(new char[] { '+', '-', '*', '/', '(', ')', '.' }).Where(x => x.Contains('d')).ToList();
            string resExpr = expression;

            foreach (var randomVar in randomVariables)
            {
                string[] raw = randomVar.Split('d');
                double throws = int.Parse(raw[0]);
                double sides = int.Parse(raw[1]);
                double fraction = throws / sides;

                double res = 0;
                //get expected value for this random variable
                for (int i = 1; i <= sides; i++)
                    res += i * fraction;
                //replace random variable with its expected value
                resExpr = resExpr.Replace(randomVar, res.ToString());
            }
            DataTable dataTable = new DataTable();
            //evaluate our new expression
            var objValue = dataTable.Compute(resExpr, "");
            double expVal = Convert.ToDouble(objValue);
            return expVal;
        }

        private double GetVariance(string expression)
        {
            //set culture for decimal point
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string pattern = @"(\+)|(-)";

            string[] terms = Regex.Split(expression, pattern);
            string res = "";

            for (int i = 0; i < terms.Length; i++)
            {
                //sign
                if (terms[i] == "+" || terms[i] == "-")
                    res += terms[i];
                //number
                else if (!terms[i].Contains("d"))
                    res += 0;
                //random variable with coefs
                else
                {
                    pattern = @"(\*)|(/)";
                    string[] mults = Regex.Split(terms[i], pattern);

                    for (int j = 0; j < mults.Length; j++)
                    {
                        //sign
                        if (mults[j] == "*" || mults[j] == "/")
                            res += mults[j];
                        //number
                        else if (!mults[j].Contains("d"))
                        {
                            double number = double.Parse(mults[j]);
                            //square coefs according to formula
                            res += number * number;
                        }
                        //random variable
                        else
                            res += GetRandomVariableVariance(mults[j]);
                    }
                }
            }

            //evaluate our expression
            DataTable dataTable = new DataTable();
            return Convert.ToDouble(dataTable.Compute(res, ""));
        }

        private double GetRandomVariableVariance(string variable)
        {
            string[] raw = variable.Split('d');
            double throws = int.Parse(raw[0]);
            double sides = int.Parse(raw[1]);

            //See this for explanation https://boardgamegeek.com/blogpost/25470/variance-dice-sums
            double expValue = 0;
            //calculate expected value
            for (int k = 1; k <= sides; k++)
                expValue += k * 1 / sides;

            double variance = 0;
            //calculate variance
            for (int k = 1; k <= sides; k++)
                variance += (k - expValue) * (k - expValue) / sides;

            return variance * throws;
        }
    }
}