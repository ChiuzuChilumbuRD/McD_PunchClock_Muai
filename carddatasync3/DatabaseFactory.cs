/* Copyright (C) 2021 Qisda - All Rights Reserved */

using System.Data;
using System.Data.Common;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Serilog;


namespace carddatasync3
{
    class DBFactory
    {
        public DBHandler create_handler(string db_key)
        {
            DBHandler db_handler = new DBHandler(db_key);
            return db_handler;
        }
    }

    class DBHandler
    {
        private string str_db_key;
        public DBHandler(string db_key)
        {
            str_db_key = db_key;
        }
        private DBHandler()
        {

        }
        
        public string get_card_reader_ip(string str_unit_code)
        {
            string ip = string.Empty;
            string sql = string.Format(@"SELECT ATTR.AttrValue
                                        FROM ORGSTDUNITATTR ATTR INNER JOIN VW_MDS_ORGSTDSTRUCT VW ON ATTR.UnitID=VW.unitid
                                        WHERE ATTR.AttrID='1e723f1b-eb77-4bf7-90cd-32ad88108586' and VW.unitcode='{0}'", str_unit_code);
            try
            {
                Database db = DatabaseFactory.CreateDatabase(str_db_key);
                DbCommand dc = db.GetSqlStringCommand(sql);
                Log.Verbose("SQL command: " + sql);
                DataSet ds = db.ExecuteDataSet(dc);

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    ip = ds.Tables[0].Rows[0][0].ToString();
                }
                Log.Information("The ip is:" + ip);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "get card reader ip failed.");
            }

            return ip;
        }

        public string get_card_reader_ip_2(string str_unit_code)
        {
            string ip = string.Empty;
            string sql = string.Format(@"SELECT ATTR.AttrValue
                                        FROM ORGSTDUNITATTR ATTR INNER JOIN VW_MDS_ORGSTDSTRUCT VW ON ATTR.UnitID=VW.unitid
                                        WHERE ATTR.AttrID='1e723f1b-eb77-4bf7-90cd-32ad88108586' and VW.unitcode='{0}'", str_unit_code);
            ip = query_single_element(sql);
            return ip;
        }

        public int get_success_count(string str_unit_code)
        {
            string str_count = string.Empty;
            string str_sql = "SELECT COUNT(*) AS SUCCESS_COUNT "
                           + "FROM CARD_DATA_TRANSFER_LOG "
                           + "WHERE RESULT='Success' "
                           + $"AND UNITCODE='{str_unit_code}'";
            str_count = query_single_element(str_sql);
            return int.Parse(str_count);
        }

        public void update_operation_log(string str_unit_code,
                                         string str_machine_name,
                                         string str_machine_ip,   
                                         string str_sw_version,
                                         string str_operation,
                                         DateTime start_time,
                                         DateTime end_time,
                                         string str_result,
                                         string str_log )
        {
            str_unit_code = purify_string(str_unit_code);
            str_machine_name = purify_string(str_machine_name);
            str_machine_ip = purify_string(str_machine_ip);
            str_sw_version = purify_string(str_sw_version);
            str_operation = purify_string(str_operation);
            str_result = purify_string(str_result);
            str_log = purify_string(str_log);
            string str_sql = "INSERT INTO CARD_DATA_OPERATION_LOG "
                           + "(UNITCODE, MACHINE_NAME, MACHINE_IP, SW_VERSION, OPERATION, START_TIME, END_TIME, RESULT, LOG) "
                           + "VALUES ("
                           + $"'{str_unit_code}','{str_machine_name}','{str_machine_ip}','{str_sw_version}','{str_operation}',"
                           + $"'" + start_time.ToString("yyyy-MM-dd HH:mm:ss") + "',"
                           + $"'" + end_time.ToString("yyyy-MM-dd HH:mm:ss") + "',"
                           + $"'{str_result}','{str_log}')";
            try
            {
                Database db = DatabaseFactory.CreateDatabase(str_db_key);
                using (DbCommand dc = db.GetSqlStringCommand(str_sql))
                {
                    Log.Verbose("SQL command: " + str_sql);
                    db.ExecuteNonQuery(dc);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "update operation log failed.");
            }
            
        }

        public int update_card_num_cmd(string str_emp_id,
                                      string str_card_num)   
        {
            str_emp_id = purify_string(str_emp_id);
            str_card_num = purify_string(str_card_num);
            int update_count = 0;
            string str_sql = "UPDATE VW_MDS_PSNACCOUNT "
                            + $"SET CARDNUM='{str_card_num}' "
                            + $"WHERE EMPLOYEEID='{str_emp_id}'"; 
            try
            {
                Database db = DatabaseFactory.CreateDatabase(str_db_key);
                using (DbCommand dc = db.GetSqlStringCommand(str_sql))
                {
                    Log.Verbose("SQL command: " + str_sql);
                    update_count = db.ExecuteNonQuery(dc);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "update operation log failed.");
                update_count = -2;
            }
            return update_count;
        }

        
        private string query_single_element(string str_SQL)
        {
            string str_ret = string.Empty;

            try
            {
                Database db = DatabaseFactory.CreateDatabase(str_db_key);
                using (DbCommand dc = db.GetSqlStringCommand(str_SQL))
                {
                    Log.Verbose("SQL command: " + str_SQL);
                    DataSet ds = db.ExecuteDataSet(dc);

                    if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        str_ret = ds.Tables[0].Rows[0][0].ToString();
                    }
                    Log.Information("The resultis:" + str_ret);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "query_single_element failed.");
            }
            
            return str_ret;
        }

        public string purify_string(string str_src)
        {
            return str_src.Replace("'", "''");
        }

        public static string get_user_id_in_conn_string(string conn_string)
        {
            return get_conn_string_property(conn_string, "User id");
        }

        public static string get_conn_string_property(string conn_string, string str_key)
        {
            Object value = null;
            string str_ret = string.Empty;
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
            builder.ConnectionString = conn_string;
            try
            {
                if (builder.TryGetValue(str_key, out value))
                {
                    str_ret = value.ToString();
                }
                else
                {
                    Console.WriteLine(@"Unable to retrieve value for '{0}'", str_key);
                }
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("Unable to retrieve value for null key.");
            }
            return str_ret;
        }


    }
}
