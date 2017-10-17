using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace UNP.Core.Helpers {

    public enum debugSignalType : int { Rand, Sinus };            // whether the test data is random or based on sinusoids

    static class ThreadSafeRandom {

        [ThreadStatic] private static Random Local;

        public static Random ThisThreadsRandom {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }

    }

    public static class Extensions {


        public static double[] generateTestData(double[] amp, double[] f, double fs, int N, debugSignalType type) {

            double[] data = new double[N];

            if (type == debugSignalType.Rand) {

                Random rand = new Random(Guid.NewGuid().GetHashCode());
                for (int i = 0; i < N; i++) {data[i] = rand.Next(1, 1000);}

            } else if (type == debugSignalType.Sinus) {

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



        // TODO: use non-Linq statement?
        public static int[] findIndices(int[] array, int match) {
            return Enumerable.Range(0, array.Length)
                         .Where(i => array[i] == match)
                         .ToArray();
        }


        // TODO: use non-Linq statement?
        public static int[] unique(int[] array) {
            return array.Distinct().ToArray();
        }

        public static void Swap<T>(this IList<T> list, int indexA, int indexB) {
            T tmp = list[indexA];
            list[indexA] = list[indexB];
            list[indexB] = tmp;
        }

        public static void Shuffle<T>(this IList<T> list) {

            int n = list.Count;
            while (n > 1) {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }

        }


        public static string TrimAll(this string str) {
            var len = str.Length;
            var src = str.ToCharArray();
            int dstIdx = 0;
            for (int i = 0; i < len; i++) {
                var ch = src[i];
                switch (ch) {
                    case '\u0020': case '\u00A0': case '\u1680': case '\u2000': case '\u2001': 
                    case '\u2002': case '\u2003': case '\u2004': case '\u2005': case '\u2006': 
                    case '\u2007': case '\u2008': case '\u2009': case '\u200A': case '\u202F': 
                    case '\u205F': case '\u3000': case '\u2028': case '\u2029': case '\u0009': 
                    case '\u000A': case '\u000B': case '\u000C': case '\u000D': case '\u0085':
                        continue;
                    default:
                        src[dstIdx++] = ch;
                        break;
                }
            }
            return new string(src, 0, dstIdx);
        }

    }

}
