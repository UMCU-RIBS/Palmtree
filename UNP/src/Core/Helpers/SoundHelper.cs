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
using System.Timers;

namespace UNP.Core.Helpers {

    /// <summary>
    /// The <c>Sound</c> class.
    /// 
    /// ...
    /// </summary>
    public static class SoundHelper {

        private static Logger logger = LogManager.GetLogger("Sound");
        private static Timer soundTimer = null;                    // timer to play the sound intervalled on

        public static void play(string filename) {

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

        public static void playContinuousAtInterval(string filename, int ms) {

            stopContinuous();

            // play the connection lost sound
            play(filename);

            // setup and start a timer to play the connection lost sound every 2 seconds
            soundTimer = new System.Timers.Timer(ms);
            soundTimer.Elapsed += delegate (object source, System.Timers.ElapsedEventArgs e) {

                // play the connection lost sound
                play(filename);

            };
            soundTimer.AutoReset = true;
            soundTimer.Start();
        }

        public static void stopContinuous() {

            // stop and clear the connection lost timer
            if (soundTimer != null) {
                soundTimer.Stop();
                soundTimer = null;
            }

        }

    }
}
