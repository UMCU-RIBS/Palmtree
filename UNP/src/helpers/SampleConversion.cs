using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.helpers {

    static class SampleConversion {

        const int SamplesPerSecond = 5;

        public static int timeToSamples(double timeInSeconds) {
            return (int)Math.Round(timeInSeconds * (double)SamplesPerSecond);
        }

        public static double samplesToTime(int samples) {
            return (double)samples / (double)SamplesPerSecond;
        }
    }
}
