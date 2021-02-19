/**
 * IApplication interface
 * 
 * This file declares an interface which all application modules are supposed to implement.
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

namespace Palmtree.Applications {

    /// <summary>
    /// Interface which application modules are supposed to implement
    /// </summary>
    public interface IApplication {

        /// <summary>Retrieve the the application module's version number</summary>
        /// <returns>The version number as an integer</returns>
        int getClassVersion();

        /// <summary>Retrieve the name of the application module</summary>
        /// <returns>The name as a string</returns>
        string getClassName();

        /// <summary>Retrieve the configuration parameters that are used in the application module</summary>
        /// <returns>A Parameter object defining the configuration parameters</returns>
        Parameters getParameters();

        /// <summary>Configure the application module. Called upon by the "Set config and init" button in the GUI</summary>
        /// <param name="input">Reference to a PackageFormat object which defines the incoming sample streams</param>
        /// <returns>A boolean, either true for a succesfull configuration, or false upon failure</returns>
        bool configure(ref SamplePackageFormat input);

        /// <summary>Initialize the application module. Called upon by the "Set config and init" button in the GUI</summary>
        void initialize();

        /// <summary>Start the application module. Called upon by "Start" in the GUI</summary>
        void start();

        /// <summary>Stop the application module. Called upon by "Stop" in the GUI</summary>
        void stop();

        /// <summary>Check whether the application module is started</summary>
        /// <returns>True if the module is started, false if not started</returns>
        bool isStarted();

        /// <summary>Process new incoming samples</summary>
        /// <param name="input">A reference to an array containing one or more input samples for one or more channels</param>
        void process(double[] input);

        /// <summary>
        /// Closes the application module, it's dependencies and frees memory.
        /// 
        /// Called upon when Palmtree closes. Or when the instance of the application module is a child
        /// instance (hosted by another application module) and is being stopped.
        /// </summary>
        void destroy();

    }

}
