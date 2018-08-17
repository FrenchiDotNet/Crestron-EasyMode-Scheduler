using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;

namespace EasyMode {

    //-------------------------------------//
    //       Class | FileManager
    // Description | ...
    //-------------------------------------//
    public static class FileManager {

        //===================// Members //===================//

        private static FileStream fStream;
        private static StreamReader sReader;

        //===================// Constructor //===================//

        static FileManager() {

        }

        //===================// Methods //===================//

        //-------------------------------------//
        //      Method | SaveToFile
        // Description | ...
        //-------------------------------------//
        public static void SaveToFile(string _fpath, string _data) {

            byte[] bytes = Encoding.ASCII.GetBytes(_data);
            int numBytes = bytes.Length;

            fStream = new FileStream(_fpath, FileMode.Create);
            fStream.BeginWrite(bytes, 0, numBytes, BeginWriteCallback, null);

            if(Scheduler._DebugEnabled)
                CrestronConsole.PrintLine(String.Format("[DEBUG] Beginning write of {0} bytes to file {1}...", numBytes, _fpath));

        }

        //-------------------------------------//
        //      Method | ReadFromFile
        // Description | ...
        //-------------------------------------//
        public static void ReadFromFile(Action<string> _callback, string _fpath) {

            string errString = "";
            bool error = false;
            StringBuilder result = new StringBuilder();

            try {
                fStream = new FileStream(_fpath, FileMode.OpenOrCreate);
            }
            catch (FileNotFoundException) {
                error = true;
                errString = "File \"{0}\" not found!";
            }
            catch (Exception e) {
                error = true;
                errString = e.Message;
            }

            if (error) {
                CrestronConsole.PrintLine(String.Format("[ERROR] {0}", errString));
                return;
            }

            sReader = new StreamReader(fStream);
            result.Append(sReader.ReadToEnd());

            _callback(result.ToString());

            sReader.Close();
            fStream.Close();

        }

        //-------------------------------------//
        //      Method | FileExists
        // Description | ...
        //-------------------------------------//
        public static bool FileExists(string _fpath) {

            return File.Exists(_fpath);

        }

        //===================// Event Handlers //===================//

        //-------------------------------------//
        //      Method | BeginWriteCallback
        // Description | ...
        //-------------------------------------//
        private static void BeginWriteCallback(Crestron.SimplSharp.CrestronIO.IAsyncResult _res) {

            if (_res.IsCompleted) {
                fStream.EndWrite(_res);
                fStream.Close();
                if (Scheduler._DebugEnabled)
                    CrestronConsole.PrintLine(String.Format("[DEBUG] Completed File Write Operation!"));
            }

        }

    } // End FileManager class

}