/**
 * The IApplicationChild interface
 * 
 * ...
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
    /// The <c>IApplicationChild</c> interface.
    /// 
    /// abc.
    /// </summary>
    public interface IApplicationChild {

        void AppChild_start(Parameters parentParameters);
        void AppChild_stop();                        // stops the task from running. The parent process should check isRunning (whether the task stopped) and is responsible for removing the object
        bool AppChild_isRunning();                   // returns whether the task is running (will also return false when the task is finished)

        void AppChild_process(double[] input, bool connectionLost);

        void AppChild_resume();
        void AppChild_suspend();

    }

}
