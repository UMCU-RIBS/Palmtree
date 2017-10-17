using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Helpers {

    public class SampleFormat {
        
        private int channels = 0;                       //
        private int rate = 0;                        // amount of values contained in one packet
        //private int[] types = null;                   // 

        public SampleFormat() {
        
        }

        public SampleFormat(int channels, int rate) {
            this.channels = channels;
            this.rate = rate;
        }

        public int getNumberOfChannels() {
            return channels;
        }

        public int getRate() {
            return rate;
        }

    }

}
