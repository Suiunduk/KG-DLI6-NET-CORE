using System;
using System.Collections.Generic;

namespace KG_DLI6_NET_CORE.Models
{
    public class AgeSexCoefficientData
    {
        public Dictionary<int, Dictionary<int, double>> MalePopulation { get; set; }
        public Dictionary<int, Dictionary<int, double>> FemalePopulation { get; set; }
        public Dictionary<int, Dictionary<int, double>> MaleVisits { get; set; }
        public Dictionary<int, Dictionary<int, double>> FemaleVisits { get; set; }
    }

    public class AgeSexCoefficientResult
    {
        public Dictionary<int, double> MaleCoefficients { get; set; }
        public Dictionary<int, double> FemaleCoefficients { get; set; }
        public Dictionary<int, Dictionary<string, double>> CombinedCoefficients { get; set; }
        public List<AgeSexCoefficient> AllCoefficients { get; set; }
        public double VisitsPerCapita { get; set; }
    }
} 