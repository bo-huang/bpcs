using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BPCSASSIST
{
    public partial class LoginForm : Form
    {
        public string Access_Token { get; set; }
        public bool IsLogin { get; set; }
        public LoginForm()
        {
          
            InitializeComponent();
            IsLogin = false;
            //使用时，请将client_id换成自己的
            webBrowser.Url = new Uri("http://openapi.baidu.com/oauth/2.0/authorize?client_id=aeq9kqvCGEhR5ADuXu18jLcf&response_type=token&redirect_uri=oob&confirm_login=1&scope=netdisk");
        }
        /// <summary>
        /// 发生导航时，触发改事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void webBrowser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            string url = e.Url.ToString();
            int start = url.IndexOf("access_token");
            //找到获取access_token的跳转链接
            //两次会触发该事件：1.刚进入 2.确认授权
            if (start != -1)
            {
                //获取access_token（有效期30天）
                start+=13;//跳过"access_token="
                //找到access_token的结束位置
                int end;
                for (end = start; end < url.Length; ++end)
                    if (url[end] == '&')
                        break;
                Access_Token = url.Substring(start, end - start);
                IsLogin = true;
                this.Hide();
            }
        }
    }
}
