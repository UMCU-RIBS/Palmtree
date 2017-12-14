/**
 * The Sound class
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
using NLog;
using System;
using System.IO;

namespace UNP.Core.Helpers {

    /// <summary>
    /// The <c>Sound</c> class.
    /// 
    /// ...
    /// </summary>
    public static class Sound {

        private static Logger logger = LogManager.GetLogger("Sound");

        public static void Play(string filename) {

            // check if the file exists
            if (!File.Exists(filename)) {
                logger.Error("Could not play soundfile '" + filename + "'");
                return;
            }

            // play the file
            try {
                new System.Media.SoundPlayer(filename).Play();
            } catch (Exception) {
                logger.Error("Could not play soundfile '" + filename + "'");
                return;
            }
        }

    }
}
