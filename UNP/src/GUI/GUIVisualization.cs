using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using UNP.Core;
using UNP.Core.Events;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.GUI {

    public partial class GUIVisualization : Form {

        private static Logger logger = LogManager.GetLogger("GUIVisualization");

        public GUIVisualization() {

            // initialize components
            InitializeComponent();

        }
        
        private void GUIVisualization_Load(object sender, EventArgs e) {

            Data.newVisualizationSourceInputSample += newSourceInputValue;      // add the method to the event handle
            Data.newVisualizationStreamSample += newStreamValue;                // add the method to the event handle
            Data.newVisualizationEvent += newEvent;                             // add the method to the event handle

            /*
            var series1 = new System.Windows.Forms.DataVisualization.Charting.Series
            {
                Name = "Series1",
                Color = System.Drawing.Color.Green,
                IsVisibleInLegend = false,
                IsXValueIndexed = true,
                ChartType = SeriesChartType.Line
            };

            this.chart1.Series.Add(series1);

            for (int i=0; i < 100; i++)
            {
                series1.Points.AddXY(i, f(i));
            }
            chart1.Invalidate();
            var series1 = new System.Windows.Forms.DataVisualization.Charting.Series {
                Name = "Series1",
                Color = System.Drawing.Color.Green,
                IsVisibleInLegend = false,
                IsXValueIndexed = true,
                ChartType = SeriesChartType.Line
            };

            this.chart1.Series.Add(series1);

            for (int i = 0; i < 100; i++) {
                series1.Points.AddXY(i, f(i));
            }
            chart1.Invalidate();
            */
        }

        private void GUIVisualization_FormClosing(object sender, FormClosingEventArgs e) {
            /*
            // TODO: check for changes

            // check whether the user is closing the form
            if (e.CloseReason == CloseReason.UserClosing) {

                // ask the user for confirmation
                if (MessageBox.Show("Are you sure you want to close?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) {
                    // user clicked no

                    // cancel the closing
                    e.Cancel = true;

                } else {

                    // continuing will close the form

                }

            }
            
            // check if the form is actually closing
            if (e.Cancel == false) {

                // flag the configuration as adjusted
                //this.DialogResult = DialogResult.Cancel;

            }
            */
            
        }


        /**
         * Function called by Data when a new source input value is logged
         * 
         * @param e
         */
        public void newSourceInputValue(object sender, VisualizationValueArgs e) {

        }

        /**
         * Function called by Data when a new stream value is logged
         * 
         * @param e
         */
        public void newStreamValue(object sender, VisualizationValueArgs e) {
            //logger.Debug("newStreamValue " + e.value);
        }

        /**
         * Function called by Data when a new event is logged
         * 
         * @param e
         */
        public void newEvent(object sender, VisualizationEventArgs e) {

        }


    }
    

}
