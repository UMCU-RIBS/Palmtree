/**
 * The IFilter interface
 * 
 * This file declares an interface which all fllter modules are supposed to implement.
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using Palmtree.Core;
using Palmtree.Core.Params;

namespace Palmtree.Filters {

    /// <summary>
    /// Interface which filter modules are supposed to implement
    /// </summary>
    public interface IFilter {

        /// <summary>Retrieve the the filter module's version number</summary>
        /// <returns>The version number as an integer</returns>
        int getClassVersion();

        /// <summary>Retrieve the name of the filter module</summary>
        /// <returns>The name as a string</returns>
        string getName();

        /// <summary>Retrieve the configuration parameters that are used in the filter module</summary>
        /// <returns>A Parameter object defining the configuration parameters</returns>
        Parameters getParameters();

        /// <summary>Configure the filter module. Called upon by the "Set config and init" button in the GUI</summary>
        /// <param name="input">Reference to a PackageFormat object which defines the incoming sample streams</param>
        /// <returns>A boolean, either true for a succesfull configuration, or false upon failure</returns>
        bool configure(ref SamplePackageFormat input, out SamplePackageFormat output);

        /// <summary>Initialize the filter module. Called upon by the "Set config and init" button in the GUI</summary>
        void initialize();

        /// <summary>
        /// Re-configure and/or reset the filter configration parameters on the fly (during runtime).
        /// Most likely is called upon through the MainThread by application modules. Handles both the configuration and initialization of filter related variables.  
        /// </summary>
        /// <param name="newParameters">Parameter object that defines the configuration parameters to be set. Set to NULL to leave the configuration parameters untouched.
        /// The new configuration parameters are checked to see whether they can or cannot be applied to a running filter
        /// (e.g. when the new configuration parameters would adjust the number of expected output channels, which would have unforseen consequences for the next filter)
        /// </param>
        /// <param name="resetOption">Filter reset options. 
        /// 0 will reset the minimum, trying to retain as much of the information in the filter as possible; 1 will perform a complete reset of filter information.
        /// More specific reset options can be specified per filter and passed using options > 1, in such cases overwriting the FilterBase.ResetOptions enum in the filter implementation is encouraged.
        /// </param>
        /// <returns>A boolean, either true for a succesfull re-configuration, or false upon failure</returns>
        bool configureRunningFilter(Parameters newParameters, int resetOption);

        /// <summary>Start the filter module. Called upon by "Start" in the GUI</summary>
        void start();

        /// <summary>Stop the filter module. Called upon by "Stop" in the GUI</summary>
        void stop();

        /// <summary>Check whether the filter module is started</summary>
        /// <returns>True if the module is started, false if not started</returns>
        bool isStarted();

        /// <summary>Process new incoming samples</summary>
        /// <param name="input">A reference to an array containing one or more input samples for one or more channels</param>
        /// <param name="output">A reference to the array that the function should populate with processed samples, each position the array represents one output channel</param>
        void process(double[] input, out double[] output);

        /// <summary>
        /// Closes the filter module, it's dependencies and frees memory. Called upon when Palmtree closes.
        /// </summary>
        void destroy();

    }

}
