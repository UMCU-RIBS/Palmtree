using System;

namespace UNP.Core.Helpers {

    public static class SampleConversion {

        public static double sampleRate() {
            return MainThread.SamplesPerSecond();
        }

        public static int timeToSamples(double timeInSeconds) {
            return (int)Math.Round(timeInSeconds * MainThread.SamplesPerSecond());
        }

        public static double timeToSamplesAsDouble(double timeInSeconds) {
            return timeInSeconds * MainThread.SamplesPerSecond();
        }

        public static double samplesToTime(int samples) {
            return (double)samples / MainThread.SamplesPerSecond();
        }
    }

}
