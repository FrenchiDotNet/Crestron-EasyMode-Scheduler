using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;

namespace EasyMode {

    //-------------------------------------//
    //       Class | Scheduler
    // Description | ...
    //-------------------------------------//
    public static class Scheduler {

        //===================// Members //===================//

        private static CTimer sTimer;
        private static List<Schedule> schedules;
        private static long tickTime = 30000;

        public static string SavePath = "\\NVRAM\\EasyMode\\Scheduler\\";

        internal static bool _DebugEnabled;
        public static ushort DebugEnabled {
            get { return (ushort)(_DebugEnabled ? 1 : 0); }
            set { _DebugEnabled = value == 1 ? true : false; }
        }

        private static bool ioInUse;

        //===================// Constructor //===================//

        static Scheduler() {

            schedules = new List<Schedule>();

        }

        //===================// Methods //===================//

        //-------------------------------------//
        //      Method | DebugMessage
        // Description | Adds timestamp to outgoing console messages.
        //-------------------------------------//
        public static void DebugMessage(string _message) {

            if (Scheduler._DebugEnabled)
                CrestronConsole.PrintLine("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), _message);

        }

        //-------------------------------------//
        //      Method | setFilePath
        // Description | ...
        //-------------------------------------//
        public static void setFilePath(string _path) {

            SavePath = _path;

        }

        //-------------------------------------//
        //      Method | setTickTime
        // Description | ...
        //-------------------------------------//
        public static void setTickTime(ushort _val) {

            tickTime = (long)_val;
            StartScheduler();

        }

        //-------------------------------------//
        //      Method | createSchedule
        // Description | ...
        //-------------------------------------//
        public static void createSchedule(Schedule _sch, string _id) {

            DebugMessage(String.Format("[DEBUG] CreateSchedule called for new id [{0}]", _id));

            _sch.values = new ScheduleValues();
            _sch.values.id = _id;

            schedules.Add(_sch);

            if (!ioInUse)
                newScheduleFileCheck(_sch);

        }

        private static void newScheduleFileCheck(Schedule _sch) {

            string id    = _sch.values.id;
            string fPath = SavePath + id + ".txt";

            // Check for saved timer details

            ioInUse = true;

            if (FileManager.FileExists(fPath)) {

                DebugMessage(String.Format("[DEBUG] Schedule [{0}] exists, loading from file.", id));

                FileManager.ReadFromFile((_str) => {

                    ScheduleValues vtmp;

                    vtmp = JsonConvert.DeserializeObject<ScheduleValues>(_str);

                    _sch.values.dueTime = vtmp.dueTime;
                    _sch.values.enabled = vtmp.enabled;

                    if (_sch.scheduleCreatedEvent != null)
                        _sch.scheduleCreatedEvent();

                    // Asynchronous event, ready to call next schedule.
                    ioInUse = false;
                    _sch.initialized = true;
                    InitializeNextSchedule(id);

                }, fPath);

            } else {

                DebugMessage(String.Format("[DEBUG] No Schedule [{0}] exists, creating a new one.", id));

                _sch.values.dueTime = 0;
                _sch.values.enabled = false;

                _sch.SaveSchedule();

                if (_sch.scheduleCreatedEvent != null)
                    _sch.scheduleCreatedEvent();

                // Synchronous event, just call the next schedule
                ioInUse          = false;
                _sch.initialized = true;
                InitializeNextSchedule(id);

            }

        }

        //-------------------------------------//
        //      Method | InitializeNextSchedule
        // Description | ...
        //-------------------------------------//

        private static void InitializeNextSchedule(string _id) {

            int cnt = schedules.Count();

            DebugMessage(String.Format("[DEBUG] Schedule [{0}] initialized. Waiting for additional schedules...", _id));

            for (int i = 0; i < cnt; i++) {
                if (!schedules[i].initialized) {
                    newScheduleFileCheck(schedules[i]);
                    break;
                }
            }

        }

        //-------------------------------------//
        //      Method | StartScheduler
        // Description | ...
        //-------------------------------------//
        private static void StartScheduler() {

            sTimer = new CTimer(Tick, null, tickTime, tickTime);

            DebugMessage("[DEBUG] EasyMode Scheduler started.");

        }

        //-------------------------------------//
        //      Method | StopScheduler
        // Description | ...
        //-------------------------------------//
        private static void StopScheduler() {

            sTimer.Stop();

        }

        //===================// Event Handlers //===================//

        //-------------------------------------//
        //      Method | Tick
        // Description | ...
        //-------------------------------------//
        private static void Tick(object o) {

            int cnt = schedules.Count;
            double mins = Math.Floor(DateTime.Now.TimeOfDay.TotalMinutes);

            DebugMessage(String.Format("[DEBUG] Tick! Total Minutes: {0}", mins));

            for (int i = 0; i < cnt; i++) {
                if (schedules[i].values.enabled && schedules[i].values.dueTime == mins)
                    if (schedules[i].scheduleDueEvent != null)
                        schedules[i].scheduleDueEvent();
            }

        }

    } // End Scheduler class

