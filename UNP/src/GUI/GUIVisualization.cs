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
using System.Threading;
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
        private Object lockVisualization = new Object();                          // threadsafety lock for visualization

        private struct VisualizationStream {
            public string name;
        }

        private struct Graph {
            public int id;
            public bool source;
            public bool updateOnNewData;
            public int numDataPoints;
            public GraphStream[] streams;
            public Chart chartObject;
        }

        private struct GraphStream {
            public bool display;
            public bool standardize;                // standardize the values around 0
            public RingBuffer values;               // the stream values are stored in this ringbuffer
            public RingBuffer intervals;            // the intervals between the time a value came in and the previous one coming in are stored in this ringbuffer
            public Series series;
        }
        
        private int numVisualizationSourceInputStreams = 0;
        private VisualizationStream[] visualizationSourceInputStreams = null;   // array that holds all the visualization source input streams
        
        private int numVisualizationDataStreams = 0;
        private VisualizationStream[] visualizationDataStreams = null;          // array that holds all the visualization data streams
        
        private Graph[] visualizationGraphs = null;                             // array that holds all the visualization graphs

        public GUIVisualization() {

            // initialize components
            InitializeComponent();

        }

        private void GUIVisualization_Load(object sender, EventArgs e) {


        }

        public void initGraphs() {

            // thread safety
            lock (lockVisualization) {

                try {

                    //
                    // retrieve and process source input streams
                    //

                    // retrieve the number of source input streams
                    numVisualizationSourceInputStreams = Data.GetNumberOfSourceInputStreams();

                    // retrieve the visualization stream names (this is already an array copy of the list held in the data class)
                    string[] visualizationSourceInputStreamNames = Data.GetSourceInputStreamNames();

                    // create a struct for each stream
                    visualizationSourceInputStreams = new VisualizationStream[numVisualizationSourceInputStreams];
                    for (int i = 0; i < numVisualizationSourceInputStreams; i++) {
                        visualizationSourceInputStreams[i].name = visualizationSourceInputStreamNames[i];
                    }


                    //
                    // retrieve and process data streams
                    //

                    // retrieve the number of visualization data streams
                    numVisualizationDataStreams = Data.GetNumberOfVisualizationDataStreams();

                    // retrieve the visualization stream names (this is already an array copy of the list held in the data class)
                    string[] visualizationStreamNames = Data.GetVisualizationDataStreamNames();

                    // create a struct for each stream
                    visualizationDataStreams = new VisualizationStream[numVisualizationDataStreams];
                    for (int i = 0; i < numVisualizationDataStreams; i++) {
                        visualizationDataStreams[i].name = visualizationStreamNames[i];
                    }

                    //
                    // create graphs
                    //

                    // create graphs
                    visualizationGraphs = new Graph[NumberOfGraphs];
                    for (int i = 0; i < NumberOfGraphs; i++) {
                        createGraph(i, ref visualizationGraphs[i], i == 0);
                    }

                    // connect the events
                    Data.newVisualizationSourceInputValues += newSourceInputValues;      // add the method to the event handle
                    Data.newVisualizationStreamValues += newStreamValues;                // add the method to the event handle
                    Data.newVisualizationEvent += newEvent;                              // add the method to the event handle

                } catch (Exception e) {

                    // message
                    logger.Warn("Exception while initializing graphs: " + e.Message);

                }
            
            }   // end lock

        }

        public void destroyGraphs() {
            
            // thread safety
            lock (lockVisualization) {

                // disconnect the events
                Data.newVisualizationSourceInputValues -= newSourceInputValues;      // add the method to the event handle
                Data.newVisualizationStreamValues -= newStreamValues;                // add the method to the event handle
                Data.newVisualizationEvent -= newEvent;                              // add the method to the event handle

                if (visualizationGraphs != null) {
                    // clear the graph structs
                    for (int i = 0; i < NumberOfGraphs; i++) {
                        if (i >= visualizationGraphs.Count()) break;

                        // remove/clear the char control
                        this.Controls.Remove(visualizationGraphs[i].chartObject);
                        visualizationGraphs[i].chartObject.Dispose();
                        visualizationGraphs[i].chartObject = null;

                        // clear the streams
                        visualizationGraphs[i].streams = new GraphStream[0];

                    }

                    // clear the graphs
                    visualizationGraphs = null;

                }

                // clear the data streams
                visualizationDataStreams = null;
                numVisualizationDataStreams = 0;

                // clear the source input streams
                visualizationSourceInputStreams = null;
                numVisualizationSourceInputStreams = 0;
            
            }
            
        }
        

        private void createGraph(int id, ref Graph graph, bool source) {
            
            // define
            int numStreams = 0;
            VisualizationStream[] streams;
            if (source) {
                numStreams = numVisualizationSourceInputStreams;
                streams = visualizationSourceInputStreams;
            } else {
                numStreams = numVisualizationDataStreams;
                streams = visualizationDataStreams;
            }
            
            // 
            int y = 12 + id * 200;

            // create the actual control
            Chart chart = new Chart();
            ChartArea chartArea = new ChartArea("ChartArea");
            chart.ChartAreas.Add(chartArea);
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
            graph.source = source;
            graph.updateOnNewData = true;
            graph.chartObject = chart;
            
            // set a standard amount of data points for the graph
            graph.numDataPoints = 100;

            // add all the streams to the graph
            graph.streams = new GraphStream[numStreams];
            for (int i = 0; i < numStreams; i++) {
                graph.streams[i].display = false;
                graph.streams[i].standardize = false;
                graph.streams[i].values = new RingBuffer((uint)graph.numDataPoints);
                graph.streams[i].intervals = new RingBuffer((uint)graph.numDataPoints);
            }
            
            // TODO: debug: make the first stream visible
            if (numStreams > 0) {
                graph.streams[0].display = true;
                graph.streams[0].series = new Series(streams[0].name);
                graph.streams[0].series.Name = "Series0";
                graph.streams[0].series.Color = Color.Black;
                graph.streams[0].series.ChartType = SeriesChartType.Line;
                graph.streams[0].series.IsXValueIndexed = true;
                chart.Series.Add(graph.streams[0].series);
            }

            // create a context menu for the chart
            ContextMenu mnu = createGraphContextMenu(chart, ref graph, ref streams);
            chart.ContextMenu = mnu;

        }

        private ContextMenu createGraphContextMenu(Chart chart, ref Graph graph, ref VisualizationStream[] streams) {

            // main context menu
            ContextMenu mnuContextMenu = new ContextMenu();

            // stream submenu
            MenuItem mnuStreams = new MenuItem("Streams");
            for (int i = 0; i < streams.Length; i++) {
                MenuItem mnuStreamItem = new MenuItem(streams[i].name);

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

            // 
            MenuItem mnuUpdateItem = new MenuItem("Update");
            MenuItem mnuUpdateOnNewData = new MenuItem("On new data");
            MenuItem mnuUpdateOnTimer = new MenuItem("On timer");
            mnuUpdateOnNewData.Checked = graph.updateOnNewData;
            mnuUpdateOnTimer.Checked = !mnuUpdateOnNewData.Checked;
            mnuUpdateItem.MenuItems.Add(mnuUpdateOnNewData);
            mnuUpdateItem.MenuItems.Add(mnuUpdateOnTimer);
            mnuContextMenu.MenuItems.Add(mnuUpdateItem);

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

            // destroy all graph related objects
            destroyGraphs();

            
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
            //logger.Debug("newSourceInputValues " + e.values.Length);
            
            // add the new values to source input graphs
            addNewValues(e.values, true);

        }

        /**
         * Function called by Data when a new stream value is logged for visualization
         * 
         * @param e
         */
        public void newStreamValues(object sender, VisualizationValuesArgs e) {
            //logger.Debug("newStreamValues " + e.values.Length);

            // add the new values to data stream graphs
            addNewValues(e.values, false);

        }

        public void addNewValues(double[] values, bool source) {

            // flag to hold whether an chart update call has to be made
            bool updateOnNewData = false;

            // thread safety
            lock (lockVisualization) {

                // loop through the graphs
                for (int i = 0; i < visualizationGraphs.Length; i++) {

                    // if the current graph is a source graph and the data is not (or visa versa) then skip
                    if (source != visualizationGraphs[i].source) continue;

                    // check if the graph's chart wants to be updated on new data
                    if (visualizationGraphs[i].updateOnNewData) updateOnNewData = true;

                    // loop through the visualization streams
                    for (int j = 0; j < numVisualizationSourceInputStreams; j++) {

                        // in every graph add the values respectively over the streams 
                        visualizationGraphs[i].streams[j].values.Put(values[j]);

                    }

                }

            } // end lock
            /*
            // check if one or more chars need to be updated on new data
            if (updateOnNewData) {

                // create a thread which call update on
                //Thread updateThread = new System.Threading.Thread(delegate() {
                    
                    // thread safety
                    //lock (lockVisualization) {

                        for (int i = 0; i < visualizationGraphs.Length; i++) {

                            // if the current graph is a source graph and the data is not (or visa versa) then skip
                            if (source != visualizationGraphs[i].source) continue;

                            // check if the graph's chart wants to be updated on new data
                            if (visualizationGraphs[i].updateOnNewData) {

                                this.Invoke((MethodInvoker)delegate {
                                    updateChart(visualizationGraphs[i]);
                                });

                            }

                        }

                    //}

                //});
                //updateThread.Start();

            }
            */

        }

        /**
         * update the chart display to reflect the data
         **/
        private void updateChart(Graph graph) {
            //logger.Error("updateChart " + graph.id);

            Chart chart = graph.chartObject;

            // loop through the streams in the graph
            for (int i = 0; i < graph.streams.Length; i++) {

                



                // check if the stream should be displayed
                if (graph.streams[i].display) {
                    double[] aap = graph.streams[i].values.DataSequential();

                    graph.streams[i].series.Points.Add(aap[aap.Length - 1]);
                    
                    //graph.streams[i].series.Points.Add(10);
                    //graph.streams[i].series.Points.AddXY(1, 10);
                    //graph.streams[i].series.Points.AddXY(2, 20);
                    //graph.streams[i].series.Points.AddXY(3, 25);


                }
                    

            }
            
            chart.Invalidate();

            /*
            this.Invoke((MethodInvoker)delegate {
                graph.chartObject.Invalidate();
            });
            */
            /*
            var series = new Series("Finance");
            // Frist parameter is X-Axis and Second is Collection of Y- Axis
            series.Points.DataBindXY(new[] { 2001, 2002, 2003, 2004 }, new[] { 100, 200, 90, 150 });
            chart1.Series.Add(series);
            */

            //if (graph.id == 1)

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


            //chart.Refresh();

        }

        /**
         * Function called by Data when a new event is logged for visualization
         * 
         * @param e
         */
        public void newEvent(object sender, VisualizationEventArgs e) {

        }

        private void tmrUpdate_Tick(object sender, EventArgs e) {
            logger.Error("Tick");

            for (int i = 0; i < visualizationGraphs.Length; i++) {
                updateChart(visualizationGraphs[i]);
            }

        }



    }
    

}
