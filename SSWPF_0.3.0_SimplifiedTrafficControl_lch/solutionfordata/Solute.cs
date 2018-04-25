using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data;
using MySql.Data.MySqlClient;


namespace solutionfordata
{
    public class Solute
    {
       string Myconnectingstring = "Server=localhost;Database=terminal;Uid=root;Pwd=1234;";

       public MySqlConnection GetConn()
       {
           MySqlConnection mysqlconn = new MySqlConnection(Myconnectingstring);
           return mysqlconn;
       }

       public void initSJK()
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();

           MySql.Data.MySqlClient.MySqlCommand cmd;

           try
           {
               string ostr1 = @"CREATE TABLE IF NOT EXISTS agv(
                NO INT UNSIGNED AUTO_INCREMENT,
                agvid int,
                x int,
                y int, 
                angle int,
                PRIMARY KEY (NO)
                )ENGINE=InnoDB DEFAULT CHARSET=utf8;";
               string ostr2 = @"CREATE TABLE IF NOT EXISTS dataintime(
                agvid int,
                x int,
                y int, 
                angle int,
                CurrLaneID int,
                NextLaneID int,
                AimLaneID int,
                CurrAGVline int,
                Deviation int
                )ENGINE=InnoDB DEFAULT CHARSET=utf8;";
               string ostr3 = @"CREATE TABLE IF NOT EXISTS agv_lane(
                agvid int,
                laneid int
                )ENGINE=InnoDB DEFAULT CHARSET=utf8;";
               string ostr4 = @"CREATE TABLE IF NOT EXISTS agv_agvline(
                agvid int,
                AGVlineid int
                )ENGINE=InnoDB DEFAULT CHARSET=utf8;";

               cmd = mc.CreateCommand();

               cmd.CommandText = ostr1;
               cmd.ExecuteNonQuery();

               cmd.CommandText = ostr2;
               cmd.ExecuteNonQuery();

               cmd.CommandText = ostr3;
               cmd.ExecuteNonQuery();

               cmd.CommandText = ostr4;
               cmd.ExecuteNonQuery();
               mc.Close();
           }
           catch (Exception)
           {

               throw;
           }
       }

       public void DeleteAllData(string table)
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();

           MySql.Data.MySqlClient.MySqlCommand cmd;
            try
           {
               cmd = mc.CreateCommand();
               string str = "truncate " + table+";";
               cmd.CommandText = str;
               cmd.ExecuteNonQuery();
               mc.Close();
           }
           catch (Exception)
           {

               throw;
           }
       }

       //table为表名称
       //2个num对应2个str
       public void Add(int num_1, int num_2, string str_1, string str_2, string table)
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();

           MySql.Data.MySqlClient.MySqlCommand cmd;
           try
           {
               cmd = mc.CreateCommand();
               string str = "insert into " + table + "(" + str_1 + "," + str_2+") values(@a,@b);";
               cmd.CommandText = str;
               cmd.Parameters.AddWithValue("@a", num_1);
               cmd.Parameters.AddWithValue("@b", num_2);
               cmd.ExecuteNonQuery();
               mc.Close();
           }
           catch (Exception)
           {

               throw;
           }
       }
       //在末尾添加一行
       //table为表名称
       //4个num对应4个str
       public void Add(int num_1, int num_2, int num_3, int num_4, string str_1, string str_2, string str_3, string str_4, string table)
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();

           MySql.Data.MySqlClient.MySqlCommand cmd;
           try
           {
               cmd = mc.CreateCommand();
               string str = "insert into " + table + "(" + str_1 + "," + str_2 + "," + str_3 + "," + str_4 + ") values(@a,@b,@c,@d);";
               cmd.CommandText = str;
               cmd.Parameters.AddWithValue("@a", num_1);
               cmd.Parameters.AddWithValue("@b", num_2);
               cmd.Parameters.AddWithValue("@c", num_3);
               cmd.Parameters.AddWithValue("@d", num_4);
               cmd.ExecuteNonQuery();
               mc.Close();
           }
           catch (Exception)
           {

               throw;
           }
       }

       //在末尾添加一行
       //table为表名称
       //9个num对应9个str
       public void Add(int num_1, int num_2, int num_3, int num_4, int num_5, int num_6, int num_7, int num_8, int num_9, string str_1, string str_2, string str_3, string str_4, string str_5, string str_6, string str_7, string str_8, string str_9, string table)
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();

           MySql.Data.MySqlClient.MySqlCommand cmd;
           try
           {
               cmd = mc.CreateCommand();
               string str = "insert into " + table + "(" + str_1 + "," + str_2 + "," + str_3 + "," + str_4 + "," + str_5 + "," + str_6 + "," + str_7 + "," + str_8 + "," + str_9 + ") values(@a,@b,@c,@d,@e,@f,@g,@h,@i);";
               cmd.CommandText = str;
               cmd.Parameters.AddWithValue("@a", num_1);
               cmd.Parameters.AddWithValue("@b", num_2);
               cmd.Parameters.AddWithValue("@c", num_3);
               cmd.Parameters.AddWithValue("@d", num_4);
               cmd.Parameters.AddWithValue("@e", num_5);
               cmd.Parameters.AddWithValue("@f", num_6);
               cmd.Parameters.AddWithValue("@g", num_7);
               cmd.Parameters.AddWithValue("@h", num_8);
               cmd.Parameters.AddWithValue("@i", num_9);
               cmd.ExecuteNonQuery();
               mc.Close();
           }
           catch (Exception)
           {

               throw;
           }
       }

       //读取一行中某项数据
       //num为行数
       //ID为表中排序项的头名称
       //select为要读取的那一项的头名称
       //table为表名称
       public int Read(int num, string id, string select, string table)
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();
           int result;

           MySqlCommand cmd;
           try
           {
               string sql = "select " + select + " from " + table + " where " + id + "=" + num.ToString() + ";";
               cmd = new MySqlCommand(sql, mc);
               MySqlDataReader reader = cmd.ExecuteReader();

               if (reader.Read())
               {
                   result = reader.GetInt32(0);
                   mc.Close();
                   return result;
               }
               mc.Close();
               return 0;

           }
           catch (Exception)
           {

               throw;
           }
       }

       //读取某项数据
       //num为行数
       //ID为表中排序项的头名称
       //select为要读取的那一项的头名称
       //table为表名称
       public List<int> ReadLine(int num, string id, string select, string table)
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();
           List<int> result = new List<int>();

           MySqlCommand cmd;
           try
           {
               string sql = "select " + select + " from " + table + " where " + id + "=" + num.ToString() + ";";
               cmd = new MySqlCommand(sql, mc);
               MySqlDataReader reader = cmd.ExecuteReader();

               while (reader.Read())
               {
                   result.Add(reader.GetInt32(0));
               }

               mc.Close();
               return result;

           }
           catch (Exception)
           {

               throw;
           }
       }

       //删除ID为某值的所有行
       //no为ID的选取值
       //table为表名称
       //ID为选取的排序项头名称
       public void DeleteLine(int no, string table, string id)//删除ID为某值的所有行
       {
           MySqlConnection mc = this.GetConn();
           mc.Open();

           MySqlCommand cmd = new MySqlCommand();
           try
           {
               cmd.Connection = mc;
               string str = "DELETE FROM " + table + " WHERE " + id + "=" + no.ToString() + ";";
               cmd.CommandText = str;
               int count = cmd.ExecuteNonQuery();
               mc.Close();
           }
           catch (Exception)
           {

               throw;
           }
       }
    }
}
