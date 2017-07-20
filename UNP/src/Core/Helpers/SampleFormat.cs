using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Helpers {

    public class SampleFormat {
        
        private int channels = 0;                       //
        private double rate = 0;                        // sample rate (in samples/second)
        //private int[] types = null;                     // 

        public SampleFormat() {
        
        }

        public SampleFormat(int channels, double rate) {
            this.channels = channels;
            this.rate = rate;
        }

        public int getNumberOfChannels() {
            return channels;
        }

        public double getRate() {
            return rate;
        }

    }

}
