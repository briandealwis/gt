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
        protected Random uniformGenerator;
        protected bool mustGenerate = true;
        protected double mean, deviation;
        protected double n1, n2;

        /// <summary>
        /// Create a new instance with specified mean and standard deviation.
        /// </summary>
        /// <param name="mean">the mean</param>
        /// <param name="deviation">the standard deviation</param>
        public GaussianRandomNumberGenerator(double mean, double deviation) 
            : this(mean, deviation, new Random()) { }

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


