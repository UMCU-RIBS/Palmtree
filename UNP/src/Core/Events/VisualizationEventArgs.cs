using System;

namespace UNP.Core.Events {

    public class VisualizationEventArgs : EventArgs {
        public int level { get; set; }
        public string text { get; set; }
        public string value { get; set; }
    }

}
