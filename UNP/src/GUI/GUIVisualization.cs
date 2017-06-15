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

        public const int NumberOfGraphs = 3;

        private static Logger logger = LogManager.GetLogger("GUIVisualization");

        private struct VisualizationStream {
            public string name;
        }

        private struct Graph {
            public int id;
            public int numDataPoints;
            public GraphStream[] streams;
            public Chart chartObject;
        }

        private struct GraphStream {
            public bool display;
            public bool standardize;                // standardize the values around 0
            public RingBuffer values;               // the stream values are stored in this ringbuffer
            public RingBuffer intervals;            // the intervals between the time a value came in and the previous one coming in are stored in this ringbuffer
        }
        
        private int numVisualizationStreams = 0;
        private VisualizationStream[] visualizationStreams = null;      // array that holds all the visualization streams
        private Graph[] visualizationGraphs = null;                     // array that holds all the visualization graphs

        public GUIVisualization() {

            // initialize components
            InitializeComponent();

        }

        private void GUIVisualization_Load(object sender, EventArgs e) {

            // retrieve the number of visualization data streams
            numVisualizationStreams = Data.GetNumberOfVisualizationStreams();
            
            // retrieve the visualization stream names (this is already an array copy of the list held in the data class)
            string[] visualizationStreamNames = Data.GetVisualizationStreamNames();

            // create a struct for each stream
            visualizationStreams = new VisualizationStream[numVisualizationStreams];
            for (int i = 0; i < numVisualizationStreams; i++) {
                visualizationStreams[i].name = visualizationStreamNames[i];
            }

            // create graphs

            visualizationGraphs = new Graph[NumberOfGraphs];
            for (int i = 0; i < NumberOfGraphs; i++)
                createGraph(i, ref visualizationGraphs[i]);


            // connect the events
            Data.newVisualizationSourceInputValues += newSourceInputValues;      // add the method to the event handle
            Data.newVisualizationStreamValues += newStreamValues;                // add the method to the event handle
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
        
        

        private void createGraph(int id, ref Graph graph) {

            int y = 12 + id * 200;

            // create the actual control
            Chart chart = new Chart();
            chart.Location = new System.Drawing.Point(12, y);
            chart.Name = "crtGraph" + id;
            chart.Size = new System.Drawing.Size(800, 190);
            chart.TabIndex = id;
            chart.Text = "crtGraph" + id;
            chart.BorderlineWidth = 1;
            chart.BorderlineColor = Color.Black;
            chart.BorderlineDashStyle = ChartDashStyle.Solid;
            this.Controls.Add(chart);

            // store the id and a reference to the chart object in the graph struct
            graph.id = id;
            graph.chartObject = chart;
            
            // set a standard amount of data points for the graph
            graph.numDataPoints = 100;

            // add all the streams to the graph
            graph.streams = new GraphStream[numVisualizationStreams];
            for (int i = 0; i < numVisualizationStreams; i++) {
                graph.streams[i].display = false;
                graph.streams[i].standardize = false;
                graph.streams[i].values = new RingBuffer((uint)graph.numDataPoints);
                graph.streams[i].intervals = new RingBuffer((uint)graph.numDataPoints);
            }

            // create a context menu for the 
            ContextMenu mnu = createGraphContextMenu(chart, ref graph);
            chart.ContextMenu = mnu;

        }

        private ContextMenu createGraphContextMenu(Chart chart, ref Graph graph) {

            // main context menu
            ContextMenu mnuContextMenu = new ContextMenu();

            // stream submenu
            MenuItem mnuStreams = new MenuItem("Streams");
            for (int i = 0; i < numVisualizationStreams; i++) {
                MenuItem mnuStreamItem = new MenuItem(visualizationStreams[i].name);

                MenuItem mnuStreamItemDisplay = new MenuItem("Display");
                mnuStreamItemDisplay.Click += (sender, e) => {
                    toggleStreamDisplay_Click(sender, e, chart);
                };
                
                MenuItem mnuStreamItemStandard = new MenuItem("Standardize");
                MenuItem mnuStreamItemColor = new MenuItem("Set color");


                mnuStreamItem.MenuItems.Add(mnuStreamItemDisplay);
                mnuStreamItem.MenuItems.Add(mnuStreamItemStandard);
                mnuStreamItem.MenuItems.Add(mnuStreamItemColor);
                mnuStreams.MenuItems.Add(mnuStreamItem);
            }
            mnuStreams.MenuItems.Add("-");
            mnuStreams.MenuItems.Add("Hide all");
            mnuContextMenu.MenuItems.Add(mnuStreams);


            mnuContextMenu.MenuItems.Add("Time scale");
            mnuContextMenu.MenuItems.Add("&Close");

            return mnuContextMenu;

        }

        private void toggleStreamDisplay_Click(object sender, EventArgs e, Chart chart) {
            //ToolStripItem clickedItem = sender as ToolStripItem;
            // your code here
            logger.Error("click");
            MenuItem aap = (MenuItem)sender;
            aap.Checked = true;
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
         * Function called by Data when new source input values are logged for visualization
         * 
         * @param e
         */
        public void newSourceInputValues(object sender, VisualizationValuesArgs e) {

        }

        /**
         * Function called by Data when a new stream value is logged for visualization
         * 
         * @param e
         */
        public void newStreamValues(object sender, VisualizationValuesArgs e) {
            logger.Debug("newStreamValues " + e.values.Length);
            
            // loop through the graphs
            for (int i = 0; i < visualizationGraphs.Length; i++) {

                // loop through the visualization streams
                for (int j = 0; j < numVisualizationStreams; j++) {

                    // in every graph add the values respectively over the streams 
                    visualizationGraphs[i].streams[j].values.Put(e.values[j]);

                    if (i == 0 && j == 0) {

                        uint size = visualizationGraphs[i].streams[j].values.Fill();
                        logger.Error("s " + size);
                        double[] dat = visualizationGraphs[i].streams[j].values.Data();
                        string reeks = "";
                        for (int h = 0; h < size; h++) {
                            if (h != 0)
                                reeks += " - ";
                            reeks += dat[h];
                        }
                        logger.Error("reeks " + reeks);
                    }

                }

            }

        }

        /**
         * Function called by Data when a new event is logged for visualization
         * 
         * @param e
         */
        public void newEvent(object sender, VisualizationEventArgs e) {

        }


    }
    

}
