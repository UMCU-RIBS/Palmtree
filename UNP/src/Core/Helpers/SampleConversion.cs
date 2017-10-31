using System;

namespace UNP.Core.Helpers {

    public static class SampleConversion {

        public static double sampleRate() {
            return MainThread.getPipelineSamplesPerSecond();
        }

        public static int timeToSamples(double timeInSeconds) {
            return (int)Math.Round(timeInSeconds * MainThread.getPipelineSamplesPerSecond());
        }

        public static double timeToSamplesAsDouble(double timeInSeconds) {
            return timeInSeconds * MainThread.getPipelineSamplesPerSecond();
        }

        public static double samplesToTime(int samples) {
            return (double)samples / MainThread.getPipelineSamplesPerSecond();
        }
    }

}
