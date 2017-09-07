using NLog;
using System;
using System.IO;

namespace UNP.Core.Helpers {

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
