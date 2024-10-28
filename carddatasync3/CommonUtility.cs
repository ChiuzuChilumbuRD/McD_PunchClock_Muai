/* Copyright (C) 2021 Qisda - All Rights Reserved */

using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace carddatasync3
{
    class CommonUtility
    {
        public static bool is_valid_org_code(string org_code)
        {
            if (org_code.Length == 5)
            {
                Regex r = new Regex(@"\d{5}");
                if (r.IsMatch(org_code))
                    return true;
            }
            return false;
        }
        public static JObject get_machine_info()
        {
            JObject ret_obj = new JObject();
            List<string> ip_list = new List<string>();
            string str_ip = string.Empty;
            var host = Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ip_list.Add(ip.ToString());
                }
            }
            ret_obj["machine_ip"] = string.Join(";", ip_list);
            ret_obj["machine_name"] = host.HostName;
            return ret_obj;
        }
    }


}
