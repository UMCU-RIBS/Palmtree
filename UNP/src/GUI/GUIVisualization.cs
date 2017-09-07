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
using UNP.Core.DataIO;
using UNP.Core.Events;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.GUI {

    public partial class GUIVisualization : Form {

        public const int NumberOfGraphs = 3;

        private static Logger logger = LogManager.GetLogger("GUIVisualization");
        private Object lockVisualization = new Object();                          // threadsafety lock for visualization

        private struct Graph {
            public int id;
            public bool source;
            public bool updateOnNewData;
            public int numDataPoints;
            public GraphStream[] streams;
            public Chart chart;
        }

        private struct GraphStream {
            public string name;
            public bool display;
            public bool standardize;                // standardize the values around 0
            public RingBuffer values;               // the stream values are stored in this ringbuffer
            public RingBuffer intervals;            // the intervals between the time a value came in and the previous one coming in are stored in this ringbuffer
            public Series series;
            public int numValuesAddedSinceUpdate;   // tracks the number of values added since the last chart update
        }

        private Graph[] visualizationGraphs = null;           // array that holds all the visualization graphs

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

                    // retrieve the visualization stream names (this is already an array copy of the list held in the data class)
                    string[] visualizationSourceInputStreamNames = Data.getSourceInputStreamNames();

                    // retrieve the visualization stream names (this is already an array copy of the list held in the data class)
                    string[] visualizationStreamNames = Data.getVisualizationDataStreamNames();
                    
                    // create graphs
                    visualizationGraphs = new Graph[NumberOfGraphs];
                    for (int i = 0; i < NumberOfGraphs; i++) {

                        visualizationGraphs[i].source = i == 0;

                        if (visualizationGraphs[i].source)
                            createGraph(i, ref visualizationGraphs[i], visualizationSourceInputStreamNames);
                        else
                            createGraph(i, ref visualizationGraphs[i], visualizationStreamNames);

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

                // check if there are graphs initialized
                if (visualizationGraphs != null) {

                    // clear the graph structs
                    for (int i = 0; i < NumberOfGraphs; i++) {
                        if (i >= visualizationGraphs.Count()) break;

                        // remove/clear the char control
                        this.Controls.Remove(visualizationGraphs[i].chart);
                        visualizationGraphs[i].chart.Dispose();
                        visualizationGraphs[i].chart = null;

                        // clear the streams
                        visualizationGraphs[i].streams = new GraphStream[0];

                    }

                    // clear the graphs
                    visualizationGraphs = null;

                }

            }
            
        }


        private void createGraph(int id, ref Graph graph, string[] streamNames) {

            int numStreams = streamNames.Length;

            // 
            int y = 12 + id * 180;

            // create the actual control
            Chart chart = new Chart();
            chart.Location = new System.Drawing.Point(12, y);
            chart.Name = "crtGraph" + id;
            chart.Size = new System.Drawing.Size(this.ClientRectangle.Width - 20, 160);
            chart.TabIndex = id;
            chart.Text = "crtGraph" + id;
            chart.BorderlineWidth = 1;
            chart.BorderlineColor = Color.Black;
            chart.BorderlineDashStyle = ChartDashStyle.Solid;
            
            ChartArea chartArea = new ChartArea("ChartArea");
            //chartArea.AxisX.Interval = 1;
            //chartArea.AxisX.IntervalAutoMode = IntervalAutoMode.FixedCount;
            chart.ChartAreas.Add(chartArea);

            this.Controls.Add(chart);

            // store the id and a reference to the chart object in the graph struct
            graph.id = id;
            graph.updateOnNewData = false;
            graph.chart = chart;
            
            // set a standard amount of data points for the graph
            graph.numDataPoints = 100;

            // add all the streams to the graph
            graph.streams = new GraphStream[numStreams];
            for (int i = 0; i < numStreams; i++) {
                graph.streams[i].name = streamNames[i];
                graph.streams[i].display = false;
                graph.streams[i].standardize = false;
                graph.streams[i].values = new RingBuffer((uint)graph.numDataPoints);
                graph.streams[i].intervals = new RingBuffer((uint)graph.numDataPoints);
                graph.streams[i].numValuesAddedSinceUpdate = 0;
            }
            
            // TODO: debug: make the first stream visible
            if (numStreams > 0) {
                graph.streams[0].display = true;
                graph.streams[0].series = new Series(streamNames[0]);
                graph.streams[0].series.Name = "Series0";
                graph.streams[0].series.Color = Color.Black;
                graph.streams[0].series.ChartType = SeriesChartType.Line;
                graph.streams[0].series.MarkerStyle = MarkerStyle.Circle;
                graph.streams[0].series.MarkerSize = 8;
                graph.streams[0].series.MarkerColor = Color.Black;
                chart.Series.Add(graph.streams[0].series);
            }

            // create a context menu for the chart
            ContextMenu mnu = createGraphContextMenu(ref graph);
            chart.ContextMenu = mnu;

        }

        private ContextMenu createGraphContextMenu(ref Graph graph) {

            // main context menu
            ContextMenu mnuContextMenu = new ContextMenu();

            // stream submenu
            MenuItem mnuStreams = new MenuItem("Streams");
            for (int irr = 0; irr < graph.streams.Length; irr++) {
                MenuItem mnuStreamItem = new MenuItem(graph.streams[irr].name);

                MenuItem mnuStreamItemDisplay = new MenuItem("Display");
                mnuStreamItemDisplay.Click += (sender, e) => {
                    int val = irr;
                    //toggleStreamDisplay_Click(sender, e, ref graph, ref graph.streams[i]);   
                    logger.Error(val);
                };
                mnuStreamItemDisplay.Checked = graph.streams[irr].display;
                mnuStreamItem.MenuItems.Add(mnuStreamItemDisplay);

                MenuItem mnuStreamItemStandard = new MenuItem("Standardize");
                //mnuStreamItemStandard.Click += (sender, e) => {      toggleStreamStandardize_Click(sender, e, chart);    };
                mnuStreamItem.MenuItems.Add(mnuStreamItemStandard);

                MenuItem mnuStreamItemColor = new MenuItem("Set color");
                //mnuStreamItemColor.Click += (sender, e) => {      toggleStreamColor_Click(sender, e, chart);    };
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

        private void GUIVisualization_FormClosing(object sender, FormClosingEventArgs e) {

            // destroy all graph related objects
            destroyGraphs();

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
                    for (int j = 0; j < visualizationGraphs[i].streams.Length; j++) {
                        
                        // in every graph add the values respectively over the streams 
                        visualizationGraphs[i].streams[j].values.Put(values[j]);

                        // count the values that have been added
                        visualizationGraphs[i].streams[j].numValuesAddedSinceUpdate++;

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
                                    
                                // thread safety
                                lock (lockVisualization) {
            
                                        updateChart(visualizationGraphs[i]);
                                    }
                                });

                            }

                        }

                    //}

                //});
                //updateThread.start();

            }
            */

        }

        /**
         * update the chart display to reflect the data
         * 
         * Assumes lock (visualizationLock) on certain objects before being called
         **/
        private void updateChart(Graph graph) {
            //logger.Error("updateChart " + graph.id);

            // loop through the streams in the graph
            for (int i = 0; i < graph.streams.Length; i++) {

                // check if the stream should be displayed
                if (graph.streams[i].display) {

                    // check if there are points added to this stream since the last update of the chart
                    if (graph.streams[i].numValuesAddedSinceUpdate > 0) {

                        // check if the number of values added is greater than the number of data points in the graph (and it's stream buffers)
                        if (graph.streams[i].numValuesAddedSinceUpdate < graph.numDataPoints) {
                            // a smaller of amount of values added then the graphs data size

                            // retrieve the number of values to added and the data
                            double[] values = graph.streams[i].values.DataSequential();
                            int numValues = graph.streams[i].numValuesAddedSinceUpdate;
                            
                            // add the values to the series
                            for (int j = values.Length - numValues; j < values.Length; j++) {
                                
                                graph.streams[i].series.Points.Add(values[j]);

                                /*
                                int x = graph.streams[i].series.Points.Count;
                                if (x > 10) {
                                    x = x * x;
                                }
                                graph.streams[i].series.Points.AddXY(x, values[j]);
                                */
                                
                            }

                            // decreases the number of values added since last chart update with the number of values that were just added
                            graph.streams[i].numValuesAddedSinceUpdate -= numValues;

                        } else {
                            // bigger (or equal) amount of values added than the graph (and it's stream buffers) should hold

                            // TODO: one time update the entire series with the last x datapoints
                            graph.streams[i].series.Points.Clear();
                            double[] values = graph.streams[i].values.DataSequential();
                            //graph.streams[i].series.Points.AddY(

                        }

                        //graph.streams[i].series.Points.Add(10);
                        //graph.streams[i].series.Points.AddXY(1, 10);
                        //graph.streams[i].series.Points.AddXY(2, 20);
                        //graph.streams[i].series.Points.AddXY(3, 25);

                    }
                }
                    

            }
            
            graph.chart.Invalidate();

            
            /*
            var series = new Series("Finance");
            // Frist parameter is X-Axis and Second is Collection of Y- Axis
            series.Points.DataBindXY(new[] { 2001, 2002, 2003, 2004 }, new[] { 100, 200, 90, 150 });
            chart1.Series.Add(series);
            */

            
        }

        /**
         * Function called by Data when a new event is logged for visualization
         * 
         * @param e
         */
        public void newEvent(object sender, VisualizationEventArgs e) {

        }

        private void tmrUpdate_Tick(object sender, EventArgs e) {
            //logger.Error("Tick");
            
            // thread safety
            lock (lockVisualization) {

                // check if the visualization graphs are set
                if (visualizationGraphs != null) {

                    // loop through the graphs
                    for (int i = 0; i < visualizationGraphs.Length; i++) {

                        // check if the graph needs to be updated on the timer (instead of on new data)
                        if (!visualizationGraphs[i].updateOnNewData) {

                            // update the chart
                            updateChart(visualizationGraphs[i]);

                        }

                    }   // end loop

                }

            }   // end lock

        }


        private void toggleStreamDisplay_Click(object sender, EventArgs e, Graph graph, GraphStream stream) {

            // toggie the display setting
            stream.display = !stream.display;

            // update the menu item
            MenuItem menuItem = (MenuItem)sender;
            menuItem.Checked = stream.display;

            // clear the stream it's graph points (if it had any)
            stream.series.Points.Clear();


        }


    }
    

}