    //-------------------------------------//
    //       Class | Schedule
    // Description | ...
    //-------------------------------------//
    public class Schedule {

        //===================// Members //===================//

        public ScheduleValues values;
        public bool initialized;

        public CTimer modTimeout;
        public bool timeoutActive;

        public DelegateEmpty scheduleCreatedEvent { get; set; }
        public DelegateEmpty scheduleDueEvent { get; set; }
        public DelegateUshort reportTimeEvent { get; set; }
        public DelegateUshort reportEnabledEvent { get; set; }

        public delegate SimplSharpString DelegateGetScheduleID();
        public DelegateGetScheduleID getScheduleID { get; set; }

        //===================// Methods //===================//

        //-------------------------------------//
        //      Method | ModifySchedule
        // Description | ...
        //-------------------------------------//
        public void ModifySchedule(short _mod) {

            int fTime = values.dueTime + _mod;

            if (fTime < 0)
                fTime = 1440 + fTime;
            else if (fTime > 1439)
                fTime = fTime - 1440;

            values.dueTime = fTime;

            if (reportTimeEvent != null)
                reportTimeEvent((ushort)values.dueTime);

            // Set/Reset 5s modification timer before saving changes
            if (!timeoutActive) {
                modTimeout = new CTimer(modTimeoutExpired, 5000);
                timeoutActive = true;
            } else {
                modTimeout.Stop();
                modTimeout.Reset(5000);
            }

        }

        //-------------------------------------//
        //      Method | EnableDisableSchedule
        // Description | ...
        //-------------------------------------//
        public void EnableDisableSchedule(ushort _val) {

            if (_val == 0)
                this.values.enabled = false;
            else if (_val == 1)
                this.values.enabled = true;
            else
                this.values.enabled = !this.values.enabled;

            if (reportEnabledEvent != null)
                reportEnabledEvent((ushort)(this.values.enabled ? 1 : 0));

            // Set/Reset 5s modification timer before saving changes
            if (!timeoutActive) {
                modTimeout = new CTimer(modTimeoutExpired, 5000);
                timeoutActive = true;
            } else {
                modTimeout.Stop();
                modTimeout.Reset(5000);
            }

        }

        //-------------------------------------//
        //      Method | modTimeoutExpired
        // Description | ...
        //-------------------------------------//
        private void modTimeoutExpired(object _o) {

            Scheduler.DebugMessage(String.Format("[DEBUG] Modification timeout reached for schedule [{0}]", this.values.id));

            SaveSchedule();
            timeoutActive = false;

        }

        //-------------------------------------//
        //      Method | SaveSchedule
        // Description | ...
        //-------------------------------------//
        public void SaveSchedule() {

            string fPath = Scheduler.SavePath + values.id + ".txt";

            if (this.values == null) {
                CrestronConsole.PrintLine("[ERROR] Encountered error on saving Schedule [{0}]: Schedule values returned null.");
                return;
            }


            try {
                FileManager.SaveToFile(fPath, JsonConvert.SerializeObject(this.values));
            }
            catch (Exception e) {
                CrestronConsole.PrintLine("[ERROR] Encountered error on saving Schedule [{0}]: {1}", this.values.id, e.Message);
            }

            Scheduler.DebugMessage(String.Format("[DEBUG] Saving Schedule [{0}] with dueTime {1}", values.id, values.dueTime));

        }

        //-------------------------------------//
        //      Method | RequestState
        // Description | ...
        //-------------------------------------//
        public void RequestState() {

            if (reportTimeEvent != null)
                reportTimeEvent((ushort)values.dueTime);
            if (reportEnabledEvent != null)
                reportEnabledEvent((ushort)(this.values.enabled ? 1 : 0));

        }

    } // End Schedule Class

    //-------------------------------------//
    //       Class | ScheduleValues
    // Description | ...
    //-------------------------------------//
    public class ScheduleValues {

        public bool enabled;
        public int dueTime;
        public string id;

    }

    public delegate void DelegateEmpty();
    public delegate void DelegateUshort(ushort value1);
    public delegate void DelegateUshort2(ushort value1, ushort value2);
    public delegate void DelegateString(SimplSharpString string1);

}