using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.helpers {
    public class SampleFormat {
        
        private uint channels = 0;

        public SampleFormat() {
        
        }

        public SampleFormat(uint channels) {
            this.channels = channels;

        }

        public uint getNumberOfChannels() {
            return channels;
        }

    }
}
