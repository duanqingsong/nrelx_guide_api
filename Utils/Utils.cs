using System;

namespace nRelax.Tour.WebApp
{
    public class Utils
    {
      
        /// <summary>
        /// 生成日期随机码
        /// </summary>
        /// <returns></returns>
        public static string GetRamCode()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmssffff");
        }
    }
}