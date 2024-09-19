using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;

namespace nRelax.Tour.WebApp
{
    public static class License
    {
        private static string licenseCacheKey = "uwuUjhs293";
        //private static Dictionary<string,DateTime>  licenseCashe=new Dictionary<string, DateTime>();
        public static string  SysTitle { get; set; }
        public static string  Theme { get; set; }

        public static string CompanyName { get; set; }
        public static bool  Pass { get; set; }

        public static int ExpireDays { get; set; }
        private static DateTime ExpreDate { get; set; }

        public new static string ToString()
        {
            string str = "SysTitle:{0},Theme:{1},CompanyName:{2},ExpreDate:{3},Pass:{4}";
            string sResult = string.Format(str, License.SysTitle, License.Theme, License.CompanyName, 
                License.ExpreDate.ToString("yyyy-MM-dd"), License.Pass);
            return sResult;
        }
        public static void  LoadInfo() {

            if (HttpContext.Current.Cache[licenseCacheKey] != null)
            {
                Logger.Debug("license allready in cache");
                Dictionary<string, DateTime> licenseCashe = HttpContext.Current.Cache[licenseCacheKey] as Dictionary<string, DateTime>;
                DateTime dtExpire = (DateTime)licenseCashe["expireDate"] ;
                if (dtExpire > DateTime.Now)
                    LoadLicenseData();
            }
            else
            {
                Logger.Debug("license cache is null");
                LoadLicenseData();

                Dictionary<string, DateTime> licenseCashe = new Dictionary<string, DateTime>();
                licenseCashe.Add("expireDate", DateTime.Now.AddHours(24));

                //if (!licenseCashe.ContainsKey("expireDate"))
                //    licenseCashe.Add("expireDate", DateTime.Now.AddHours(24));
                //else
                //    licenseCashe["expireDate"] = DateTime.Now.AddHours(24);

                HttpContext.Current.Cache[licenseCacheKey] = licenseCashe;
            }

        }

        private static void LoadLicenseData() {
            string filePath = HttpContext.Current.Server.MapPath("~/license.sys");
            if (File.Exists(filePath))
            {
                string smd5 = GetMD5HashFromFile(filePath);
                string slicense = WebConfig.License;

                if (smd5 != slicense)
                {
                    Pass = false;
                    return;
                }

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);
                XmlNode oNode = xmlDoc.DocumentElement;
                if (oNode.HasChildNodes)
                {
                    XmlNode systitle = xmlDoc.SelectSingleNode("/root/systitle");
                    if (systitle != null)
                        SysTitle = systitle.InnerText.TrimStart().TrimEnd();
                    else
                        Pass = false;


                    XmlNode theme = xmlDoc.SelectSingleNode("/root/theme");
                    if (theme != null)
                        Theme = theme.InnerText.TrimStart().TrimEnd();
                    else
                        Pass = false;

                    XmlNode companyname = xmlDoc.SelectSingleNode("/root/companyname");
                    if (companyname != null)
                        CompanyName = companyname.InnerText.TrimStart().TrimEnd();
                    else
                        Pass = false;

                    XmlNode expire = xmlDoc.SelectSingleNode("/root/expire");
                    if (expire != null)
                    {
                        string dtExpire = expire.InnerText.TrimStart().TrimEnd();
                        DateTime dt = DateTime.Parse(dtExpire);
                        ExpreDate = dt;
                        ExpireDays = (dt - DateTime.Now).Days;
                        if (dt < DateTime.Now)
                        {
                            Pass = false;
                        }
                        else
                            Pass = true;
                    }
                    else
                        Pass = false;

                }

            }
            else
            {
                Logger.Error("未找到 license.sys 文件");
                Pass = false;
            }
        }
        private static string GetMD5HashFromFile(string fileName)
        {
            try
            {
                using (FileStream file = new FileStream(fileName, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { 
                    MD5 md5 = new MD5CryptoServiceProvider();
                    byte[] retVal = md5.ComputeHash(file);
                    file.Close();
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < retVal.Length; i++)
                    {
                        sb.Append(retVal[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return "";
                //throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }
        }  
    }
}