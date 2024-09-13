using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace nRelax.Tour.WebApp
{
    public class Logger
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger("nRelaxLogger");
        /// <summary>
        /// 写入Info日志
        /// </summary>
        /// <param name="strMessage"></param>
        public static void Info(string strMessage)
        {
            string sMessage = BuildMessage(strMessage,"Info");
            log.Info(sMessage);
        }

        /// <summary>
        /// 写入Debug日志
        /// </summary>
        /// <param name="strMessage"></param>
        public static void Debug(string strMessage)
        {
            string sMessage = BuildMessage(strMessage,"Debug");
            log.Debug(sMessage);
        }

        /// <summary>
        /// 写入错误日志,URL,Message
        /// </summary>
        /// <param name="strMessage"></param>
        public static void Error(string strMessage)
        {
            string sMessage = BuildMessage(strMessage,"Error");

            log.Error(sMessage);
        }

       
        /// <summary>
        /// 写入错误日志 URL,Source,Message,Stack trace
        /// </summary>
        /// <param name="exception"></param>
        public static void Error(Exception ex)
        {
            Exception exception = ex.InnerException != null ? ex.InnerException : ex;
            string sMessage = BuildMessage(exception.Message, "Error");

            StringBuilder sbErrInfo = new StringBuilder();
            sbErrInfo.Append(Environment.NewLine + sMessage);
            sbErrInfo.Append(Environment.NewLine + "Source: " + exception.Source);
            sbErrInfo.Append(Environment.NewLine + "Stack trace: " + exception.StackTrace);
            
            log.Error(sbErrInfo.ToString());
        }

        private static string BuildMessage(string strMessage, string sType)
        {
            StringBuilder sbErrInfo = new StringBuilder();
            sbErrInfo.Append(Environment.NewLine + "<=====================================" + System.DateTime.Now.ToString() + "=============================================>");
            sbErrInfo.Append(Environment.NewLine + "Level: " + sType);
            sbErrInfo.Append(Environment.NewLine + "URL: " + HttpContext.Current.Request.Url.ToString());
            sbErrInfo.Append(Environment.NewLine + "Message: " + strMessage);

            return sbErrInfo.ToString();
        }
    }
}