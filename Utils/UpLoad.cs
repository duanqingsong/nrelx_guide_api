using nRelax.Images.Common;
using nRelax.SSO;
using System;
using System.Web;

namespace nRelax.Tour.WebApp
{
    public class UpLoad
    {
      
        public UpLoad()
        {

        }

        
        public string UploadBase64File(string fileData, string filename, string fileExt, bool isWater, Enums.UploadType nType)
        {
            return UploadBase64File(fileData, filename, fileExt, isWater, nType, "");
        }
        public string UploadBase64File( string fileData,string filename, string fileExt, bool isWater, Enums.UploadType nType,string encryKey)
        {
            try
            {
                string newFileName = Utils.GetRamCode() + "." + fileExt; //随机生成新的文件名

                //检查文件扩展名是否合法
                //if (!CheckFileExt(fileExt))
                //{
                //    return "{\"status\": 0, \"msg\": \"不允许上传" + fileExt + "类型的文件！\"}";
                //}
                //获取文件字节数组
                byte[] byteFile = Convert.FromBase64String(fileData);
                int fileSize = byteFile.Length;
                //检查文件大小是否合法
                //if (!CheckFileSize(fileExt, fileSize))
                //{
                //    return "{\"status\": 0, \"msg\": \"文件超过限制的大小！\"}";
                //}
                SystemUser user = CurrentSysUser.Get();

                FileModel model = new FileModel();
                model.Type = (int)nType;
                model.FileExt = fileExt;
                model.FileName = filename.Length==0? newFileName:filename;
                model.FileSize = fileSize;
                model.ByteFile = HttpUtility.UrlEncode(fileData);
                model.IsThumbnail = 0;
                model.IsWater = isWater ? 1 : 0;
                model.OwnerId = (int)user.UserId;
                if (encryKey.Length > 0)
                {
                    model.IsEncry = 1;
                    model.EncryKey = encryKey;
                }
                //接收回传数据
                string msg = HttpWebClient.Send(model);
                return msg;
            }
            catch(Exception ex)
            {
                Logger.Error(ex);
                return "{\"status\": 0, \"msg\": \"上传过程中发生意外错误！\"}";
            }
        }
     

       
    }
}
