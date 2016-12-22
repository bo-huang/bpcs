﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BPCSASSIST
{
    class BPCS
    {
        private string access_token="";
        private const string filePath = "access_token.txt";
        private const string host = "https://pcs.baidu.com/rest/2.0/";

        //委托(用于传递进度条信息到MainForm)
        public delegate void ProgressEventHander(long value,long maxnum);
        public ProgressEventHander progressEvent = null;
        private long fileLength;
        private long uploadSize;//已上传的字节

        public BPCS()
        {
            if (File.Exists(filePath))
                access_token = File.ReadAllText(filePath, Encoding.UTF8);
            //默认此处为2？？（晕啊，还真是这个问题！！！）
            System.Net.ServicePointManager.DefaultConnectionLimit = 100;
        }
        /// <summary>
        /// 保存access_token到本地文件
        /// </summary>
        /// <param name="access_token"></param>
        public void Save(string access_token)
        {
            try
            {
                File.WriteAllText(filePath, access_token, Encoding.UTF8);
            }
            catch
            {
            }
        }
        /// <summary>
        /// 获取用户名
        /// </summary>
        /// <returns></returns>
        public string GetUserName()
        {
            string uri = string.Format("https://openapi.baidu.com/rest/2.0/passport/users/getInfo?access_token={0}",access_token);
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.KeepAlive = false;
                request.Method = "GET"; 
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using(Stream readStream = response.GetResponseStream())
                {
                    StreamReader myStreamReader = new StreamReader(readStream, Encoding.UTF8);
                    string retString = myStreamReader.ReadToEnd();
                    myStreamReader.Close();
                    response.Close();
                    //使用了第三方库Newtonsoft解析json
                    JObject jo = JObject.Parse(retString);
                    return  System.Web.HttpUtility.UrlDecode(jo["username"].ToString());
                }
                
            }
            catch
            {
                return "";
            }
        }
        /// <summary>
        /// 分片上传文件
        /// 最开始写的并行上传所有的分段，结果老出错
        /// 出错原因是：我明明写了Task.WaitAll(tasks);
        /// 但所有分段没上传完，这个函数却执行完毕
        /// 导致没有得到md5就返回了，因此合并就出错了
        /// 现在还不知道是什么原因，先不并行了
        /// 
        /// 知道是什么原因了……
        /// 默认http连接最大数量为2，如果前面有http连接没释放的话
        /// 再调用request.GetResponse()函数就会超时！！！
        /// 解决方法见构造函数
        /// 
        /// 另外，每次使用完requestStream或responseStream用完都要及时close
        /// 
        /// 按照上面两点做了，但发现如果超过了10个连接，还是会超时……
        /// 于是网上又有人建议，设置request.KeepAlive = false
        /// 
        /// 结果还是没有解决，呵呵（感觉和.Net的垃圾回收机制有关）
        /// 暴力的把DefaultConnectionLimit改为100
        /// </summary>
        /// <param name="file"></param>
        public bool Upload(string file) 
        {
            //上传前先验证acce_token
            if (!isValidate())
                return false;
            if (File.Exists(file))
            {
                uploadSize = 0;
                //获取文件大小
                FileInfo fileInfo = new FileInfo(file);
                fileLength = fileInfo.Length;
                long segmentLength;//分段大小
                if (fileLength > (1L << 28))//大于256MB（api限制是不超过2GB）
                    segmentLength = 1L << 28;
                else
                    segmentLength = (fileLength+1) >> 1;//至少需要2段
                int segmentCount = (int)((fileLength+segmentLength-1) / segmentLength);
                //分段读文件并上传  
                byte[] segmentData = new byte[segmentLength];
                List<string> md5s = new List<string>();
                bool ok = true;//所有段是否都上传成功
                /*Task<string>[] tasks = new Task<string>[segmentCount];//每一段用一个线程
                int segmentID = 0;//当前位于第几段*/
                using (FileStream fileStream = File.OpenRead(file))
                {
                    while (fileStream.Position<fileLength)
                    {
                        //返回读取的字节数 count
                        int count = fileStream.Read(segmentData, 0, (int)segmentLength);
                        /*//新建一个上传任务
                        tasks[segmentID] = Task<string>.Factory.StartNew(() =>
                            {
                                return SegmentUpload(segmentData, count);
                            });
                        ++segmentID;*/
                        string md5 = SegmentUpload(segmentData, count);
                        if(md5=="")
                        {
                            ok = false;
                            break;
                        }
                        md5s.Add(md5);
                    }
                    /*//等待所有上传任务完成
                    Task.WaitAll(tasks);
                    foreach (Task<string> task in tasks)
                    {
                        if(task.Result=="")//该段是否上传成功
                        {
                            ok = false;
                            break;
                        }
                        md5s.Add(task.Result);
                    }*/
                }
                //合并所有段
                if (ok)
                {
                    string fileName = System.IO.Path.GetFileName(file).Trim();
                    bool res = Merge(md5s, fileName);
                    return res;
                }
            }
            return false;
        }
        /// <summary>
        /// 合并上传的分片
        /// 至少需要两个分片，这也是为什么上面分段时要至少分2段的原因
        /// </summary>
        /// <param name="md5s"></param>
        /// <param name="path"></param>
        private bool Merge(List<string> md5s,string path)
        {
            string uri = host+"/file?method=createsuperfile&ondup=newcopy&path="
                + System.Web.HttpUtility.UrlEncode("/apps/UniDrive/"+path, Encoding.UTF8)
                + "&access_token=" + access_token;
            JObject jo = new JObject();
            JArray ja = new JArray();
            foreach (string md5 in md5s)
                ja.Add(md5);
            jo.Add("block_list", ja);
            byte[] postData = Encoding.UTF8.GetBytes("param="+jo.ToString());
            HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
            request.Method = "POST";
            request.KeepAlive = false;
            //必须加上这个ContentType
            request.ContentType = "application/x-www-form-urlencoded";
            try
            {
                using (Stream writeStream = request.GetRequestStream())
                {
                    //写文件
                    writeStream.Write(postData, 0, postData.Length);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 上传分片文件并返回该分片的md5
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private string SegmentUpload(byte[] segmentData,int segmentLength)
        {
            //开始长传
            try
            {
                string uri = host+"/pcs/file?method=upload&access_token=" + access_token + "&type=tmpfile";
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "PUT";//POST老是失败，提示content_type is not exists
                request.Host = "pcs.baidu.com";
                request.ContentLength = segmentLength;
                request.KeepAlive = false;
                using(Stream writeStream = request.GetRequestStream())
                {
                    //写文件（这里没有按照官方文档上写的file=）
                    //每次上传4MB，从而获得上传进度
                    int unitSize = 1 << 22;
                    int offset = 0;
                    while (offset < segmentLength)
                    {
                        int actualSize = unitSize;//实际上传的大小
                        //到了最后一个单元，且该单元size<unitSize
                        if (offset + unitSize > segmentLength)
                            actualSize = segmentLength - offset;
                        writeStream.Write(segmentData, offset, actualSize);
                        writeStream.Flush();
                        offset += unitSize;
                        uploadSize+=actualSize;
                        if (progressEvent != null)
                            progressEvent(uploadSize, fileLength);
                    }
                }
                //返回
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream readStream = response.GetResponseStream();
                StreamReader myStreamReader = new StreamReader(readStream, Encoding.UTF8);
                string retString = myStreamReader.ReadToEnd();
                myStreamReader.Close();
                response.Close();
                //使用了第三方库Newtonsoft解析json
                JObject jo = JObject.Parse(retString);
                return jo["md5"].ToString();
                
                
            }
            catch
            {
                return "";
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Login()
        {
            access_token = "";
            LoginForm login = new LoginForm();
            login.ShowDialog();
            if (login.IsLogin)
            {
                access_token = login.Access_Token;
                Save(access_token);
            }
        }
        /// <summary>
        /// 退出账户，删除access_token
        /// </summary>
        public void Logout()
        {
            access_token = "";
            File.Delete(filePath);
        }
        /// <summary>
        /// 判断access_token是否合法或已过期
        /// 只想到了“通过已经得到的access_token发送请求，看是否抛出异常”这种方式
        /// </summary>
        /// <returns></returns>
        public bool isValidate()
        {
            string uri = host+"/pcs/quota?method=info&access_token="+access_token;
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "GET";
                request.KeepAlive = false;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                }
            }
            catch 
            {
                return false;
            }
            return true;
        }
    }
}
