using System;

namespace UNP.Core.Helpers {

    public class PackageFormat {
        
        private int channels = 1;                       // number of channels in each package
        private int samples = 1;                        // number of samples in each package
        private double rate = 5;                        // the rate at which packages are passed (in packages per second/hz)
        //private int[] types = null;                     // 

        public PackageFormat(int channels, int samples, double rate) {
            this.channels = channels;
            this.samples = samples;
            this.rate = rate;
        }

        public int getNumberOfChannels() {
            return channels;
        }

        public int getSamples() {
            return samples;
        }

        public double getRate() {
            return rate;
        }

    }

}
