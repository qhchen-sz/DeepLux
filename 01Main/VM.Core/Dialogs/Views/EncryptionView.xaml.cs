using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HV.Common;
using HV.PersistentData;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Dialogs.ViewModels;
using SLM_HANDLE_INDEX = System.UInt32;
using HV.Common.Helper.Encrypt;
using System.Runtime.InteropServices;
using System.Timers;

namespace HV.Dialogs.Views
{
    /// <summary>
    /// EncryptionView.xaml 的交互逻辑
    /// </summary>
    public partial class EncryptionView : Window
    {
        #region Singleton
        private static readonly EncryptionView _instance = new EncryptionView();
        public const int DEVELOPER_ID_LENGTH = 8;
        public const int DEVICE_SN_LENGTH = 16;
        //private static callback pfn;
        private static Timer timer;
        private static SLM_HANDLE_INDEX Handle;

        public static void StartTimer()
        {
            // 实例化计时器
            timer = new Timer(10000); // 设置计时器间隔为1000毫秒（1秒）

            // 订阅Elapsed事件
            timer.Elapsed += OnTimedEvent;

            // 启动计时器
            timer.AutoReset = true; // 设置为true使计时器持续触发
            timer.Enabled = true;

            // 设置回调
            //pfn = new callback(handle_service_msg);
        }
        public static void StopTimer()
        {
            // 停止计时器
            timer.Enabled = false;
            timer.Dispose();
        }
        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            if(AboutViewModel.Ins.ActiveState== eActiveState.Softdog)
            {
                // 调用回调函数
                uint result = SlmRuntime.slm_keep_alive(Handle);
                //uint result = pfn(message, wparam, lparam);
                if (result != 0x00000000)
                {
                    StopTimer();
                    MessageView.Ins.MessageBoxShow("加密狗未找到！！软件即将退出", eMsgType.Error);
                    CommonMethods.Exit();
                }
            }


