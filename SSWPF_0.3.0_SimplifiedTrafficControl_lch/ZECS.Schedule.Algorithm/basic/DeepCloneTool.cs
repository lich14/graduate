using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 克隆工具类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-3-20       ver1.0
    /// </summary>
    public class DeepCloneTool
    {
        ///<summary>
        ///  返回一个对象的深度克隆
        /// </summary>
      public T GetClone<T>(T entity)
      {
          MemoryStream memoryStream = new MemoryStream();
          BinaryFormatter formatter = new BinaryFormatter();
          formatter.Serialize(memoryStream, entity);
          memoryStream.Position = 0;
          return (T)formatter.Deserialize(memoryStream);
      }

      /// <summary>
      /// 返回对象列表的深度克隆
      /// </summary>
      public List<T> GetCloneList<T>(List<T> entitylist)
      {
          MemoryStream memoryStream = new MemoryStream();
          BinaryFormatter formatter = new BinaryFormatter();
          formatter.Serialize(memoryStream, entitylist);
          memoryStream.Position = 0;
          return (List<T>)formatter.Deserialize(memoryStream);
      }

      /// <summary>
      /// 返回二维对象列表的深度克隆
      /// </summary>
      public List<List<T>> GetCloneListList<T>(List<List<T>> listentitylist)
      {
          MemoryStream memoryStream = new MemoryStream();
          BinaryFormatter formatter = new BinaryFormatter();
          formatter.Serialize(memoryStream, listentitylist);
          memoryStream.Position = 0;
          return (List<List<T>>)formatter.Deserialize(memoryStream);
      }

    }
}
