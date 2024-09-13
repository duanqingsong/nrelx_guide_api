using System;
using System.Collections;
using System.Web;
using System.IO;
using System.Drawing;
using System.Net;
using nRelax.Images.Common;
using nRelax.SSO;
using nRelax.DevBase.BaseTools;

namespace nRelax.Tour.WebApp
{
    public class UpLoad
    {
        private UploadConfig uploadConfig;

        public UpLoad()
        {
            uploadConfig = new UploadConfig().LoadConfig();
        }

        /// <summary>
        /// 裁剪图片并保存
        /// </summary>
        public bool cropSaveAs(string fileName, string newFileName, int maxWidth, int maxHeight, int cropWidth, int cropHeight, int X, int Y)
        {
            string fileExt = Utils.GetFileExt(fileName); //文件扩展名，不含“.”
            if (!IsImage(fileExt))
            {
                return false;
            }
            string newFileDir = Utils.GetMapPath(newFileName.Substring(0, newFileName.LastIndexOf(@"/") + 1));
            //检查是否有该路径，没有则创建
            if (!Directory.Exists(newFileDir))
            {
                Directory.CreateDirectory(newFileDir);
            }
            try
            {
                string fileFullPath = Utils.GetMapPath(fileName);
                string toFileFullPath = Utils.GetMapPath(newFileName);
                return Thumbnail.MakeThumbnailImage(fileFullPath, toFileFullPath, 180, 180, cropWidth, cropHeight, X, Y);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 文件上传方法
        /// </summary>
        /// <param name="postedFile">文件流</param>
        /// <param name="isThumbnail">是否生成缩略图</param>
        /// <param name="isWater">是否打水印</param>
        /// <returns>上传后文件信息</returns>
        public string fileSaveAs(HttpPostedFile postedFile, bool isThumbnail, bool isWater,int nType)
        {
            try
            {
                string fileExt = Utils.GetFileExt(postedFile.FileName); //文件扩展名，不含“.”
                int fileSize = postedFile.ContentLength; //获得文件大小，以字节为单位
                string fileName = postedFile.FileName.Substring(postedFile.FileName.LastIndexOf(@"\") + 1); //取得原文件名
                string newFileName = Utils.GetRamCode() + "." + fileExt; //随机生成新的文件名
                string newThumbnailFileName = "thumb_" + newFileName; //随机生成缩略图文件名
                string upLoadPath = GetUpLoadPath(); //上传目录相对路径
                string fullUpLoadPath = Utils.GetMapPath(upLoadPath); //上传目录的物理路径
                string newFilePath = upLoadPath + newFileName; //上传后的路径
                string newThumbnailPath = upLoadPath + newThumbnailFileName; //上传后的缩略图路径

                //检查文件扩展名是否合法
                if (!CheckFileExt(fileExt))
                {
                    return "{\"status\": 0, \"msg\": \"不允许上传" + fileExt + "类型的文件！\"}";
                }
                //检查文件大小是否合法
                if (!CheckFileSize(fileExt, fileSize))
                {
                    return "{\"status\": 0, \"msg\": \"文件超过限制的大小！\"}";
                }
                //获取文件字节数组
                byte[] byteFile = ByteFiles(postedFile);

                SystemUser user = CurrentSysUser.Get();

                FileModel model = new FileModel();
                model.Type = nType;
                model.FileExt = fileExt;
                model.FileSize = fileSize;
                model.FileName = fileName;
                model.ByteFile = HttpUtility.UrlEncode(Convert.ToBase64String(byteFile));
                model.IsThumbnail = isThumbnail?1:0;
                model.IsWater = isWater ? 1 : 0;
                model.OwnerId = (int)user.UserId;
                //string strResult = JsonConvert.SerializeObject(model);
               //Logger.Error("上傳文件model="+ strResult);
                //受保護模式,要加密
                if (nType == (int)Enums.UploadType.Protect) {
                    string skey = new Random().Next(100000000).ToString();
                    model.IsEncry = 1;
                    model.EncryKey = skey;
                }
                //string sValue = ConfigTool.GetParameterValue("systemconfig", "UploadImgUrl");
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
                if (!CheckFileExt(fileExt))
                {
                    return "{\"status\": 0, \"msg\": \"不允许上传" + fileExt + "类型的文件！\"}";
                }
                //获取文件字节数组
                byte[] byteFile = Convert.FromBase64String(fileData);
                int fileSize = byteFile.Length;
                //检查文件大小是否合法
                if (!CheckFileSize(fileExt, fileSize))
                {
                    return "{\"status\": 0, \"msg\": \"文件超过限制的大小！\"}";
                }
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
        private byte[] ByteFiles(HttpPostedFile postedFile)
        {
            int fileLen = postedFile.ContentLength;//图片长度
            Byte[] byteFile = new Byte[fileLen];
            //读取文件内容
            Stream sr = postedFile.InputStream;
            sr.Read(byteFile, 0, fileLen);
            //释放资源
            sr.Dispose();
            //drimg.Dispose();

            return byteFile;

        }
        public string fileSaveAsToLocal(HttpPostedFile postedFile, bool isThumbnail, bool isWater)
        {
            try
            {
                string fileExt = Utils.GetFileExt(postedFile.FileName); //文件扩展名，不含“.”
                int fileSize = postedFile.ContentLength; //获得文件大小，以字节为单位
                string fileName = postedFile.FileName.Substring(postedFile.FileName.LastIndexOf(@"\") + 1); //取得原文件名
                string newFileName = Utils.GetRamCode() + "." + fileExt; //随机生成新的文件名
                string newThumbnailFileName = "thumb_" + newFileName; //随机生成缩略图文件名
                string upLoadPath = GetUpLoadPath(); //上传目录相对路径
                string fullUpLoadPath = Utils.GetMapPath(upLoadPath); //上传目录的物理路径
                string newFilePath = upLoadPath + newFileName; //上传后的路径
                string newThumbnailPath = upLoadPath + newThumbnailFileName; //上传后的缩略图路径

                //检查文件扩展名是否合法
                if (!CheckFileExt(fileExt))
                {
                    return "{\"status\": 0, \"msg\": \"不允许上传" + fileExt + "类型的文件！\"}";
                }
                //检查文件大小是否合法
                if (!CheckFileSize(fileExt, fileSize))
                {
                    return "{\"status\": 0, \"msg\": \"文件超过限制的大小！\"}";
                }
                //检查上传的物理路径是否存在，不存在则创建
                if (!Directory.Exists(fullUpLoadPath))
                {
                    Directory.CreateDirectory(fullUpLoadPath);
                }

                //保存文件
                postedFile.SaveAs(fullUpLoadPath + newFileName);
                //如果是图片，检查图片是否超出最大尺寸，是则裁剪
                if (IsImage(fileExt) && (this.uploadConfig.imgmaxheight > 0 || this.uploadConfig.imgmaxwidth > 0))
                {
                    Thumbnail.MakeThumbnailImage(fullUpLoadPath + newFileName, fullUpLoadPath + newFileName,
                        this.uploadConfig.imgmaxwidth, this.uploadConfig.imgmaxheight);
                }
                //如果是图片，检查是否需要生成缩略图，是则生成
                if (IsImage(fileExt) && isThumbnail && this.uploadConfig.thumbnailwidth > 0 && this.uploadConfig.thumbnailheight > 0)
                {
                    Thumbnail.MakeThumbnailImage(fullUpLoadPath + newFileName, fullUpLoadPath + newThumbnailFileName,
                        this.uploadConfig.thumbnailwidth, this.uploadConfig.thumbnailheight, "Cut");
                }
                else
                {
                    newThumbnailPath = newFilePath; //不生成缩略图则返回原图
                }
                //如果是图片，检查是否需要打水印
                if (IsWaterMark(fileExt) && isWater)
                {
                    switch (this.uploadConfig.watermarktype)
                    {
                        case 1:
                            WaterMark.AddImageSignText(newFilePath, newFilePath,
                                this.uploadConfig.watermarktext, this.uploadConfig.watermarkposition,
                                this.uploadConfig.watermarkimgquality, this.uploadConfig.watermarkfont, this.uploadConfig.watermarkfontsize);
                            break;
                        case 2:
                            WaterMark.AddImageSignPic(newFilePath, newFilePath,
                                this.uploadConfig.watermarkpic, this.uploadConfig.watermarkposition,
                                this.uploadConfig.watermarkimgquality, this.uploadConfig.watermarktransparency);
                            break;
                    }
                }
                //处理完毕，返回JOSN格式的文件信息
                return "{\"status\": 1, \"msg\": \"上传文件成功！\", \"name\": \""
                    + fileName + "\", \"path\": \"" + newFilePath + "\", \"thumb\": \""
                    + newThumbnailPath + "\", \"size\": " + fileSize + ", \"ext\": \"" + fileExt + "\"}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return "{\"status\": 0, \"msg\": \"上传过程中发生意外错误！\"}";
            }
        }

        /// <summary>
        /// 保存远程文件到本地
        /// </summary>
        /// <param name="fileUri">URI地址</param>
        /// <returns>上传后的路径</returns>
        public string remoteSaveAs(string fileUri)
        {
            WebClient client = new WebClient();
            string fileExt = string.Empty; //文件扩展名，不含“.”
            if (fileUri.LastIndexOf(".") == -1)
            {
                fileExt = "gif";
            }
            else
            {
                fileExt = Utils.GetFileExt(fileUri);
            }
            string newFileName = Utils.GetRamCode() + "." + fileExt; //随机生成新的文件名
            string upLoadPath = GetUpLoadPath(); //上传目录相对路径
            string fullUpLoadPath = Utils.GetMapPath(upLoadPath); //上传目录的物理路径
            string newFilePath = upLoadPath + newFileName; //上传后的路径
            //检查上传的物理路径是否存在，不存在则创建
            if (!Directory.Exists(fullUpLoadPath))
            {
                Directory.CreateDirectory(fullUpLoadPath);
            }

            try
            {
                client.DownloadFile(fileUri, fullUpLoadPath + newFileName);
                //如果是图片，检查是否需要打水印
                if (IsWaterMark(fileExt))
                {
                    switch (this.uploadConfig.watermarktype)
                    {
                        case 1:
                            WaterMark.AddImageSignText(newFilePath, newFilePath,
                                this.uploadConfig.watermarktext, this.uploadConfig.watermarkposition,
                                this.uploadConfig.watermarkimgquality, this.uploadConfig.watermarkfont, this.uploadConfig.watermarkfontsize);
                            break;
                        case 2:
                            WaterMark.AddImageSignPic(newFilePath, newFilePath,
                                this.uploadConfig.watermarkpic, this.uploadConfig.watermarkposition,
                                this.uploadConfig.watermarkimgquality, this.uploadConfig.watermarktransparency);
                            break;
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
            client.Dispose();
            return newFilePath;
        }

        #region 私有方法
        /// <summary>
        /// 返回上传目录相对路径
        /// </summary>
        /// <param name="fileName">上传文件名</param>
        private string GetUpLoadPath()
        {
            string path = uploadConfig.webpath + uploadConfig.filepath + "/"; //站点目录+上传目录
            switch (this.uploadConfig.filesave)
            {
                case 1: //按年月日每天一个文件夹
                    path += DateTime.Now.ToString("yyyyMMdd");
                    break;
                default: //按年月/日存入不同的文件夹
                    path += DateTime.Now.ToString("yyyyMM") + "/" + DateTime.Now.ToString("dd");
                    break;
            }
            return path + "/";
        }

        /// <summary>
        /// 是否需要打水印
        /// </summary>
        /// <param name="_fileExt">文件扩展名，不含“.”</param>
        private bool IsWaterMark(string _fileExt)
        {
            //判断是否开启水印
            if (this.uploadConfig.watermarktype > 0)
            {
                //判断是否可以打水印的图片类型
                ArrayList al = new ArrayList();
                al.Add("bmp");
                al.Add("jpeg");
                al.Add("jpg");
                al.Add("png");
                if (al.Contains(_fileExt.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 是否为图片文件
        /// </summary>
        /// <param name="_fileExt">文件扩展名，不含“.”</param>
        private bool IsImage(string _fileExt)
        {
            ArrayList al = new ArrayList();
            al.Add("bmp");
            al.Add("jpeg");
            al.Add("jpg");
            al.Add("gif");
            al.Add("png");
            if (al.Contains(_fileExt.ToLower()))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检查是否为合法的上传文件
        /// </summary>
        private bool CheckFileExt(string _fileExt)
        {
            //检查危险文件
            string[] excExt = { "asp", "aspx", "ashx", "asa", "asmx", "asax", "php", "jsp", "htm", "html" };
            for (int i = 0; i < excExt.Length; i++)
            {
                if (excExt[i].ToLower() == _fileExt.ToLower())
                {
                    return false;
                }
            }
            //检查合法文件
            string[] allowExt = (this.uploadConfig.fileextension + "," + this.uploadConfig.videoextension).Split(',');
            Logger.Error("this.uploadConfig.fileextension=" + this.uploadConfig.fileextension);
            Logger.Error("_fileExt=" + _fileExt);
            for (int i = 0; i < allowExt.Length; i++)
            {
                if (allowExt[i].ToLower() == _fileExt.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查文件大小是否合法
        /// </summary>
        /// <param name="_fileExt">文件扩展名，不含“.”</param>
        /// <param name="_fileSize">文件大小(B)</param>
        private bool CheckFileSize(string _fileExt, int _fileSize)
        {
            //将视频扩展名转换成ArrayList
            ArrayList lsVideoExt = new ArrayList(this.uploadConfig.videoextension.ToLower().Split(','));
            //判断是否为图片文件
            if (IsImage(_fileExt))
            {
                if (this.uploadConfig.imgsize > 0 && _fileSize > this.uploadConfig.imgsize * 1024)
                {
                    return false;
                }
            }
            else if (lsVideoExt.Contains(_fileExt.ToLower()))
            {
                if (this.uploadConfig.videosize > 0 && _fileSize > this.uploadConfig.videosize * 1024)
                {
                    return false;
                }
            }
            else
            {
                if (this.uploadConfig.attachsize > 0 && _fileSize > this.uploadConfig.attachsize * 1024)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

    }
}
