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
    public partial class MainForm : Form
    {
        //保存所有待上传的文件路径
        private List<string> files;
        private BPCS bpcs;
        public MainForm()
        {
            
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;//允许跨线程访问
            //Init
            files = new List<string>();
            bpcs = new BPCS();
            //读取用户信息
            if(bpcs.isValidate())
            {
                loginToolStripMenuItem.Enabled = false;
                loginToolStripMenuItem.Text = bpcs.GetUserName();
            }
            else
            {
                exitToolStripMenuItem.Enabled = false;
            }
        }
        private void AddFileToList(string file)
        {
            string fileName = System.IO.Path.GetFileName(file).Trim();
            listBox.Items.Add(fileName);
        }
        private void chooseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                //添加待上传文件
                files.Add(openFileDialog.FileName);
                AddFileToList(openFileDialog.FileName);
            }
        }
        private void loginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bpcs.Login();
            //读取用户信息
            if (bpcs.isValidate())
            {
                loginToolStripMenuItem.Enabled = false;
                exitToolStripMenuItem.Enabled = true;
                loginToolStripMenuItem.Text = bpcs.GetUserName();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loginToolStripMenuItem.Enabled = true;
            exitToolStripMenuItem.Enabled = false;
            loginToolStripMenuItem.Text = "login";
            bpcs.Logout();
        }

        private void uploadButton_Click(object sender, EventArgs e)
        {
            if (bpcs.isValidate())
            {
                if (files.Count == 0)
                    MessageBox.Show(this, "Please choose a file at least!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                {
                    Task task = new Task(Upload);
                    task.Start();
                }
            }
            else
            {
                MessageBox.Show(this, "Please login first!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private void Upload()
        {
            uploadButton.Enabled = false;
            for (int i = 0; i < files.Count;++i )
            {
                string file = files[i];
                progressBar.Value = 0;
                progressBar.Maximum = 100;
                statusLabel.Text = "computing……";
                bpcs.progressEvent = new BPCS.ProgressEventHander(Progress);
                //先尝试利用秒传
                //如果上传失败，很有可能是百度云上没有找到相同的文件
                //这个时候用普通方式上传
                bool ok = bpcs.RapidUpload(file);
                if(!ok)
                {    
                    //普通方式上传
                    statusLabel.Text = "0%";
                    ok = bpcs.Upload(file);
                }
                if (!ok)
                    MessageBox.Show(string.Format("{0} upload failed", System.IO.Path.GetFileName(file)));
                DeleteListItem(file);
            }
            files.Clear();
            uploadButton.Enabled = true;
        }
        private void Progress(long value ,long maxnum)
        {
            statusLabel.Text = ((int)(value*100.0 / maxnum)).ToString() + "%";
            //防止溢出(progressBar int)
            if(maxnum>int.MaxValue)
            {
                value /= 10;
                maxnum /= 10;
            }
            progressBar.Maximum = (int)maxnum;
            progressBar.Value = (int)value;
        }
        private void DeleteListItem(string file)
        {
            for(int i=0;i<listBox.Items.Count;++i)
            {
                if(listBox.Items[i].ToString()
                    ==System.IO.Path.GetFileName(file))
                {
                    listBox.Items.RemoveAt(i);
                    break;
                }
            }
        }
        /// <summary>
        /// 鼠标拖曳进入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uploadPage_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }  
        }
        /// <summary>
        /// 鼠标拖曳离开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uploadPage_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                String[] dragedFiles = e.Data.GetData(DataFormats.FileDrop, false) as String[];
                foreach (string file in dragedFiles)
                {
                    if (System.IO.File.Exists(file))
                    {
                        files.Add(file);
                        AddFileToList(file);
                    }
                }
            }
            catch
            {

            }
        }
    }
}
