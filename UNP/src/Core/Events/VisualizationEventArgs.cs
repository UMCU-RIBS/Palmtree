using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Events {

    public class VisualizationEventArgs : EventArgs {
        public int level { get; set; }
        public string text { get; set; }
        public string value { get; set; }
    }

}
