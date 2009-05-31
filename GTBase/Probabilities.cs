//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;

namespace GT
{
    /// <summary>
    /// A random number generator for gaussian values.
    /// This generator uses the polar transformation of the Box-Muller transformation, 
    /// as described at http://www.taygeta.com/random/gaussian.html.
    /// </summary>
    public class GaussianRandomNumberGenerator
    {
        /// <summary>
        /// The generator of uniform random values
        /// </summary>
        protected Random uniformGenerator;

        /// <summary>
        /// Track the usage status of the n1 and n2 values.  If true, then
        /// they must be regenerated; if false, then n2 is still valid.
        /// </summary>
        protected bool mustGenerate = true;

        /// <summary>
        /// The mean and standard deviations
        /// </summary>
        protected double mean, deviation;

        /// <summary>
        /// The two sample values.
        /// </summary>
        protected double n1, n2;

        /// <summary>
        /// Create a new instance with specified mean and standard deviation.
        /// </summary>
        /// <param name="mean">the mean</param>
        /// <param name="deviation">the standard deviation</param>
        public GaussianRandomNumberGenerator(double mean, double deviation) 
            : this(mean, deviation, new Random()) { }

        /// <summary>
        /// Create a new instance with specified mean and standard deviation
        /// using a configured generator of uniform random values.
        /// </summary>
        /// <param name="mean">the mean</param>
        /// <param name="deviation">the standard deviation</param>
        /// <param name="uniformGenerator">a generator of uniform random values</param>
        public GaussianRandomNumberGenerator(double mean, double deviation, 
            Random uniformGenerator)
        {
            this.mean = mean;
            this.deviation = deviation;
            this.uniformGenerator = uniformGenerator;
        }

        /// <summary>
        /// Return a random value on N(mean, deviation).
        /// </summary>
        /// <returns></returns>
        public double NextDouble()
        {
            if(mustGenerate)
            {
                GenerateGaussianSamples();
                mustGenerate = false;
                return n1;
            }
            mustGenerate = true;
            return n2;
        }

        /// <summary>
        /// Gnerate two gaussian samples.
        /// </summary>
        protected void GenerateGaussianSamples()
        {
            // NB: System.Random.NextDouble() returns [0,1), not [0,1]
            // Hopefully not too big a deal
            double u1, u2, w;
            do 
            {
                u1 = 2.0 * uniformGenerator.NextDouble() - 1.0;
                u2 = 2.0 * uniformGenerator.NextDouble() - 1.0;
                w = u1 * u1 + u2 * u2;
            }
            while(w >= 1.0);
            w = Math.Sqrt((-2.0 * Math.Log(w)) / w);
            n1 = (u1 * w) * deviation + mean;
            n2 = (u2 * w) * deviation + mean;
        }
    }
}


