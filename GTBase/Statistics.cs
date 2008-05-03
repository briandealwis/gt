/*
* Copyright (c) 2003, the JUNG Project and the Regents of the University 
* of California
* All rights reserved.
*
* This software is open-source under the BSD license; see either
* "license.txt" or
* http://jung.sourceforge.net/license.txt for a description.
*/

// This code was ported and lightly C#'d from JUNG 1.7.6's
// edu.uci.ics.jung.statistics.StatisticalMoments
// This could be implemented in terms of properties
// and overloaded += methods.

using System;
namespace GT.Utils
{

    /// <summary>
    /// <para>A data structure representing the central moments of a distribution
    /// including:</para>
    /// <ul>
    /// <li> the mean </li>
    /// <li> the Variance </li>
    /// <li> the skewness</li>
    /// <li> the kurtosis </li>
    /// </ul>
    /// <para>Data values that are observed are passed into this data structure
    /// via the <see cref="Accumulate(double)">Accumulate</see> method and the
    /// corresponding central moments are updated on each call.</para>
    ///
    /// <para>Author: Didier H. Besset (modified by Scott White)</para>
    /// </summary>
    public class StatisticalMoments
    {
        /// <summary>
        /// Vector containing the points.
        /// </summary>
        protected double[] moments;

        /// <summary>
        /// Default constructor methods: declare space for 5 moments.
        /// </summary>
        public StatisticalMoments() : this(5) { }

        /// <summary>
        /// General constructor methods intended for subclasses needing
        /// more than 5 moments.  Implementors will need to modify
        /// <see cref="Accumulate(double)">Accumulate()</see> in particular.
        /// </summary>
        /// <param name="n">number of moments to accumulate.</param>
        protected StatisticalMoments(int n)
        {
            moments = new double[n];
            Reset();

        }

        /// <summary>
        /// Reset all counters.
        /// </summary>
        public void Reset()
        {
            for (int n = 0; n < moments.Length; n++)
            {
                moments[n] = 0;
            }
        }

        /// <summary>
        /// Statistical moment accumulation up to order 4.
        /// </summary>
        /// <param name="x">value to accumulate</param>
        /// <remarks>Could override "+=" instead...</remarks>
        public void Accumulate(double x)
        {
            double n = moments[0];
            double n1 = n + 1;
            double n2 = n * n;
            double delta = (moments[1] - x) / n1;
            double d2 = delta * delta;
            double d3 = delta * d2;
            double r1 = (double)n / (double)n1;
            moments[4] += 4 * delta * moments[3] + 6 * d2 * moments[2]
                    + (1 + n * n2) * d2 * d2;
            moments[4] *= r1;
            moments[3] += 3 * delta * moments[2] + (1 - n2) * d3;
            moments[3] *= r1;
            moments[2] += (1 + n) * d2;
            moments[2] *= r1;
            moments[1] -= delta;
            moments[0] = n1;
            return;
        }

        /// <summary>
        /// Returns the number of accumulated counts.
        /// </summary>
        public long Count()
        {
            return (long)moments[0];
        }

        /// <summary>
        /// Returns the average.
        /// </summary>
        public double Average()
        {
            return moments[1];
        }

        /// <summary>
        /// Returns the error on average.
        /// May return NaN if there have not been sufficient number of
        /// values accumulated.
        /// </summary>
        /// <exception cref="System.DivideByZeroException">
        /// If no values have been accumulated.
        /// </exception>
        public double ErrorOnAverage()
        {
            return Math.Sqrt(Variance() / moments[0]);
        }

        /// <summary>
        /// The kurtosis measures the sharpness of the distribution near
        /// the maximum.
        /// May return NaN if there have not been sufficient number of
        /// values accumulated.
        /// Note: The kurtosis of the Normal distribution is 0 by definition.
        /// Note: this is the estimator of the population kurtosis,
        ///     and not the sample kurtosis.
        /// </summary>
        public double Kurtosis()
        {
            if (moments[0] < 4) { return Double.NaN; }
            double kFact = (moments[0] - 2) * (moments[0] - 3);
            double n1 = moments[0] - 1;
            double v = Variance();
            return (moments[4] * moments[0] * moments[0] * (moments[0] + 1)
                    / (v * v * n1) - n1 * n1 * 3) / kFact;
        }

        /// <summary>
        /// The skewness of the data.
        /// May return NaN if there have not been sufficient number of
        /// values accumulated.
        /// Note: this is the estimator of the population skewness,
        ///     and not the sample skewness.
        /// </summary>
        public double Skewness()
        {
            if (moments[0] < 3) { return Double.NaN; }
            double v = Variance();
            return moments[3] * moments[0] * moments[0]
                    / (Math.Sqrt(v) * v * (moments[0] - 1)
                    * (moments[0] - 2));
        }

        /// <summary>
        /// Returns the standard deviation.
        /// May return NaN if there have not been sufficient number of
        /// values accumulated.
        /// </summary>
        public double StandardDeviation()
        {
            return Math.Sqrt(Variance());
        }

        /// <summary>
        /// Returns the unormalized standard deviation.
        /// May return NaN if there have not been sufficient number of
        /// values accumulated.
        /// </summary>
        public double UnnormalizedVariance()
        {
            return moments[2] * moments[0];
        }

        /// <summary>
        /// Note: the Variance includes the Bessel correction factor.
        /// </summary>
        public double Variance()
        {
            if (moments[0] < 2) { return Double.NaN; }
            return UnnormalizedVariance() / (moments[0] - 1);
        }
    }
}
