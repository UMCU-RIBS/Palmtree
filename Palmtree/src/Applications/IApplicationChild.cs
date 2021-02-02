/**
 * IApplicationChild interface
 * 
 * This file declares an interface which application modules are supposed to implement in order to be started, stopped
 * pauzed and resumed as a child module by another (parent) application module (such as UNPMenu/Coconut).
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
using Palmtree.Core.Params;

namespace Palmtree.Applications {

    /// <summary>
    /// Interface which application modules must implement in order to be used by another application module
    /// </summary>
    public interface IApplicationChild {

        /// <summary>
        /// Start the application module as a child module from another (parent) application module
        /// </summary>
        /// <param name="parentParameters">A Parameters object that contains the configuration parameters of the parent application module. Can be used to transfer paremeters such as the application window position and size.</param>
        void AppChild_start(Parameters parentParameters);
		
        /// <summary>
        /// Stop the application module from running.
        /// The parent application module should check isRunning (whether the task stopped) and is responsible for removing the child module instance
        /// </summary>
        void AppChild_stop();
		
        /// <summary>
        /// Check whether the application module is running (has been started and not stopped).
        /// The module stops running automatically when it is finite and has finished.
        /// </summary>
        /// <returns>True if the module is running, false if is not running and/or has finished running</returns>
        bool AppChild_isRunning();

        /// <summary>Process new incoming samples, these are forwarded by the parent application module</summary>
        /// <param name="input">An array containing the new samples, each position the array represents one input channel</param>
        void AppChild_process(double[] input, bool connectionLost);

        /// <summary>
        /// Resume the application module after suspending it. 
        /// In most cases this will re-initiate the (opengl) view associated with the child instance so the child module can resume the interaction.
        /// </summary>
        void AppChild_resume();
		
        /// <summary>
        /// Suspend the application module from another (parent) application module
        /// In most cases this should close the (opengl) view associated with the child instance, so the parent module can take over the view/interaction again.
        /// </summary>
        void AppChild_suspend();

    }

}