            // 可以根据result做进一步处理
        }
        static byte[] HexStringToByteArray(string hex)
        {
            int len = hex.Length;
            byte[] data = new byte[len / 2];
            for (int i = 0; i < len; i += 2)
            {
                data[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return data;
        }
        private bool SSDLisence()
        {

            uint ret = 0;
            Handle = 0;

            string StrMsg = string.Empty;
            IntPtr a = IntPtr.Zero;

            // slm_get_developer_id
            byte[] developer_id = HexStringToByteArray("0800000000002679");

            //new byte[DEVELOPER_ID_LENGTH];

            ret = SlmRuntime.slm_get_developer_id(developer_id);
            if (ret != SSErrCode.SS_OK)
            {
                StrMsg = string.Format("slm_get_developer_id Failure:0x{0:X8}", ret);
                //WriteLineRed(StrMsg);
            }
            else
            {
                //WriteLineGreen("slm_get_developer_id Success!");

                // 将开发商ID转化为字符串
                string developerIDStr = BitConverter.ToString(developer_id).Replace("-", "");
                //WriteLineYellow(developerIDStr);
            }

            //01. init
            ST_INIT_PARAM initPram = new ST_INIT_PARAM();
            initPram.version = SSDefine.SLM_CALLBACK_VERSION02;
            initPram.flag = SSDefine.SLM_INIT_FLAG_NOTIFY;
            StartTimer();
            //initPram.pfn = pfn;

            // 指定开发者 API 密码，示例代码指定 Demo 开发者的 API 密码。
            // 注意：正式开发者运行测试前需要修改此值，可以从 Virbox 开发者网站获取 API 密码。
            //
            //B6AAC927A37FF9F6FA09B0138B463C9C
            //Hymson0123456789
            initPram.password = HexStringToByteArray("B6AAC927A37FF9F6FA09B0138B463C9C");
            //new byte[] { 0xDB, 0x3B, 0x83, 0x8B, 0x2E, 0x4F, 0x08, 0xF5, 0xC9, 0xEF, 0xCD, 0x1A, 0x5D, 0xD1, 0x63, 0x41 };

            ret = SlmRuntime.slm_init(ref initPram);
            if (ret == SSErrCode.SS_OK)
            {
                //WriteLineGreen("Slminit Success!");
            }
            else if (ret == SSErrCode.SS_ERROR_DEVELOPER_PASSWORD)
            {
                StrMsg = string.Format("Slminit Failure:0x{0:X8}(ERROR_DEVELOPER_PASSWORD). Please login to the Virbox Developer Center(https://developer.lm.virbox.com), get the API password, and replace the 'initPram.password' variable content.", ret);
                //WriteLineRed(StrMsg);

            }
            else
            {
                StrMsg = string.Format("Slm_Init Failure:0x{0:X8}", ret);
                //WriteLineRed(StrMsg);
            }

            //02. find License
            IntPtr desc = IntPtr.Zero;


            //03. LOGIN
            ST_LOGIN_PARAM stLogin = new ST_LOGIN_PARAM();
            stLogin.size = (UInt32)Marshal.SizeOf(stLogin);

            // 指定登录的许可ID，Demo 设置登录0号许可，开发者正式使用时可根据需要调整此参数。
            stLogin.license_id = 1;

            // 指定登录许可ID的容器，Demo 设置为本地加密锁，开发者可根据需要调整此参数。
            stLogin.login_mode = SSDefine.SLM_LOGIN_MODE_LOCAL;

            ret = SlmRuntime.slm_login(ref stLogin, INFO_FORMAT_TYPE.STRUCT, ref Handle, a);
            if (ret != SSErrCode.SS_OK)
            {
                StrMsg = string.Format("Slm_Login Failure:0x{0:X8}", ret);
                //WriteLineRed(StrMsg);
                return false;
            }
            else
            {
                //MessageBox.Show("登录成功");
                return true;
                //WriteLineGreen("Slmlogin Success!");
                //Start();
            }

            //04. KEEP ALIVE
            //ret = SlmRuntime.slm_keep_alive(Handle);
            //if (ret != SSErrCode.SS_OK)
            //{
            //    StrMsg = string.Format("SlmKeepAliveEasy Failure:0x{0:X8}", ret);
            //    WriteLineRed(StrMsg);
            //    System.Diagnostics.Debug.Assert(true);
            //}
            //else
            //{
            //    WriteLineGreen("SlmKeepAliveEasy Success!");
            //}

            //05. get_info
            //lock_info
            //ret = SlmRuntime.slm_get_info(Handle, INFO_TYPE.LOCK_INFO, INFO_FORMAT_TYPE.JSON, ref desc);
            //if (ret != SSErrCode.SS_OK)
            //{
            //    StrMsg = string.Format("slm_get_info(local_info) Failure:0x{0:X8}", ret);
            //    WriteLineRed(StrMsg);
            //}
            //else
            //{
            //    string StrPrint = Marshal.PtrToStringAnsi(desc);
            //    WriteLineYellow(StrPrint);
            //    WriteLineGreen("slm_get_info(local_info) Success!");
            //    if (ret != SSErrCode.SS_OK)
            //    {
            //        StrMsg = string.Format("slm_free Failure:0x{0:X8}", ret);
            //        WriteLineRed(StrMsg);
            //    }
            //}
            //session_info
            ret = SlmRuntime.slm_get_info(Handle, INFO_TYPE.SESSION_INFO, INFO_FORMAT_TYPE.JSON, ref desc);
            if (ret != SSErrCode.SS_OK)
            {
                StrMsg = string.Format("slm_get_info(session_info) Failure:0x{0:X8}", ret);
                //WriteLineRed(StrMsg);
            }
            else
            {
                string StrPrint = Marshal.PtrToStringAnsi(desc);
                //WriteLineYellow(StrPrint);
                //WriteLineGreen("slm_get_info(session_info) Success!");
                if (ret != SSErrCode.SS_OK)
                {
                    StrMsg = string.Format("slm_free Failure:0x{0:X8}", ret);
                    //WriteLineRed(StrMsg);
                }
            }
            return false;
        }
        private EncryptionView()
        {
            InitializeComponent();
            LoadIcon();
            MachineCode = getCpu() + GetDiskVolumeSerialNumber();//获得24位Cpu和硬盘序列号
            tbMachineCode.Text = MachineCode + SystemConfig.Ins.ProbationNum.ToString("X2");
            License = MD5Provider.GetMD5String(MachineCode);
            ProbationLicense = DesEncrypt.Encrypt(MachineCode + SystemConfig.Ins.ProbationNum.ToString("X2"));
        }
        public static EncryptionView Ins
        {
            get { return _instance; }
        }

        #endregion

        #region Prop

        public string MachineCode;
        public static string License;
        public string ProbationLicense;
        private static int ProbationTimeLimit = 720;//一个月30*24=720
        public static bool RegisterSuccess = false;

        #endregion
        private void LoadIcon()
        {
            ImageBrush imageBrush = new ImageBrush();
            if (!File.Exists(SystemConfig.Ins.SoftwareIcoPath))
            {
                SystemConfig.Ins.SoftwareIcoPath = FilePaths.DefultSoftwareIcon;
            }
            imageBrush.ImageSource = new BitmapImage(new Uri(SystemConfig.Ins.SoftwareIcoPath, UriKind.Relative));
            //bdImage.Background = imageBrush;
        }

        /// <summary>
        /// 试用期计时
        /// </summary>
        public void ProbationTime()
        {
            if (!(License == SystemConfig.Ins.License))
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            if(AboutViewModel.Ins.ActiveState!= eActiveState.Softdog)
                            {
                                //判断是否达到720h 即一个月30*24=720
                                if (SystemConfig.Ins.ProbationTime > ProbationTimeLimit)
                                {
                                    SystemConfig.Ins.ProbationNum++;
                                    SystemConfig.Ins.SaveSystemConfig();
                                    MessageView.Ins.MessageBoxShow("软件已过试用期，请联系开发者！", eMsgType.Warn, MessageBoxButton.OKCancel);
                                    CommonMethods.Exit();
                                }
                                else
                                {
                                    SystemConfig.Ins.ProbationTime++;
                                    SystemConfig.Ins.SaveSystemConfig();
                                }
                            }

                            //延时1h
                            await Task.Delay(3600000);
                        }
                        catch (Exception ex)
                        {
                            Logger.GetExceptionMsg(ex);
                        }

                    }
                });
            }
        }
        public bool ConfirmLicense()
        {
            try
            {
                if (License == SystemConfig.Ins.License)
                {
                    AboutViewModel.Ins.ActiveState = eActiveState.Actived;
                    return true;
                }
                else if (ProbationLicense == AnalysisProbationLicense(SystemConfig.Ins.ProbationLicense))
                {
                    AboutViewModel.Ins.ActiveState = eActiveState.Probation;
                    return true;
                }
                else if (SSDLisence())
                {
                    AboutViewModel.Ins.ActiveState = eActiveState.Softdog;
                    return true;
                }
                else
                {
                    AboutViewModel.Ins.ActiveState = eActiveState.NotActived;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }
        private string AnalysisProbationLicense(string probationLicense)
        {
            string str = "";
            try
            {
                if (probationLicense == null)
                {
                    str = probationLicense;
                    return str;
                }
                str = DesEncrypt.Decrypt(probationLicense);
                //ProbationTimeLimit = int.Parse(str);
                ProbationTimeLimit = int.Parse(str.Substring(str.Length - 2, 2), NumberStyles.HexNumber);
                str = str.Substring(0, str.Length - 2);
                str = DesEncrypt.Encrypt(str+ SystemConfig.Ins.ProbationNum.ToString("X2"));
                return str;
            }
            catch (Exception)
            {
                return str;
            }
        }
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        /// <summary>
        /// 获取CPU的参数
        /// </summary>
        /// <returns></returns>
        public string getCpu()
        {
            string strCpu = null;
            ManagementClass myCpu = new ManagementClass("win32_Processor");
            ManagementObjectCollection myCpuConnection = myCpu.GetInstances();
            foreach (ManagementObject myObject in myCpuConnection)
            {
                strCpu = myObject.Properties["Processorid"].Value.ToString();
                break;
            }
            return strCpu.Substring(strCpu.Length-8, 8);
        }
        /// <summary>
        /// 获取硬盘的参数
        /// </summary>
        /// <returns></returns>
        public string GetDiskVolumeSerialNumber()
        {
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObject disk = new ManagementObject("win32_logicaldisk.deviceid=\"c:\"");
            disk.Get();
            return disk.GetPropertyValue("VolumeSerialNumber").ToString();
        }
        #region 改善用户体验


        #endregion

        #region 无边框窗体拖动
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // 获取鼠标相对标题栏位置 
            Point position = e.GetPosition(this);
            // 如果鼠标位置在标题栏内，允许拖动 
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (position.X >= 0 && position.X < this.ActualWidth && position.Y >= 0 && position.Y < this.ActualHeight)
                {
                    this.DragMove();
                }
            }
        }

        #endregion

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (tbRegistrationCode.Text != "")
                {
                    if (tbRegistrationCode.Text == License)
                    {
                        MessageView.Ins.MessageBoxShow("注册成功！");
                        SystemConfig.Ins.License = tbRegistrationCode.Text;
                        SystemConfig.Ins.SaveSystemConfig();
                        AboutViewModel.Ins.ActiveState = eActiveState.Actived;
                        RegisterSuccess = true;
                        this.DialogResult = true;
                    }
                    else if (AnalysisProbationLicense(tbRegistrationCode.Text) == ProbationLicense)
                    {
                        MessageView.Ins.MessageBoxShow("试用码注册成功！");
                        SystemConfig.Ins.ProbationLicense = tbRegistrationCode.Text;
                        SystemConfig.Ins.ProbationTime = 0;
                        SystemConfig.Ins.SaveSystemConfig();
                        AboutViewModel.Ins.ActiveState = eActiveState.Probation;
                        RegisterSuccess = true;
                        this.DialogResult = true;
                    }
                    else
                    {
                        MessageView.Ins.MessageBoxShow("注册失败！", eMsgType.Warn);
                    }
                }
                else
                {
                    MessageView.Ins.MessageBoxShow("注册码为空！", eMsgType.Warn);
                }
            }
            catch (Exception ex)
            {
                MessageView.Ins.MessageBoxShow("注册码错误，注册失败！", eMsgType.Warn);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
            base.OnClosing(e);
        }

        private void window_Activated(object sender, EventArgs e)
        {
            tbRegistrationCode.Focus();
        }
    }
}
