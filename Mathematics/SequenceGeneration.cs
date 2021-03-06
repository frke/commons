﻿using System;
using System.Collections.Generic;

namespace Commons.Mathematics
{
    public static class SequenceGeneration
    {
        public static IEnumerable<double> Linspace(double from, double to, int valueCount)
        {
            if(valueCount <= 1)
                throw new ArgumentException($"{nameof(valueCount)} must be larger than 1");
            var increment = (to - from) / (valueCount - 1);
            for (int valueIdx = 0; valueIdx < valueCount; valueIdx++)
            {
                yield return from + valueIdx * increment;
            }
        }

        public static IEnumerable<double> FixedStep(double start, double end, double stepSize)
        {
            var currentValue = start;
            yield return currentValue;
            while (currentValue + stepSize < end)
            {
                currentValue += stepSize;
                yield return currentValue;
            }
        }
    }
}
