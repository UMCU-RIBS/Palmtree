using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UNP.Core.Helpers {

    public class DebugHelper {


        public enum DebugSignalType : int {
            Rand,
            Sinus
        };            // whether the test data is random or based on sinusoids

        public static double[] generateTestData(double[] amp, double[] f, double fs, int N, DebugSignalType type) {

            double[] data = new double[N];

            if (type == DebugSignalType.Rand) {

                Random rand = new Random(Guid.NewGuid().GetHashCode());
                for (int i = 0; i < N; i++) { data[i] = rand.Next(1, 1000); }

            } else if (type == DebugSignalType.Sinus) {

                // init vars
                double samplingFrequency = fs;

                // cycle through all signals to be generated
                for (int w = 0; w < amp.Length; w++) {

                    // transfer variables
                    double amplitude = amp[w];
                    double frequency = f[w];

                    // allow test data to be cast to ushort
                    if (amplitude > ushort.MaxValue) amplitude = ushort.MaxValue;

                    // determine signal parameters: set signal frequency
                    double b = 1 / ((2 * Math.PI) * frequency);

                    // create signal, equal to length of data epochs filter expects
                    for (int i = 0; i < N; i++) {

                        // determine signal parameters: distance between datapoints is equal to sampling interval, ie 1 / samplingFrequency.
                        double x = i * (1 / samplingFrequency);

                        // create sine wave with frequency equal to given frequency and amplitude
                        data[i] += amplitude * Math.Sin(x / b);
                    }
                }
            }

            return data;
        }


    }

}
