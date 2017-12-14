/**
 * The IApplicationUNP interface
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
using UNP.Core.Params;

namespace UNP.Applications {

    /// <summary>
    /// The <c>IApplicationUNP</c> interface.
    /// 
    /// abc.
    /// </summary>
    public interface IApplicationUNP {

        void UNP_start(Parameters parentParameters);
        void UNP_stop();                        // stops the task from running. The parent process should check isRunning (whether the task stopped) and is responsible for removing the object
        bool UNP_isRunning();                   // returns whether the task is running (will also return false when the task is finished)

        void UNP_process(double[] input, bool connectionLost);

        void UNP_resume();
        void UNP_suspend();

    }

}
