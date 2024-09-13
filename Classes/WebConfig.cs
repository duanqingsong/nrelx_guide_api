using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace nRelax.Tour.WebApp
{
    public class WebConfig
    {

        /// <summary>
        /// 服務條款URL
        /// </summary>
        public static string UrlServiceContract
        {
            get
            {
                return GetValue("UrlServiceContract");
            }
        }

        /// <summary>
        /// 支付方式URL
        /// </summary>
        public static string UrlPayWay
        {
            get
            {
                return GetValue("UrlPayWay");
            }
        }

        /// <summary>
        /// 保險細則
        /// </summary>
        public static string UrlInsurContract
        {
            get
            {
                return GetValue("UrlInsurContract");
            }
        }

        /// <summary>
        /// 上傳文件路徑
        /// </summary>
        public static string UploadFolder
        {
            get
            {
                return GetValue("UploadFolder");
            }
        }

        /// <summary>
        /// 授权文件标识
        /// </summary>
        public static string License {
            get {
                return GetValue("License");
            }
        }

        /// <summary>
        /// 小程序收客 SecKey
        /// </summary>
        public static string MiniAppKey
        {
            get
            {
                return GetValue("MiniAppKey");
            }
        }
        /// <summary>
        /// 收客小程序
        /// </summary>
        public static string MiniAppId
        {
            get
            {
                return GetValue("MiniAppId");
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
        public static string DataPortFilePath
        {
            get
            {
                return GetValue("DataPortFilePath");
            }
        }
        public static string DataPortDesKey
        {
            get
            {
                return GetValue("DataPortDesKey");
            }
        }
        /// <summary>
        /// 微信 网址
        /// </summary>
        public static string TourDetail_WxUri
        {
            get
            {
                return GetValue("TourDetail_WxUri");
            }
        }
        
        /// <summary>
        /// 草料二维码生成网址
        /// </summary>
        public static string CaoLiao_QrCodeUrl
        {
            get
            {
                return GetValue("CaoLiao_QrCodeUrl");
            }
        }

        /// <summary>
        /// 呼叫中服務器
        /// </summary>
        public static string CallCenterServer
        {
            get
            {
                return GetValue("CallCenterServer");
            }
        }

       /// <summary>
       /// 呼叫中心服務器端口
       /// </summary>
        public static string CallCenterPort
        {
            get
            {
                return GetValue("CallCenterPort");
            }
        }

        public static string RedisConnString
        {
            get
            {
                return GetValue("RedisConnString");
            }
        }

        /// <summary>
        /// 收据编号
        /// </summary>
        public static string ReceiptNo {
            get {
                return GetValue("ReceiptNo");
            }
        }

        /// <summary>
        /// 是否为分销模式
        /// </summary>
        public static bool Distribution
        {
            get
            {
                string sValue= GetValue("Distribution");
                if (sValue == "1")
                    return true;
                else
                    return false;
            }
        }

        public static bool UseSeatMap {
            get {
                string sv = GetValue("DontUseSeatForRegister");
                if (sv == "0")
                    return true;
                if (sv == "1")
                    return false;

                return false;
            }
        }

        public static bool DebugNotSendSMS
        {
            get
            {
                string sv = GetValue("DebugNotSendSMS");
                if (sv == "0")
                    return false;
                if (sv == "1")
                    return true;

                return false; //默认发短信
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