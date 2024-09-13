using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace nRelax.Tour.WebApp
{
    public class UploadConfig
    {
        public UploadConfig() { }
        public UploadConfig LoadConfig() {
            string sPath=HttpContext.Current.Server.MapPath("~/config/UploadConfig.json");
            Logger.Error("UploadConfig.json path=" + sPath);

            if (File.Exists(sPath))
            {
                string sConfig = File.ReadAllText(sPath);
                UploadConfig config = JsonConvert.DeserializeObject<UploadConfig>(sConfig);
                return config;
            }
            else
            {
                Logger.Error("未找到"+ sPath+"文件");
            }
            return new UploadConfig();
        }
        
        public int imgmaxheight { get; set; }

        public int imgmaxwidth { get; set; }

        public string webpath { get; set; }

        public string filepath { get; set; }

        public int thumbnailwidth { get; set; }

        public int thumbnailheight { get; set; }

        public int watermarktype { get; set; }

        public string watermarktext { get; set; }

        public int watermarkposition { get; set; }

        public int watermarkimgquality { get; set; }

        public string watermarkfont { get; set; }

        public string watermarkpic { get; set; }

        public int watermarktransparency { get; set; }
        public int watermarkfontsize { get; set; }
        public int imgsize { get; set; }
        public int videosize { get; set; }
        public int attachsize { get; set; }
        public string videoextension { get; set; }
        public string fileextension { get; set; }
        public int filesave { get; set; }
    }
}