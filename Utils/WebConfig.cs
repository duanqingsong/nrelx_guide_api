namespace nRelax.Tour.WebApp
{
    public class WebConfig
    {
        /// <summary>
        /// 授权文件标识
        /// </summary>
        public static string License {
            get {
                return GetValue("License");
            }
        }
        
        /// <summary>
        /// 导游助手小程序
        /// </summary>
        public static string GuideAppKey
        {
            get
            {
                return GetValue("GuideAppKey");
            }
        }
        /// <summary>
        /// 导游助手小程序
        /// </summary>
        public static string GuideAppId
        {
            get
            {
                return GetValue("GuideAppId");
            }
        }
        
        

        public static string GetValue(string key) {
            string sValue=System.Configuration.ConfigurationManager.AppSettings[key];
            if (sValue == null) {
                return "";
            }
            return sValue;
        }

        public static bool IsExist(string key) {
            if (System.Configuration.ConfigurationManager.AppSettings[key] != null)
                return true;
            else
                return false;
        }
    }
}