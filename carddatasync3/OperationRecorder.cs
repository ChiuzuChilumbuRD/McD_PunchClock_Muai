using System;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace carddatasync3
{
    public class OperationRecorder
    {
        public string unitcode { get; set; }
        public string operation { get; set; }
        public DateTime start_time { get; set; }
        public DateTime end_time { get; set; }
        public string result { get; set; }
        public string log { get; set; }

        // Constructor
        public OperationRecorder()
        {
            start_time = DateTime.Now;
        }

        // Upload log method
        public void upload_log(string str_db_key)
        {
            // Get machine info
            JObject machine_info = CommonUtility.get_machine_info();

            // Initialize DBFactory and handler
            DBFactory db_factory = new DBFactory();
            var db_handler = db_factory.create_handler(str_db_key);

            // Get assembly version
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = $"v{fileVersionInfo.ProductVersion}";

            // Update operation log
            db_handler.update_operation_log(
                unitcode,
                machine_info["machine_name"].ToString(),
                machine_info["machine_ip"].ToString(),
                version,
                operation,
                start_time,
                end_time,
                result,
                log
            );
        }
    }
}
