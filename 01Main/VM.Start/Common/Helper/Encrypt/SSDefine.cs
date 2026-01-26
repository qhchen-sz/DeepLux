using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SLM_HANDLE_INDEX = System.UInt32;

namespace HV.Common.Helper.Encrypt
{
    public delegate uint callback(uint message, UIntPtr wparam, UIntPtr lparam);

    //init struct
    public struct ST_INIT_PARAM
    {
        /** 版本－用来兼容，当前使用 SLM_CALLBACK_VERSION02 */
        public UInt32 version;
        /** 如果需要接收SenseShield服务通知，填 SLM_INIT_FLAG_NOTIFY */
        public UInt32 flag;
        /** 回调函数指针*/
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public callback pfn;

        /** 通信连接超时时间（毫秒），如果填0，则使用默认超时时间（7秒）*/
        public UInt32 timeout;
        /** API密码，可从深思云开发者中心（https://developer.senseyun.com），通过“查看开发商信息”获取*/
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_DEV_PASSWORD_LENGTH)]
        public byte[] password;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_LOGIN_PARAM
    {
        /** 结构体大小（必填）*/
        public UInt32 size;
        /** 要登录的许可ID*/
        public UInt32 license_id;
        /** 许可会话的超时时间（单位：秒）,填0则使用默认值：600秒   */
        public UInt32 timeout;
        /** 许可登录的模式：本地，远程，云（见LOGIN_MODE_XXX)，如果填0，则使用SLM_LOGIN_MODE_AUTO*/
        public UInt32 login_mode;
        /** 许可登录的标志：见SLM_LOGIN_FLAG_XXX */
        public UInt32 login_flag;
        /** 许可登录指定的锁唯一序列号（可选）*/
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_LOCK_SN_LENGTH)]
        public byte[] sn;
        /** 网络锁服务器地址（可选），仅识别IP地址 */
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_MAX_SERVER_NAME)]
        public char[] server;
        /** 云锁用户token（可选）*/
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_MAX_ACCESS_TOKEN_LENGTH)]
        public char[] access_token;
        /** 云锁服务器地址（可选）*/
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_MAX_CLOUD_SERVER_LENGTH)]
        public char[] cloud_server;
        /** 碎片代码种子（可选），如果要支持碎片代码,login_flag需要指定为SLM_LOGIN_FLAG_SNIPPET*/
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_SNIPPET_SEED_LENGTH)]
        public byte[] snippet_seed;
        /** 已登录用户的guid（可选） */
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)SSDefine.SLM_CLOUD_MAX_USER_GUID_SIZE)]
        public byte[] user_guid;
    }
    /** 设备证书类型*/
    public enum CERT_TYPE : uint
    {
        /** 证书类型：根证书  */
        CERT_TYPE_ROOT_CA = 0,

        /** 证书类型：设备子CA  */
        CERT_TYPE_DEVICE_CA = 1,

        /** 证书类型：设备证书  */
        CERT_TYPE_DEVICE_CERT = 2,

        /** 证书类型：深思设备证书  */
        CERT_TYPE_SENSE_DEVICE_CERT = 3,
    }
    public enum INFO_FORMAT_TYPE : uint
    {
        JSON = 2,         //JSON格式
        STRUCT = 3,       //结构体格式
        STRING_KV = 4,    //字符串模式,遵行Key=value
        CIPHER = 5,       //加密二进制格式
    }

    /// <summary>
    /// 
    /// </summary>
    public enum LIC_USER_DATA_TYPE : uint
    {
        ROM = 0,       //只读区 
        RAW = 1,       //读写区
        PUB = 2,       //公开区
    }

    /// <summary>
    /// 
    /// </summary>
    public enum INFO_TYPE
    {
        LOCK_INFO = 1,       //锁信息
        SESSION_INFO = 2,    //会话信息
        LICENSE_INFO = 3,    //许可信息
        FILE_LIST = 4,       //文件列表
    }

    /// <summary>
    /// LED灯控制参数
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ST_LED_CONTROL
    {
        public UInt32 index;               //0表示蓝色LED，1表示红色LED
        public UInt32 state;               //0代表关闭，1代表打开，2代表闪烁
        public UInt32 interval;            //当state为2时，表示闪烁间隔（毫秒）；state为其他值时，该字段无效。
    }
    public class SSErrCode
    {
        //============================================================
        //              一般错误码
        //============================================================
        public const UInt32 SS_OK = 0x00000000;                            //  成功
        public const UInt32 SS_ERROR = 0x00000001;                         //  错误，原因未知 */ // TODO(zhaock) : 应该去
        public const UInt32 SS_ERROR_INVALID_PARAM = 0x00000002;           //  不合法的参数
        public const UInt32 SS_ERROR_MEMORY_FAIELD = 0x00000003;           //  内存错误
        public const UInt32 SS_ERROR_INSUFFICIENT_BUFFER = 0x00000004;     //  缓冲区大小不足
        public const UInt32 SS_ERROR_NOT_FOUND = 0x00000005;               //  没找到目标
        public const UInt32 SS_ERROR_EXISTED = 0x00000006;                 //  目标已存在
        public const UInt32 SS_ERROR_DATA_BROKEN = 0x00000007;             //  数据损坏
        public const UInt32 SS_ERROR_INVALID_HANDLE = 0x00000008;          //  无效的句柄
        public const UInt32 SS_ERROR_TIMEOUT = 0x00000009;                 //  操作超时
        public const UInt32 SS_ERROR_TARGET_NOT_IN_USE = 0x0000000A;       //  目标未在使用状态，如目标模块未开启或已关闭
        public const UInt32 SS_ERROR_DATA_CONFLICT = 0X0000000B;           //  不相容的数据同时存在
        public const UInt32 SS_ERROR_INVALID_TYPE = 0x0000000C;            //  无效类型
        public const UInt32 SS_ERROR_INVALID_LENGTH = 0x0000000D;          //  无效长度
        public const UInt32 SS_ERROR_USER_MOD_CRASH = 0x0000000E;          //  用户模块冲突
        public const UInt32 SS_ERROR_SERVER_IS_LOCAL = 0x0000000F;         //  查找的SS是本地
        public const UInt32 SS_ERROR_UNSUPPORT = 0x00000010;               //  不支持的操作
        public const UInt32 SS_ERROR_PORT_IN_USE = 0x00000011;             //  端口占用
        public const UInt32 SS_ERROR_NO_KEY = 0x00000013;                  //  没有密钥
        public const UInt32 SS_ERROR_SERVICE_TYPE_NOT_SUPPORT = 0x00000014;//  服务类型不支持操作
        public const UInt32 SS_ERROR_MULTICAST_ADDR_IN_USE = 0x00000015;   //  多播地址占用
        public const UInt32 SS_ERROR_MULTICAST_PORT_IN_USE = 0x00000016;   //  多播端口占用
        public const UInt32 SS_ERROR_MOD_FAIL_LIBSTRING = 0x00000020;      //  libstring错误
        public const UInt32 SS_ERROR_NET_ERROR = 0x00000040;               //  网络错误
        public const UInt32 SS_ERROR_IPC_ERROR = 0x00000041;               //  IPC 错误
        public const UInt32 SS_ERROR_INVALID_SESSION = 0x00000042;         //  会话失效

        //============================================================
        //          LM 模块(0x20): (runtime, control, develop)
        //============================================================
        public const UInt32 SS_ERROR_D2C_NO_PACKAGE = 0x13000000;                   //  D2C包中无签发内容
        public const UInt32 SS_ERROR_DEVELOPER_CERT_ALREADY_EXIST = 0x13000001;     //  开发商证书已存在
        public const UInt32 SS_ERROR_PARSE_CERT = 0x13000003;                       //  解析证书错误
        public const UInt32 SS_ERROR_D2C_PACKAGE_TOO_LARGE = 0x13000004;            //  D2C包过大
        public const UInt32 SS_ERROR_RESPONSE = 0x13000005;                         //  错误的数据响应
        public const UInt32 SS_ERROR_SEND_LM_REMOTE_REQUEST = 0x13000006;           //  发送LM远程请求失败
        public const UInt32 SS_ERROR_RUNTIME_NOT_INITIALIZE = 0x13000007;           //  未调用Runtime初始化函数
        public const UInt32 SS_ERROR_BAD_CONNECT = 0x13000008;                      //  获取连接失败
        public const UInt32 SS_ERROR_RUNTIME_VERSION = 0x13000009;                  //  版本不匹配
        public const UInt32 SS_ERROR_LIC_NOT_FOUND = 0x13000020;                    //  许可未找到
        public const UInt32 SS_ERROR_AUTH_ACCEPT_FAILED = 0x13000021;               //  认证错误
        public const UInt32 SS_ERROR_AUTH_HANDLE_FAILED = 0x13000022;               //  认证失败
        public const UInt32 SS_ERROR_DECODE_BUFFER = 0x13000023;                    //  解密错误
        public const UInt32 SS_ERROR_USER_DATA_TOO_SMALL = 0x13000024;              //  用户数据区太小
        public const UInt32 SS_ERROR_INVALID_LM_REQUEST = 0x13000025;               //  无效的LM请求
        public const UInt32 SS_ERROR_INVALID_SHORTCODE = 0x13000026;                //  无效的短码
        public const UInt32 SS_ERROR_INVALID_D2C_PACKAGE = 0x13000027;              //  错误的D2C升级包
        public const UInt32 SS_ERROR_CLOUD_RESPONSE = 0x13000028;                   //  云锁返回的数据错误
        public const UInt32 SS_ERROR_USER_DATA_TOO_LARGE = 0x13000029;              //  读写的数据过大
        public const UInt32 SS_ERROR_INVALID_MEMORY_ID = 0x1300002A;                //  无效的内存ID
        public const UInt32 SS_ERROR_INVALID_MEMORY_OFFSET = 0x1300002B;            //  无效的内存偏移
        public const UInt32 SS_ERROR_INVALID_CLOUD_SERVER = 0x1300002C;             //  无效的云锁服务器
        public const UInt32 SS_ERROR_UNCALIBRATED_TIMESTAMP = 0x1300002D;           //  时间戳未校准
        public const UInt32 SS_ERROR_GENERATE_GUID = 0x1300002F;                    //  生成GUID错误
        public const UInt32 SS_ERROR_NO_LOGGED_USER = 0x13000030;                   //  没有登录的用户
        public const UInt32 SS_ERROR_USER_AUTH_SERVER_NOT_RUNNING = 0x13000031;     //  用户认证服务未启动
        public const UInt32 SS_ERROR_MODULE_NOT_EXIST = 0x13000032;                 //  模块不存在
        public const UInt32 SS_ERROR_UNSUPPORTED_SNIPPET_CODE = 0x13000033;         //  不支持的代码片
        public const UInt32 SS_ERROR_INVALID_SNIPPET_CODE = 0x13000034;             //  无效的代码
        public const UInt32 SS_ERROR_EXECUTE_SNIPPET_CODE = 0x13000035;             //  执行碎片代码失败
        public const UInt32 SS_ERROR_SNIPPET_EXECUTE_LOGIN = 0x13000036;            //  碎片执行登录失败
        public const UInt32 SS_ERROR_LICENSE_MODULE_NOT_EXISTS = 0x13000037;        //  许可模块不存在
        public const UInt32 SS_ERROR_DEVELOPER_PASSWORD = 0x13000038;  //  错误的开发商密码
        public const UInt32 SS_ERROR_CALLBACK_VERSION = 0x13000039;  //  错误的初始化回调版本号
        public const UInt32 SS_ERROR_INFO_RELOGIN = 0x1300003A;  //  用户需重新登录
        public const UInt32 SS_ERROR_LICENSE_VERIFY = 0x1300003B;  //  许可数据验签失败
        public const UInt32 SS_ERROR_REFRESH_TOKEN_TIMEOUT = 0x1300003C;  //  重新刷新token超时
        public const UInt32 SS_ERROR_TOKEN_VERIFY_FAIL = 0x1300003D;  //  token验证失败
        public const UInt32 SS_ERROR_GET_TOKEN_FAIL = 0x1300003E;  //  token获取失败
        public const UInt32 SS_ERROR_NEED_WAIT = 0x13000044;  //  内部错误
        public const UInt32 SS_ERROR_LICENSE_NEED_TO_ACTIVATE = 0x13000051;  //  软锁许可需要联网激活
        public const UInt32 SS_ERROR_DATA_NOT_END = 0x13000052;  //  内部错误，数据未传输完毕


        //============================================================
        //              IPC 模块 (0x02)
        //============================================================

        public const UInt32 SS_ERROR_BAD_ADDR = 0x02000000;  //  错误的地址
        public const UInt32 SS_ERROR_BAD_NAME = 0x02000001;  //  错误的名称
        public const UInt32 SS_ERROR_IPC_FAILED = 0x02000002;  //  IPC 收发错误
        public const UInt32 SS_ERROR_IPC_CONNECT_FAILED = 0x02000003;  //  连接失败
        public const UInt32 SS_ERROR_IPC_AUTH_INITIALIZE = 0x02000004;  //  Auth失败
        public const UInt32 SS_ERROR_IPC_QUERY_STATE = 0x02000005;  //  查询SS状态失败
        public const UInt32 SS_ERROR_SERVICE_NOT_RUNNING = 0x02000006;  //  SS未运行
        public const UInt32 SS_ERROR_IPC_DISCONNECT_FAILED = 0x02000007;  //  断开连接失败
        public const UInt32 SS_ERROR_IPC_BUILD_SESSION_KEY = 0x02000008;  //  会话密钥协商失败
        public const UInt32 SS_ERROR_REQUEST_OUTPUT_BUFFER_TOO_LARGE = 0x02000009;  //  请求的最大缓冲区过大
        public const UInt32 SS_ERROR_IPC_AUTH_ENCODE = 0x0200000A;  //  Auth encode错误
        public const UInt32 SS_ERROR_IPC_AUTH_DECODE = 0x0200000B;  //  Auth decode错误

        //============================================================
        //              Net Agent 模块 (0x11)
        //============================================================




        //============================================================
        //              安全模块 (0x12)
        //============================================================

        public const UInt32 SS_ERROR_INIT_ANTIDEBUG = 0x12000005;
        public const UInt32 SS_ERROR_DEBUG_FOUNDED = 0x12000006;



        //============================================================
        //              LM Service (0x24)
        //============================================================

        public const UInt32 ERROR_LM_SVC_UNINTIALIZED = 0x24000001;  //  未初始化 service中的表项
        public const UInt32 ERROR_LM_SVC_INITIALIZING = 0x24000002;  //  正在初始化service表
        public const UInt32 ERROR_LM_SVC_INVALID_SESSION_INFO_SIZE = 0x24000003;  //  传入session 大小不正确
        public const UInt32 ERROR_LM_SVC_KEEP_ALIVE_FAILED = 0x24000004;  //  未知的keep alive 操作失败原因 
        public const UInt32 ERROR_LM_SVC_LICENSE_NOT_FOUND = 0x24000005;  //  缓存中没有找到对应许可
        public const UInt32 ERROR_LM_SVC_SESSION_ALREADY_LOGOUT = 0x24000006;  //  session已经退出 
        public const UInt32 ERROR_LM_SVC_SESSION_ID_NOT_FOUND = 0x24000007;  //  不存在该session id
        public const UInt32 ERROR_LM_SVC_DEBUGGED = 0x24000008;  //  发现被调试
        public const UInt32 ERROR_LM_SVC_INVALID_DESCRIPTION = 0x24000009;  //  无效的许可描述信息
        public const UInt32 ERROR_LM_SVC_HANDLE_NOT_FOUND = 0x2400000A;  //  找不到指定句柄
        public const UInt32 ERROR_LM_SVC_CACHE_OVERFLOW = 0x2400000B;  //  cache 缓存已满
        public const UInt32 ERROR_LM_SVC_SESSION_OVERFLOW = 0x2400000C;  //  session 缓存已满
        public const UInt32 ERROR_LM_SVC_INVALID_SESSION = 0x2400000D;  //  无效的session
        public const UInt32 ERROR_LM_SVC_SESSION_ALREADY_DELETED = 0x2400000E;  //  session 已经被删除
        public const UInt32 ERROR_LM_SVC_LICENCE_EXPIRED = 0x2400000F;  //  许可已经过期
        public const UInt32 ERROR_LM_SVC_SESSION_TIME_OUT = 0x24000010;  //  session超时
        public const UInt32 ERROR_LM_SVC_NOT_ENOUGH_BUFF = 0x24000011;  //  缓冲区大小不足
        public const UInt32 ERROR_LM_SVC_DESC_NOT_FOUND = 0x24000012;  //  没找到该设备描述
        public const UInt32 ERROR_LM_INVALID_PARAMETER = 0x24000013;  //  LM service参数错误
        public const UInt32 ERROR_LM_INVALID_LOCK_TYPE = 0x24000014;  //	锁类型不支持
        public const UInt32 ERROR_LM_REMOTE_LOGIN_DENIED = 0x24000015;  //  许可不允许远程登录
        public const UInt32 ERROR_LM_SVC_SESSION_INVALID_AUTHCODE = 0x24000016;  //  session认证失败
        public const UInt32 ERROR_LM_SVC_ACCOUNT_NOT_BOUND = 0x24000017;  //  账户未绑定


        //============================================================
        //              LM Native (0x21)
        //============================================================

        public const UInt32 SS_ERROR_UNSUPPORTED_ALGORITHM = 0x21000000;  //  不支持的算法类型
        public const UInt32 SS_ERROR_INVAILD_HLC_HANDLE = 0x21000001;  //  无效的HLC句柄
        public const UInt32 SS_ERROR_HLC_CHECK = 0x21000002;  //  HLC检查失败
        public const UInt32 SS_ERROR_LM_CHECK_READ = 0x21000003;  //  读标志位检查失败
        public const UInt32 SS_ERROR_LM_CHECK_LICENSE = 0x21000004;  //  输出缓冲区许可ID不匹配
        public const UInt32 SS_ERROR_LM_CHECKSUM = 0x21000005;  //  输出缓冲区校验失败
        public const UInt32 SS_ERROR_HLC_BUFFER_LEN = 0x21000006;  //  HLC数据加密大于最大的缓冲区
        public const UInt32 SS_ERROR_L2CWF_LEN = 0x21000007;  //  无效的加密长度
        public const UInt32 SS_ERROR_INVAILD_MAX_ENCRYPT_LENGTH = 0x21000008;  //  无效的加密长度
        public const UInt32 SS_ERROR_INVAILD_ENUM_CRYPT_TYPE = 0x21000009;  //  不支持的加密类型
        public const UInt32 SS_ERROR_NATIVE_INSUFFICIENT_BUFFER = 0x2100000A;  //  缓冲区不足
        public const UInt32 SS_ERROR_NATIVE_LIST_FILE_FAILED = 0x2100000B;  //  枚举锁内文件错误
        public const UInt32 SS_ERROR_INVALID_C2H_REQUEST = 0x2100000C;  //  无效的云锁到硬件锁请求

        //============================================================
        //              LM Firmware (0x22)
        //============================================================

        public const UInt32 SS_ERROR_FIRM_INVALID_FILE_NAME = 0x22000001;  // 文件名称无效                                        
        public const UInt32 SS_ERROR_FIRM_CHECK_BUFF_FAILED = 0x22000002;  // 数据校验失败
        public const UInt32 SS_ERROR_FIRM_INVALID_BUFF_LEN = 0x22000003;  // 传入数据长度错误
        public const UInt32 SS_ERROR_FIRM_INVALID_PARAM = 0x22000004;  // 参数有误
        public const UInt32 SS_ERROR_FIRM_INVALID_SESSION_INFO = 0x22000005;  // session 信息错误
        public const UInt32 SS_ERROR_FIRM_INVALID_FILE_SIZE = 0x22000006;  // 创建文件长度出错
        public const UInt32 SS_ERROR_FIRM_WRITE_FILE_FAILED = 0x22000007;  // 写入文件数据出错
        public const UInt32 SS_ERROR_FIRM_INVALID_LICENCE_HEADER = 0x22000008;  // 许可信息头部错误
        public const UInt32 SS_ERROR_FIRM_INVALID_LICENCE_SIZE = 0x22000009;  // 许可信息数据错误  
        public const UInt32 SS_ERROR_FIRM_INVALID_LICENCE_INDEX = 0x2200000A;  // 超过支持最大许可序号                                     
        public const UInt32 SS_ERROR_FIRM_LIC_NOT_FOUND = 0x2200000B;  // 没有找到对应的许可
        public const UInt32 SS_ERROR_FIRM_MEM_STATUS_INVALID = 0x2200000C;  // 内存状态数据未初始化
        public const UInt32 SS_ERROR_FIRM_INVALID_LIC_ID = 0x2200000D;  // 不可用的许可号
        public const UInt32 SS_ERROR_FIRM_LICENCE_ALL_DISABLED = 0x2200000E;  // 所有许可被禁用
        public const UInt32 SS_ERROR_FIRM_CUR_LICENCE_DISABLED = 0x2200000F;  // 当前许可被禁用
        public const UInt32 SS_ERROR_FIRM_LICENCE_INVALID = 0x22000010;  // 当前许可不可用
        public const UInt32 SS_ERROR_FIRM_LIC_STILL_UNAVALIABLE = 0x22000011;  // 许可尚不可用
        public const UInt32 SS_ERROR_FIRM_LIC_TERMINATED = 0x22000012;  // 许可已经到期
        public const UInt32 SS_ERROR_FIRM_LIC_RUNTIME_TIME_OUT = 0x22000013;  // 运行时间用尽
        public const UInt32 SS_ERROR_FIRM_LIC_COUNTER_IS_ZERO = 0x22000014;  // 次数用尽
        public const UInt32 SS_ERROR_FIRM_LIC_MAX_CONNECTION = 0x22000015;  // 已达到最大并发授权
        public const UInt32 SS_ERROR_FIRM_INVALID_LOGIN_COUNTER = 0x22000016;  // 不正确的登录计数
        public const UInt32 SS_ERROR_FIRM_REACHED_MAX_SESSION = 0x22000017;  // 锁内已经到达最大会话数量
        public const UInt32 SS_ERROR_FIRM_INVALID_TIME_INFO = 0x22000018;  // 通讯时间信息出错
        public const UInt32 SS_ERROR_FIRM_SESSION_SIZE_DISMATCH = 0x22000019;  // session 信息大小不匹配
        public const UInt32 SS_ERROR_FIRM_NOT_ENOUGH_SHAREMEMORY = 0x2200001A;  // 没有足够的共享内存
        public const UInt32 SS_ERROR_FIRM_INVALID_OPCODE = 0x2200001B;  // 不可用的操作码
        public const UInt32 SS_ERROR_FIRM_INVALID_DATA_LEN = 0x2200001C;  // 错误的数据文件长度    
        public const UInt32 SS_ERROR_FIRM_DATA_FILE_NOT_FOUND = 0x2200001E;  // 找不到对应的许可数据文件
        public const UInt32 SS_ERROR_FIRM_INVALID_PKG_TYPE = 0x2200001F;  // 远程升级包类型错误
        public const UInt32 SS_ERROR_FIRM_INVALID_TIME_STAMP = 0x22000020;  // 时间戳错误的远程升级包
        public const UInt32 SS_ERROR_FIRM_INVALID_UPD_LIC_ID = 0x22000021;  // 错误的远程升级许可序号
        public const UInt32 SS_ERROR_FIRM_LIC_ALREADY_EXIST = 0x22000022;  // 添加的许可已经存在
        public const UInt32 SS_ERROR_FIRM_LICENCE_SIZE_LIMITTED = 0x22000023;  // 许可数量受限
        public const UInt32 SS_ERROR_FIRM_INVALID_DATA_FILE_OFFSET = 0x22000024;  // 无效的许可数据文件偏移
        public const UInt32 SS_ERROR_FIRM_ZERO_INDEX_LIC_DESTROY = 0x22000025;  // 零号许可损坏
        public const UInt32 SS_ERROR_FIRM_LIC_ALREADY_DISABLED = 0x22000026;  // 许可原已经被禁止
        public const UInt32 SS_ERROR_FIRM_INVALID_UPD_OPCODE = 0x22000027;  // 无效的远程升级操作码
        public const UInt32 SS_ERROR_FIRM_LIC_ALREADY_ENABLED = 0x22000028;  // 许可原已经有效
        public const UInt32 SS_ERROR_FIRM_INVALID_PKG_SIZE = 0x22000029;  // 远程升级包长度不正确
        public const UInt32 SS_ERROR_FIRM_LIC_COUNT_RETURN = 0x2200002A;  // 返回了错误的许可计数
        public const UInt32 SS_ERROR_FIRM_INVALID_OPERATION = 0x2200002B;  // 执行了不正确的操作
        public const UInt32 SS_ERROR_FIRM_SESSION_ALREADY_LOGOUT = 0x2200002C;  // session已经退出登录
        public const UInt32 SS_ERROR_FIRM_EXCHANGE_KEY_TIMEOUT = 0x2200002D;  // 交换密钥超时
        public const UInt32 SS_ERROR_FIRM_INVALID_EXCHANGE_KEY_MAGIC = 0x2200002E;  // 错误的交换密钥魔数
        public const UInt32 SS_ERROR_FIRM_INVALID_AUTH_CODE = 0x2200002F;  // 认证数据错误
        public const UInt32 SS_ERROR_FIRM_CONVERT_INDEX_TO_FILE = 0x22000030;  // 转换lic序号到文件名称失败
        public const UInt32 SS_ERROR_FIRM_INVALID_USER_DATA_TYPE = 0x22000031;  // 用户自定义字段类型错误
        public const UInt32 SS_ERROR_FIRM_INVALID_DATA_FILE_SIZE = 0x22000032;  // 用户自定义区域过大
        public const UInt32 SS_ERROR_FIRM_INVALID_CCRNT_OPR_TYPE = 0x22000033;  // 错误的并发计数操作类型
        public const UInt32 SS_ERROR_FIRM_ALL_LIC_TERMINATED = 0x22000034;  // 所有许可时间到期不可用
        public const UInt32 SS_ERROR_FIRM_INVALID_CCRNT_VALUE = 0x22000035;  // 错误的并发计数
        public const UInt32 SS_ERROR_FIRM_INVALID_UPD_FILE = 0x22000036;  // 不可用的删除历史记录文件
        public const UInt32 SS_ERROR_FIRM_UPD_RECORD_FULL = 0x22000037;  // 更新记录达到最大值
        public const UInt32 SS_ERROR_FIRM_UPDATE_FAILED = 0x22000038;  // 远程升级失败
        public const UInt32 SS_ERROR_FIRM_LICENSE_BEING_WRITTING = 0x22000039;  // 该许可正在被写入
        public const UInt32 SS_ERROR_FIRM_INVALID_PKG_FIELD_TYPE = 0x2200003A;  // 升级包子类型错误
        public const UInt32 SS_ERROR_FIRM_LOAT_FSM_SALT = 0x2200003B;  // 加载盐值文件出错
        public const UInt32 SS_ERROR_FIRM_DATA_LENGTH_ALIGNMENT = 0x2200003C;  // 加解密数据长度不对齐
        public const UInt32 SS_ERROR_FIRM_DATA_CRYPTION = 0x2200003D;  // 加解密数据错误
        public const UInt32 SS_ERROR_FIRM_SHORTCODE_UPDATE_NOT_SUPPORTED = 0x2200003E;  // 不支持短码升级
        public const UInt32 SS_ERROR_FIRM_INVALID_SHORTCODE = 0x2200003F;  // 不可用的短码
        public const UInt32 SS_ERROR_FIRM_LIC_USR_DATA_NOT_EXIST = 0x22000040;  // 用户自定义数据不存在
        public const UInt32 SS_ERROR_FIRM_RCD_FILE_NOT_INITIALIZED = 0x22000041;  // 删除记录文件未初始化
        public const UInt32 SS_ERROR_FIRM_AUTH_FILE_NOT_FOUND = 0x22000042;  // 认证文件找不到
        public const UInt32 SS_ERROR_FIRM_SESSION_OVERFLOW = 0x22000043;  // session会话数溢出（在不限制并发时导致超过最大计数）
        public const UInt32 SS_ERROR_FIRM_TIME_OVERFLOW = 0x22000044;  // 时间信息溢出，可能由于锁外pc时间被恶意修改所致
        public const UInt32 SS_ERROR_FIRM_REACH_FILE_LIS_END = 0x22000045;  // 枚举到达最后一个文件
        public const UInt32 SS_ERROR_FIRM_ANTI_MECHANISM_ACTIVED = 0x22000046;  // 惩罚计数触发锁定lm
        public const UInt32 SS_ERROR_FIRM_NO_BLOCK = 0x22000047;  // 获取block出错
        public const UInt32 SS_ERROR_FIRM_NOT_ENDED = 0x22000048;  // 数据未传输完毕   (特殊错误码)
        public const UInt32 SS_ERROR_FIRM_LIC_ALREADY_ACTIVE = 0x22000049;  // 许可已经激活
        public const UInt32 SS_ERROR_FIRM_FILE_NOT_FOUND = 0x22000050;  // 找不到文件
        public const UInt32 SS_ERROR_FIRM_UNKNOW_USER_DATA_TYPE = 0x22000051;  // 未知的用户数据类型
        public const UInt32 SS_ERROR_FIRM_INVALID_TF_CODE = 0x22000052;  // 错误的转移操作码
        public const UInt32 SS_ERROR_FIRM_UNMATCH_GUID = 0x22000053;  // 不匹配的GUID 
        public const UInt32 SS_ERROR_FIRM_UNABLE_TRANSFER = 0x22000054;  // 许可不可进行转移
        public const UInt32 SS_ERROR_FIRM_INVALID_TRANSCODE = 0x22000055;  // 不能识别的随机码
        public const UInt32 SS_ERROR_FIRM_ACCOUNT_NAME_NOT_FOUND = 0x22000056;  // 用户名未找到
        public const UInt32 SS_ERROR_FIRM_ACCOUNT_ID_NOT_FOUND = 0x22000057;  // 账户id未找到
        public const UInt32 SS_ERROR_FIRM_INVALID_XKEY_STEP = 0x22000058;  // 错误的秘钥交换过程
        public const UInt32 SS_ERROR_FIRM_INVLAID_DEVELOPER_ID = 0x22000059;  // 无效的开发商ID
        public const UInt32 SS_ERROR_FIRM_CA_TYPE = 0x2200005A;  // CA类型错误
        public const UInt32 SS_ERROR_FIRM_LIC_TRANSFER_FAILURE = 0x2200005B;  // 许可转移失败
        public const UInt32 SS_ERROR_FIRM_TF_PACKAGE_VERSION = 0x2200005C;  // 转移包版本号错误
        public const UInt32 SS_ERROR_FIRM_BEYOND_PKG_ITEM_SIZE = 0x2200005D;  // 升级包许可数量过大
        public const UInt32 SS_ERROR_FIRM_UNBOUND_ACCOUNT_INFO = 0x2200005E;  // 账户未绑定

        //============================================================
        //              MODE LIC TRANS 模块()0x28
        //============================================================
        public const UInt32 SS_ERROR_LIC_TRANS_NO_SN_DESC = 0x28000001;  // 未找到锁描述信息
        public const UInt32 SS_ERROR_LIC_TRANS_INVALID_DATA = 0x28000002;  // 数据格式错误

        //============================================================
        //              AUTH SERVER 模块 (0x29)
        //============================================================

        public const UInt32 SS_ERROR_AUTH_SERVER_INVALID_TOKEN = 0x29000001;  //无效的token
        public const UInt32 SS_ERROR_AUTH_SERVER_REFRESH_TOKEN = 0x29000002;  //刷新token失败
        public const UInt32 SS_ERROR_AUTH_SERVER_LOGIN_CANCELED = 0x29000003;  //用户取消登陆
        public const UInt32 SS_ERROR_AUTH_SERVER_GET_ALL_USER_INFO_FAIL = 0x29000004;  //获取所有用户信息失败

        //============================================================
        //              Cloud 模块 (0x30)
        //============================================================

        public const UInt32 SS_CLOUD_OK = 0x30000000;  //  成功
        public const UInt32 SS_ERROR_CLOUD_INVALID_PARAMETER = 0x30000001;  //  参数错误
        public const UInt32 SS_ERROR_CLOUD_QUERY_UESR_INFO = 0x30000002;  //  查询用户信息失败
        public const UInt32 SS_ERROR_CLOUD_INVALID_LICENSE_SESSION = 0x30000003;  //  许可未登录或已超时
        public const UInt32 SS_ERROR_CLOUD_DATA_EXPIRED = 0x30000004;  //  数据已过期
        public const UInt32 SS_ERROR_CLOUD_VERIFY_TIMESTAMP_SIGNATURE = 0x30000005;  //  时间戳签名验证失败
        public const UInt32 SS_ERROR_CLOUD_AUTH_FAILED = 0x30000006;  //  端到端认证失败
        public const UInt32 SS_ERROR_CLOUD_NOT_BOUND = 0x30000007;  //  算法不存在或未绑定
        public const UInt32 SS_ERROR_CLOUD_EXECUTE_FAILED = 0x30000008;  //  算法执行失败
        public const UInt32 SS_ERROR_CLOUD_INVALID_TOKEN = 0x30000010;  //  不合法的token
        public const UInt32 SS_ERROR_CLOUD_LICENSE_ALREADY_LOGIN = 0x30000011;  //  许可已登陆
        public const UInt32 SS_ERROR_CLOUD_LICENSE_EXPIRED = 0x30000012;  //  许可已到期
        public const UInt32 SS_ERROR_CLOUD_SESSION_KICKED = 0x30000013;  //  许可已被其它电脑登录
        public const UInt32 SS_ERROR_CLOUD_INVALID_SESSSION = 0x30001002;  //  无效的session
        public const UInt32 SS_ERROR_CLOUD_SESSION_TIMEOUT = 0x30001004;  //  会话超时
        public const UInt32 SS_ERROR_CLOUD_PARSE_PARAM = 0x30001007;  //  参数解析错误
        public const UInt32 SS_ERROR_CLOUD_LICENSE_LOGIN_SUCCESS = 0x31001000;  //  许可登录成功
        public const UInt32 SS_ERROR_CLOUD_LICENSE_NOT_EXISTS = 0x31001001;  //  许可不存在
        public const UInt32 SS_ERROR_CLOUD_LICENSE_NOT_ACTIVE = 0x31001002;  //  许可未激活
        public const UInt32 SS_ERROR_CLOUD_LICENSE_EXPIRED2 = 0x31001003;  //  许可已过期
        public const UInt32 SS_ERROR_CLOUD_LICENSE_COUNTER_IS_ZERO = 0x31001004;  //  许可无使用次数
        public const UInt32 SS_ERROR_CLOUD_LICENSE_RUNTIME_TIME_OUT = 0x31001005;  //  许可无使用时间
        public const UInt32 SS_ERROR_CLOUD_LICENSE_MAX_CONNECTION = 0x31001006;  //  许可并发量限制
        public const UInt32 SS_ERROR_CLOUD_LICENSE_LOCKED = 0x31001007;  //  许可被锁定
        public const UInt32 SS_ERROR_CLOUD_LICENSE_DATA_NOT_EXISTS = 0x31001008;  //  许可数据不存在
        public const UInt32 SS_ERROR_CLOUD_LICENSE_STILL_UNAVAILABLE = 0x31001010;  //  许可未到开始使用时间
        public const UInt32 SS_ERROR_CLOUD_ZERO_LICENSE_NOT_EXISTS = 0x31001011;  //  0号许可不存在
        public const UInt32 SS_ERROR_CLOUD_VERIFY_LICENSE = 0x31001012;  //  许可验证失败
        public const UInt32 SS_ERROR_CLOUD_EXECUTE_FILE_NOT_EXISTS = 0x31002000;  //  算法不存在
        public const UInt32 SS_ERROR_CLOUD_LICENSE_NOT_BOUND = 0x31003001;  //  算法未绑定

        private const int H5ErrorFlag = 0x01000000;
        /// <summary>
        /// 错误码分组对比
        /// </summary>
        /// <param name="resultCode">返回错误码</param>
        /// <param name="expectCode">期望错误码</param>
        /// <returns>错误码相同返回true，否则返回false</returns>
        public static bool Compare(UInt32 returnCode, UInt32 expectCode)
        {
            if ((returnCode & H5ErrorFlag) != 0)
            {//说明该错误码为H5错误码
                return expectCode == (0x00FFFFFF & returnCode);
            }
            else
            {
                return expectCode == returnCode;
            }
        }
    }
    internal class SlmRuntime
    {
        //[UnmanagedFunctionPointer(CallingConvention.StdCall)]
        //public delegate UInt32 SSRuntimeCallBack(UInt32 message, IntPtr wparam, IntPtr lparam);
        private static bool Is64 = IntPtr.Size == 8 ? true : false;

#if DEBUG
        // 调试使用可调试的运行时库（允许调试）
        const string dll_name32 = "x86/slm_runtime_dev.dll";
        const string dll_name64 = "x64/slm_runtime_dev.dll";
#else
        // 正式发版，使用具有反调试功能的运行时库（不允许调试）
        const string dll_name32 = "x86/slm_runtime.dll";
        const string dll_name64 = "x64/slm_runtime.dll";
#endif


        /// <summary>
        /// Runtime API初始化函数，调用所有Runtime API必须先调用此函数进行初始化
        /// </summary>
        ///  <param name="init_param"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#1", CallingConvention = CallingConvention.StdCall)]
        internal static extern UInt32 slm_init32(
             ref ST_INIT_PARAM initParam);
        [DllImport(dll_name64, EntryPoint = "#1", CallingConvention = CallingConvention.StdCall)]
        internal static extern UInt32 slm_init64(
             ref ST_INIT_PARAM initParam);

        internal static UInt32 slm_init(
            ref ST_INIT_PARAM initParam)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_init64(ref initParam);
            }
            return SlmRuntime.slm_init32(ref initParam);
        }

        /// <summary>
        /// 列举锁内某id许可
        /// </summary>
        /// <param name="license_id"></param>
        /// <param name="format"></param>
        /// <param name="license_desc"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#2", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_find_license32(
            UInt32 license_id,
            INFO_FORMAT_TYPE format,
            ref IntPtr license_desc);

        [DllImport(dll_name64, EntryPoint = "#2", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_find_license64(
            UInt32 license_id,
            INFO_FORMAT_TYPE format,
            ref IntPtr license_desc);


        internal static UInt32 slm_find_license(
            UInt32 license_id,
            INFO_FORMAT_TYPE format,
            ref IntPtr license_desc)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_find_license64(license_id, format, ref license_desc);
            }
            return SlmRuntime.slm_find_license32(license_id, format, ref license_desc);
        }

        /// <summary>
        /// 安全登录许可
        /// </summary>
        /// <param name="license_param"></param>
        /// <param name="param_format"></param>
        /// <param name="slm_handle"></param>
        /// <param name="auth"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#3", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_login32(
            ref ST_LOGIN_PARAM license_param,
            INFO_FORMAT_TYPE param_format,
            ref SLM_HANDLE_INDEX slm_handle,
            IntPtr auth);

        [DllImport(dll_name64, EntryPoint = "#3", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_login64(
            ref ST_LOGIN_PARAM license_param,
            INFO_FORMAT_TYPE param_format,
            ref SLM_HANDLE_INDEX slm_handle,
            IntPtr auth);

        internal static UInt32 slm_login(
            ref ST_LOGIN_PARAM license_param,
            INFO_FORMAT_TYPE param_format,
            ref SLM_HANDLE_INDEX slm_handle,
            IntPtr auth)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_login64(ref license_param, param_format, ref slm_handle, auth);
            }
            return SlmRuntime.slm_login32(ref license_param, param_format, ref slm_handle, auth);
        }

        /// <summary>
        /// 枚举已登录的用户token
        /// </summary>
        /// <param name="access_token">默认用户的token，指向一个字符串的IntPtr</param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#4", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_cloud_token32(
            ref IntPtr access_token);

        [DllImport(dll_name64, EntryPoint = "#4", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_cloud_token64(
            ref IntPtr access_token);

        internal static UInt32 slm_get_cloud_token(
            ref IntPtr access_token)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_cloud_token64(ref access_token);
            }
            return SlmRuntime.slm_get_cloud_token32(ref access_token);
        }

        /// <summary>
        /// 许可登出，并且释放许可句柄等资源
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#5", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_logout32(
            SLM_HANDLE_INDEX slm_handle);

        [DllImport(dll_name64, EntryPoint = "#5", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_logout64(
            SLM_HANDLE_INDEX slm_handle);

        internal static UInt32 slm_logout(
            SLM_HANDLE_INDEX slm_handle)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_logout64(slm_handle);
            }
            return SlmRuntime.slm_logout32(slm_handle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#6", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_keep_alive32(
            SLM_HANDLE_INDEX slm_handle);

        [DllImport(dll_name64, EntryPoint = "#6", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_keep_alive64(
            SLM_HANDLE_INDEX slm_handle);

        internal static UInt32 slm_keep_alive(
            SLM_HANDLE_INDEX slm_handle)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_keep_alive64(slm_handle);
            }
            return SlmRuntime.slm_keep_alive32(slm_handle);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="module_id"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#7", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_check_module32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 module_id);
        [DllImport(dll_name64, EntryPoint = "#7", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_check_module64(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 module_id);

        internal static UInt32 slm_check_module(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 module_id)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_check_module64(slm_handle, module_id);
            }
            return SlmRuntime.slm_check_module32(slm_handle, module_id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="inbuffer"></param>
        /// <param name="outbuffer"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#8", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_encrypt32(
                    SLM_HANDLE_INDEX slm_handle,
                    [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuffer,
                    [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] outbuffer,
                    UInt32 len);

        [DllImport(dll_name64, EntryPoint = "#8", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_encrypt64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuffer,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] outbuffer,
            UInt32 len);

        internal static UInt32 slm_encrypt(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuffer,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] outbuffer,
            UInt32 len)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_encrypt64(slm_handle, inbuffer, outbuffer, len);
            }
            return SlmRuntime.slm_encrypt32(slm_handle, inbuffer, outbuffer, len);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="inbuffer"></param>
        /// <param name="outbuffer"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#9", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_decrypt32(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuffer,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] outbuffer,
            UInt32 len);
        [DllImport(dll_name64, EntryPoint = "#9", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_decrypt64(
           SLM_HANDLE_INDEX slm_handle,
           [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuffer,
           [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] outbuffer,
           UInt32 len);

        internal static UInt32 slm_decrypt(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuffer,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] outbuffer,
            UInt32 len)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_decrypt64(slm_handle, inbuffer, outbuffer, len);
            }
            return SlmRuntime.slm_decrypt32(slm_handle, inbuffer, outbuffer, len);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="type"></param>
        /// <param name="pmem_size"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#10", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_user_data_getsize32(
            SLM_HANDLE_INDEX slm_handle,
            LIC_USER_DATA_TYPE type,
            ref UInt32 pmem_size);

        [DllImport(dll_name64, EntryPoint = "#10", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_user_data_getsize64(
           SLM_HANDLE_INDEX slm_handle,
           LIC_USER_DATA_TYPE type,
           ref UInt32 pmem_size);

        internal static UInt32 slm_user_data_getsize(
            SLM_HANDLE_INDEX slm_handle,
            LIC_USER_DATA_TYPE type,
            ref UInt32 pmem_size)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_user_data_getsize64(slm_handle, type, ref pmem_size);
            }
            return SlmRuntime.slm_user_data_getsize32(slm_handle, type, ref pmem_size);
        }

        /// <summary>
        /// 读许可数据，可以读取RW和ROM
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="type"></param>
        /// <param name="readbuf"></param>
        /// <param name="offset"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#11", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_user_data_read32(
            SLM_HANDLE_INDEX slm_handle,
            LIC_USER_DATA_TYPE type,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] readbuf,
            UInt32 offset,
            UInt32 len);

        [DllImport(dll_name64, EntryPoint = "#11", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_user_data_read64(
            SLM_HANDLE_INDEX slm_handle,
            LIC_USER_DATA_TYPE type,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] readbuf,
            UInt32 offset,
            UInt32 len);

        internal static UInt32 slm_user_data_read(
            SLM_HANDLE_INDEX slm_handle,
            LIC_USER_DATA_TYPE type,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] readbuf,
            UInt32 offset,
            UInt32 len)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_user_data_read64(slm_handle, type, readbuf, offset, len);
            }
            return SlmRuntime.slm_user_data_read32(slm_handle, type, readbuf, offset, len);
        }

        /// <summary>
        /// 写许可的读写数据区 ,数据区操作之前请先确认内存区的大小，可以使用slm_user_data_getsize获得
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="writebuf"></param>
        /// <param name="offset"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#12", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_user_data_write32(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] writebuf,
            UInt32 offset,
            UInt32 len);
        [DllImport(dll_name64, EntryPoint = "#12", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_user_data_write64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] writebuf,
            UInt32 offset,
            UInt32 len);

        internal static UInt32 slm_user_data_write(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] writebuf,
            UInt32 offset,
            UInt32 len)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_user_data_write64(slm_handle, writebuf, offset, len);
            }
            return SlmRuntime.slm_user_data_write32(slm_handle, writebuf, offset, len);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="info_type"></param>
        /// <param name="format"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#13", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_info32(
            SLM_HANDLE_INDEX slm_handle,
            INFO_TYPE info_type,
            INFO_FORMAT_TYPE format,
            ref IntPtr result);
        [DllImport(dll_name64, EntryPoint = "#13", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_info64(
            SLM_HANDLE_INDEX slm_handle,
            INFO_TYPE info_type,
            INFO_FORMAT_TYPE format,
            ref IntPtr result);

        internal static UInt32 slm_get_info(
            SLM_HANDLE_INDEX slm_handle,
            INFO_TYPE info_type,
            INFO_FORMAT_TYPE format,
            ref IntPtr result)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_info64(slm_handle, info_type, format, ref result);
            }
            return SlmRuntime.slm_get_info32(slm_handle, info_type, format, ref result);
        }



        /// <summary>
        /// 执行锁内算法
        /// </summary>
        /// <param name="slm_handle">许可句柄值</param>
        /// <param name="exfname">锁内执行文件名</param>
        /// <param name="inbuf">输入缓冲区</param>
        /// <param name="insize">输入长度</param>
        /// <param name="poutbuf">输出缓存区</param>
        /// <param name="outsize">输出缓存长度</param>
        /// <param name="pretsize">实际返回缓存长度</param>
        /// <returns>成功返回SS_OK，失败返回相应的错误码</returns>
        [DllImport(dll_name32, EntryPoint = "#14", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_execute_static32(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string exfname,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuf,
            UInt32 insize,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] poutbuf,
            UInt32 outsize,
            ref UInt32 pretsize);
        [DllImport(dll_name64, EntryPoint = "#14", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_execute_static64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string exfname,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuf,
            UInt32 insize,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] poutbuf,
            UInt32 outsize,
            ref UInt32 pretsize);


        internal static UInt32 slm_execute_static(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string exfname,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuf,
            UInt32 insize,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] poutbuf,
            UInt32 outsize,
            ref UInt32 pretsize)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_execute_static64(slm_handle, exfname, inbuf, insize, poutbuf, outsize, ref pretsize);
            }
            return SlmRuntime.slm_execute_static32(slm_handle, exfname, inbuf, insize, poutbuf, outsize, ref pretsize);
        }

        /// <summary>
        /// 许可动态执行代码，由开发商API gen_dynamic_code生成
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="exf_buffer"></param>
        /// <param name="exf_size"></param>
        /// <param name="inbuf"></param>
        /// <param name="insize"></param>
        /// <param name="poutbuf"></param>
        /// <param name="outsize"></param>
        /// <param name="pretsize"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#15", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_execute_dynamic32(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] exf_buffer,
            UInt32 exf_size,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuf,
            UInt32 insize,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] poutbuf,
            UInt32 outsize,
            ref UInt32 pretsize);

        [DllImport(dll_name64, EntryPoint = "#15", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_execute_dynamic64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] exf_buffer,
            UInt32 exf_size,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuf,
            UInt32 insize,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] poutbuf,
            UInt32 outsize,
            ref UInt32 pretsize);

        internal static UInt32 slm_execute_dynamic(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] exf_buffer,
            UInt32 exf_size,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] inbuf,
            UInt32 insize,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] poutbuf,
            UInt32 outsize,
            ref UInt32 pretsize)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_execute_dynamic64(slm_handle, exf_buffer, exf_size, inbuf, insize, poutbuf, outsize, ref pretsize);
            }
            return SlmRuntime.slm_execute_dynamic32(slm_handle, exf_buffer, exf_size, inbuf, insize, poutbuf, outsize, ref pretsize);
        }

        /// <summary>
        /// SS内存托管内存申请
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="size"></param>
        /// <param name="mem_id"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#17", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_alloc32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 size,
            ref UInt32 mem_id);
        [DllImport(dll_name64, EntryPoint = "#17", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_alloc64(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 size,
            ref UInt32 mem_id);

        internal static UInt32 slm_mem_alloc(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 size,
            ref UInt32 mem_id)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_mem_alloc64(slm_handle, size, ref mem_id);
            }
            return SlmRuntime.slm_mem_alloc32(slm_handle, size, ref mem_id);
        }

        /// <summary>
        /// 释放托管内存
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="mem_id"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#18", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_free32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id);
        [DllImport(dll_name64, EntryPoint = "#18", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_free64(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id);

        internal static UInt32 slm_mem_free(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_mem_free64(slm_handle, mem_id);
            }
            return SlmRuntime.slm_mem_free32(slm_handle, mem_id);
        }

        /// <summary>
        /// SS内存托管读
        /// </summary>
        /// <param name="slm_handle">许可句柄值</param>
        /// <param name="mem_id">托管内存id</param>
        /// <param name="offset">偏移</param>
        /// <param name="len">长度</param>
        /// <param name="readbuff">缓存</param>
        /// <param name="readlen">返回实际读的长度</param>
        /// <returns>成功返回SS_OK，失败返回相应的错误码</returns>
        [DllImport(dll_name32, EntryPoint = "#19", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_read32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id,
            UInt32 offset,
            UInt32 len,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] readbuff,
            ref UInt32 readlen);

        [DllImport(dll_name64, EntryPoint = "#19", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_read64(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id,
            UInt32 offset,
            UInt32 len,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] readbuff,
            ref UInt32 readlen);

        internal static UInt32 slm_mem_read(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id,
            UInt32 offset,
            UInt32 len,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] readbuff,
            ref UInt32 readlen)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_mem_read64(slm_handle, mem_id, offset, len, readbuff, ref readlen);
            }
            return SlmRuntime.slm_mem_read32(slm_handle, mem_id, offset, len, readbuff, ref readlen);
        }

        /// <summary>
        /// SS内存托管内存写入
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="mem_id"></param>
        /// <param name="offset"></param>
        /// <param name="len"></param>
        /// <param name="writebuff"></param>
        /// <param name="numberofbyteswritten"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#20", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_write32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id,
            UInt32 offset,
            UInt32 len,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] writebuff,
            ref UInt32 numberofbyteswritten);
        [DllImport(dll_name64, EntryPoint = "#20", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_mem_write64(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id,
            UInt32 offset,
            UInt32 len,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] writebuff,
            ref UInt32 numberofbyteswritten);

        internal static UInt32 slm_mem_write(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 mem_id,
            UInt32 offset,
            UInt32 len,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] readbuff,
            ref UInt32 readlen)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_mem_write64(slm_handle, mem_id, offset, len, readbuff, ref readlen);
            }
            return SlmRuntime.slm_mem_write32(slm_handle, mem_id, offset, len, readbuff, ref readlen);
        }

        /// <summary>
        /// 检测是否正在调试
        /// </summary>
        /// <param name="auth">auth 验证数据(目前填IntPtr.Zero即可）</param>
        /// <returns>SS_UINT32错误码, 返回SS_OK代表未调试</returns>
        [DllImport(dll_name32, EntryPoint = "#21", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_is_debug32(
             IntPtr auth);

        [DllImport(dll_name64, EntryPoint = "#21", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_is_debug64(
            IntPtr auth);

        internal static UInt32 slm_is_debug(
            IntPtr auth)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_is_debug64(auth);
            }
            return SlmRuntime.slm_is_debug32(auth);
        }

        /// <summary>
        /// 获取锁的设备证书
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="device_cert"></param>
        /// <param name="buff_size"></param>
        /// <param name="return_size"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#22", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_device_cert32(
            SLM_HANDLE_INDEX slm_handle,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] device_cert,
            UInt32 buff_size,
            ref UInt32 return_size);

        [DllImport(dll_name64, EntryPoint = "#22", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_device_cert64(
            SLM_HANDLE_INDEX slm_handle,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] device_cert,
            UInt32 buff_size,
            ref UInt32 return_size);

        internal static UInt32 slm_get_device_cert(
            SLM_HANDLE_INDEX slm_handle,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] device_cert,
            UInt32 buff_size,
            ref UInt32 return_size)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_device_cert64(slm_handle, device_cert, buff_size, ref return_size);
            }
            return SlmRuntime.slm_get_device_cert32(slm_handle, device_cert, buff_size, ref return_size);
        }

        /// <summary>
        /// 设备正版验证
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="verify_data"></param>
        /// <param name="verify_data_size"></param>
        /// <param name="signature"></param>
        /// <param name="signature_buf_size"></param>
        /// <param name="signature_size"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#23", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_sign_by_device32(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] verify_data,
            UInt32 verify_data_size,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 signature_buf_size,
            ref UInt32 signature_size);

        [DllImport(dll_name64, EntryPoint = "#23", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_sign_by_device64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] verify_data,
            UInt32 verify_data_size,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 signature_buf_size,
            ref UInt32 signature_size);

        internal static UInt32 slm_sign_by_device(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] verify_data,
            UInt32 verify_data_size,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 signature_buf_size,
            ref UInt32 signature_size)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_sign_by_device64(slm_handle, verify_data, verify_data_size, signature, signature_buf_size, ref signature_size);
            }
            return SlmRuntime.slm_sign_by_device32(slm_handle, verify_data, verify_data_size, signature, signature_buf_size, ref signature_size);
        }


        /// <summary>
        /// 获取时间修复数据，用于生成时钟校准请求
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="rand"></param>
        /// <param name="lock_time"></param>
        /// <param name="pc_time"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#24", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_adjust_time_request32(
            SLM_HANDLE_INDEX slm_handle,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] rand,
            ref UInt32 lock_time,
            ref UInt32 pc_time
            );
        [DllImport(dll_name64, EntryPoint = "#24", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_adjust_time_request64(
            SLM_HANDLE_INDEX slm_handle,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] rand,
            ref UInt32 lock_time,
            ref UInt32 pc_time
            );

        internal static UInt32 slm_adjust_time_request(
            SLM_HANDLE_INDEX slm_handle,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] rand,
            ref UInt32 lock_time,
            ref UInt32 pc_time
            )
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_adjust_time_request64(slm_handle, rand, ref lock_time, ref pc_time);
            }
            return SlmRuntime.slm_adjust_time_request32(slm_handle, rand, ref lock_time, ref pc_time);
        }


        /// <summary>
        /// 闪烁指示灯
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="led_ctrl"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#25", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_led_control32(
            SLM_HANDLE_INDEX slm_handle,
            ref ST_LED_CONTROL led_ctrl);
        [DllImport(dll_name64, EntryPoint = "#25", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_led_control64(
           SLM_HANDLE_INDEX slm_handle,
           ref ST_LED_CONTROL led_ctrl);

        internal static UInt32 slm_led_control(
            SLM_HANDLE_INDEX slm_handle,
            ref ST_LED_CONTROL led_ctrl)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_led_control64(slm_handle, ref led_ctrl);
            }
            return SlmRuntime.slm_led_control32(slm_handle, ref led_ctrl);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="api_version"></param>
        /// <param name="ss_version"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#26", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_version32(
            ref UInt32 api_version,
            ref UInt32 ss_version);
        [DllImport(dll_name64, EntryPoint = "#26", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_version64(
            ref UInt32 api_version,
            ref UInt32 ss_version);

        internal static UInt32 slm_get_version(
            ref UInt32 api_version,
            ref UInt32 ss_version)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_version64(ref api_version, ref ss_version);
            }
            return SlmRuntime.slm_get_version32(ref api_version, ref ss_version);
        }

        /// <summary>
        /// 升级许可
        /// </summary>
        /// <param name="d2c_pkg">许可D2C数据</param>
        /// <param name="error_msg">错误信息（json）</param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#27", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_update32(
            [In, MarshalAs(UnmanagedType.LPStr)] string d2c_pkg,
            ref IntPtr error_msg);

        [DllImport(dll_name64, EntryPoint = "#27", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_update64(
            [In, MarshalAs(UnmanagedType.LPStr)] string d2c_pkg,
            ref IntPtr error_msg);

        internal static UInt32 slm_update(
            [In, MarshalAs(UnmanagedType.LPStr)] string d2c_pkg,
            ref IntPtr error_msg)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_update64(d2c_pkg, ref error_msg);
            }
            return SlmRuntime.slm_update32(d2c_pkg, ref error_msg);
        }

        /// <summary>
        ///  将D2C包进行升级
        /// </summary>
        /// <param name="lock_sn"></param>
        /// <param name="d2c_pkg"></param>
        /// <param name="error_msg"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#28", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_update_ex32(
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] lock_sn,
            [In, MarshalAs(UnmanagedType.LPStr)] string d2c_pkg,
            ref IntPtr error_msg);

        [DllImport(dll_name64, EntryPoint = "#28", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_update_ex64(
             [In, MarshalAs(UnmanagedType.LPArray)] byte[] lock_sn,
            [In, MarshalAs(UnmanagedType.LPStr)] string d2c_pkg,
            ref IntPtr error_msg);

        internal static UInt32 slm_update_ex(
             [In, MarshalAs(UnmanagedType.LPArray)] byte[] lock_sn,
            [In, MarshalAs(UnmanagedType.LPStr)] string d2c_pkg,
            ref IntPtr error_msg)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_update_ex64(lock_sn, d2c_pkg, ref error_msg);
            }
            return SlmRuntime.slm_update_ex32(d2c_pkg, ref error_msg);
        }

        private static uint slm_update_ex32(string d2c_pkg, ref IntPtr error_msg)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///  枚举本地锁信息
        /// </summary>
        /// <param name="device_info"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#29", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_enum_device32(
                   ref IntPtr device_info);

        [DllImport(dll_name64, EntryPoint = "#29", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_enum_device64(
          ref IntPtr device_info);

        internal static UInt32 slm_enum_device(
           ref IntPtr device_info)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_enum_device64(ref device_info);
            }
            return SlmRuntime.slm_enum_device32(ref device_info);
        }

        /// <summary>
        ///   
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#30", CallingConvention = CallingConvention.StdCall)]
        public static extern void slm_free32(IntPtr buffer);

        [DllImport(dll_name64, EntryPoint = "#30", CallingConvention = CallingConvention.StdCall)]
        public static extern void slm_free64(IntPtr buffer);

        internal static void slm_free(IntPtr buffer)
        {
            if (SlmRuntime.Is64)
            {
                SlmRuntime.slm_free64(buffer);
                return;
            }
            SlmRuntime.slm_free32(buffer);
            return;
        }

        /// <summary>
        ///   获取API对应的开发商ID
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#31", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_developer_id32(
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] buffer);

        [DllImport(dll_name64, EntryPoint = "#31", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_developer_id64(
           [Out, MarshalAs(UnmanagedType.LPArray)] byte[] buffer);

        internal static UInt32 slm_get_developer_id(
           [Out, MarshalAs(UnmanagedType.LPArray)] byte[] buffer)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_developer_id64(buffer);
            }
            return SlmRuntime.slm_get_developer_id32(buffer);
        }

        /// <summary>
        /// 通过错误码获得错误信息
        /// </summary>
        /// <param name="error_code"></param>
        /// <param name="language_id"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#32", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr slm_error_format32(
           UInt32 error_code,
           UInt32 language_id
            );
        [DllImport(dll_name64, EntryPoint = "#32", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr slm_error_format64(
           UInt32 error_code,
           UInt32 language_id
            );

        internal static IntPtr slm_error_format(
           UInt32 error_code,
           UInt32 language_id
            )
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_error_format64(error_code, language_id);
            }
            return SlmRuntime.slm_error_format32(error_code, language_id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#33", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_cleanup32();

        [DllImport(dll_name64, EntryPoint = "#33", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_cleanup64();

        internal static UInt32 slm_cleanup()
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_cleanup64();
            }
            return SlmRuntime.slm_cleanup32();
        }


        /// <summary>
        /// 碎片代码执行（开发者不必关心）
        /// </summary>
        /// <param name="slm_handle"></param> 
        /// <param name="snippet_code"></param>
        /// <param name="code_size"></param>
        /// <param name="input"></param>
        /// <param name="input_size"></param>
        /// <param name="output"></param>
        /// <param name="outbuf_size"></param>
        /// <param name="output_size"></param> 
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#35", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_snippet_execute32(
                    SLM_HANDLE_INDEX slm_handle,
                    [In, MarshalAs(UnmanagedType.LPArray)] byte[] d2c_pkg,
                    UInt32 code_size,
                    [In, MarshalAs(UnmanagedType.LPArray)] byte[] input,
                    UInt32 input_size,
                    [Out, MarshalAs(UnmanagedType.LPArray)] byte[] output,
                    UInt32 outbuf_size,
                    ref UInt32 language_id);
        [DllImport(dll_name64, EntryPoint = "#35", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_snippet_execute64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] d2c_pkg,
            UInt32 code_size,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] input,
            UInt32 input_size,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] output,
            UInt32 outbuf_size,
            ref UInt32 language_id);

        internal static UInt32 slm_snippet_execute(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] d2c_pkg,
            UInt32 code_size,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] input,
            UInt32 input_size,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] output,
            UInt32 outbuf_size,
            ref UInt32 language_id)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_snippet_execute64(slm_handle, d2c_pkg, code_size, input, input_size, output, outbuf_size, ref language_id);
            }
            return SlmRuntime.slm_snippet_execute32(slm_handle, d2c_pkg, code_size, input, input_size, output, outbuf_size, ref language_id);
        }


        /// <summary>
        /// 获得指定许可的公开区数据区大小，需要登录0号许可
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="license_id"></param>
        /// <param name="pmem_size"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#36", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_pub_data_getsize32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 license_id,
            ref UInt32 pmem_size);

        [DllImport(dll_name64, EntryPoint = "#36", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_pub_data_getsize64(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 license_id,
            ref UInt32 pmem_size);

        internal static UInt32 slm_pub_data_getsize(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 license_id,
            ref UInt32 pmem_size)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_pub_data_getsize64(slm_handle, license_id, ref pmem_size);
            }
            return SlmRuntime.slm_pub_data_getsize32(slm_handle, license_id, ref pmem_size);
        }


        /// <summary>
        /// 获得指定许可的公开区数据区大小，需要登录0号许可
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="license_id"></param>
        /// <param name="readbuf"></param>
        /// <param name="offset"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#37", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_pub_data_read32(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 license_id,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] readbuf,
            UInt32 offset,
            UInt32 len);
        [DllImport(dll_name64, EntryPoint = "#37", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_pub_data_read64(
           SLM_HANDLE_INDEX slm_handle,
           UInt32 license_id,
           [Out, MarshalAs(UnmanagedType.LPArray)] byte[] readbuf,
           UInt32 offset,
           UInt32 len);

        internal static UInt32 slm_pub_data_read(
            SLM_HANDLE_INDEX slm_handle,
            UInt32 license_id,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] readbuf,
            UInt32 offset,
            UInt32 len)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_pub_data_read64(slm_handle, license_id, readbuf, offset, len);
            }
            return SlmRuntime.slm_pub_data_read32(slm_handle, license_id, readbuf, offset, len);
        }

        /// <summary>
        /// 锁内短码升级
        /// </summary>
        /// <param name="lock_sn"></param>
        /// <param name="inside_file"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#38", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_d2c_update_inside32(
            [In, MarshalAs(UnmanagedType.LPStr)] string lock_sn,
            [In, MarshalAs(UnmanagedType.LPStr)] string inside_file);

        [DllImport(dll_name64, EntryPoint = "#38", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_d2c_update_inside64(
            [In, MarshalAs(UnmanagedType.LPStr)] string lock_sn,
            [In, MarshalAs(UnmanagedType.LPStr)] string inside_file);

        internal static UInt32 slm_d2c_update_inside(
            [In, MarshalAs(UnmanagedType.LPStr)] string lock_sn,
            [In, MarshalAs(UnmanagedType.LPStr)] string inside_file)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_d2c_update_inside64(lock_sn, inside_file);
            }
            return SlmRuntime.slm_d2c_update_inside32(lock_sn, inside_file);
        }

        /// <summary>
        /// 枚举指定设备下所有许可ID
        /// </summary>
        /// <param name="device_info"></param>
        /// <param name="license_ids"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#39", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_enum_license_id32(
            [In, MarshalAs(UnmanagedType.LPStr)] string device_info,
            ref IntPtr license_ids);
        [DllImport(dll_name64, EntryPoint = "#39", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_enum_license_id64(
           [In, MarshalAs(UnmanagedType.LPStr)] string device_info,
           ref IntPtr license_ids);


        internal static UInt32 slm_enum_license_id(
            [In, MarshalAs(UnmanagedType.LPStr)] string device_info,
            ref IntPtr license_ids)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_enum_license_id64(device_info, ref license_ids);
            }
            return SlmRuntime.slm_enum_license_id32(device_info, ref license_ids);
        }


        /// <summary>
        /// 枚举指定设备下所有许可ID
        /// </summary>
        /// <param name="device_info"></param>
        /// <param name="license_id"></param>
        /// <param name="license_info"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#40", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_license_info32(
            [In, MarshalAs(UnmanagedType.LPStr)] string device_info,
            UInt32 license_id,
            ref IntPtr license_info);

        [DllImport(dll_name64, EntryPoint = "#40", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_license_info64(
            [In, MarshalAs(UnmanagedType.LPStr)] string device_info,
            UInt32 license_id,
            ref IntPtr license_info);

        internal static UInt32 slm_get_license_info(
            [In, MarshalAs(UnmanagedType.LPStr)] string device_info,
            UInt32 license_id,
            ref IntPtr license_info)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_license_info64(device_info, license_id, ref license_info);
            }
            return SlmRuntime.slm_get_license_info32(device_info, license_id, ref license_info);
        }


        /// <summary>
        /// 使用已登录的云许可进行签名（仅支持云锁）
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="sign_data"></param>
        /// <param name="sign_length"></param>
        ///  <param name="signature"></param>
        ///   <param name="max_buf_size"></param>
        ///    <param name="signature_length"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#41", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_license_sign32(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] sign_data,
            UInt32 sign_length,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 max_buf_size,
            ref UInt32 signature_length);

        [DllImport(dll_name64, EntryPoint = "#41", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_license_sign64(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] sign_data,
            UInt32 sign_length,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 max_buf_size,
            ref UInt32 signature_length);

        internal static UInt32 slm_license_sign(
            SLM_HANDLE_INDEX slm_handle,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] sign_data,
            UInt32 sign_length,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 max_buf_size,
            ref UInt32 signature_length)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_license_sign64(slm_handle, sign_data, sign_length, signature, max_buf_size, ref signature_length);
            }
            return SlmRuntime.slm_license_sign32(slm_handle, sign_data, sign_length, signature, max_buf_size, ref signature_length);
        }


        /// <summary>
        /// 对云许可签名后的数据进行验签（仅支持云锁）
        /// </summary>
        /// <param name="sign_data"></param>
        /// <param name="sign_length"></param>
        ///  <param name="signature"></param>
        ///   <param name="signature_length"></param>
        ///    <param name="sign_info"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#42", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_license_verify32(
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] sign_data,
            UInt32 sign_length,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 signature_length,
            ref IntPtr sign_info);

        [DllImport(dll_name64, EntryPoint = "#42", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_license_verify64(
           [In, MarshalAs(UnmanagedType.LPArray)] byte[] sign_data,
           UInt32 sign_length,
           [In, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
           UInt32 signature_length,
           ref IntPtr sign_info);

        internal static UInt32 slm_license_verify(
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] sign_data,
            UInt32 sign_length,
            [In, MarshalAs(UnmanagedType.LPArray)] byte[] signature,
            UInt32 signature_length,
            ref IntPtr sign_info)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_license_verify64(sign_data, sign_length, signature, signature_length, ref sign_info);
            }
            return SlmRuntime.slm_license_verify32(sign_data, sign_length, signature, signature_length, ref sign_info);
        }

        /// <summary>
        /// 通过证书类型，获取已登录许可的设备证书
        /// </summary>
        /// <param name="slm_handle"></param>
        /// <param name="cert_type"></param>
        ///  <param name="cert"></param>
        ///   <param name="cert_size"></param>
        ///    <param name="cert_len"></param>
        /// <returns></returns>
        [DllImport(dll_name32, EntryPoint = "#43", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_cert32(
            SLM_HANDLE_INDEX slm_handle,
            CERT_TYPE cert_type,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] cert,
            UInt32 cert_size,
            ref UInt32 cert_len);

        [DllImport(dll_name64, EntryPoint = "#43", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 slm_get_cert64(
            SLM_HANDLE_INDEX slm_handle,
            CERT_TYPE cert_type,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] cert,
            UInt32 cert_size,
            ref UInt32 cert_len);

        internal static UInt32 slm_get_cert(
            SLM_HANDLE_INDEX slm_handle,
            CERT_TYPE cert_type,
            [Out, MarshalAs(UnmanagedType.LPArray)] byte[] cert,
            UInt32 cert_size,
            ref UInt32 cert_len)
        {
            if (SlmRuntime.Is64)
            {
                return SlmRuntime.slm_get_cert64(slm_handle, cert_type, cert, cert_size, ref cert_len);
            }
            return SlmRuntime.slm_get_cert32(slm_handle, cert_type, cert, cert_size, ref cert_len);
        }
    }
    public class SSDefine
    {
        public const UInt32 LED_COLOR_BLUE = 0;         //闪灯颜色：蓝色
        public const UInt32 LED_COLOR_RED = 1;          //闪灯颜色：红色

        public const UInt32 LED_STATE_CLOSE = 0;        //闪灯控制：关闭
        public const UInt32 LED_STATE_OPEN = 1;         //闪灯控制：打开
        public const UInt32 LED_STATE_SHRINK = 2;       //闪灯控制：闪烁

        //============================================================
        //              回调消息 message 类型
        //============================================================
        public const UInt32 SS_ANTI_INFORMATION = 0x0101;  //  消息类型：信息提示
        public const UInt32 SS_ANTI_WARNING = 0x0102;  //  消息类型：警告
        public const UInt32 SS_ANTI_EXCEPTION = 0x0103;  //  消息类型：异常
        public const UInt32 SS_ANTI_IDLE = 0x0104;  //  消息类型：暂保留


        public const UInt32 SS_MSG_SERVICE_START = 0x0200;  //  服务启动
        public const UInt32 SS_MSG_SERVICE_STOP = 0x0201;  //  服务停止
        public const UInt32 SS_MSG_LOCK_AVAILABLE = 0x0202;  //  锁可用（插入锁或SS启动时锁已初始化完成）回调函数wparam 代表锁号
        public const UInt32 SS_MSG_LOCK_UNAVAILABLE = 0x0203;  //  锁无效（锁已拔出）回调函数wparam 代表锁号


        //============================================================
        //              回调消息 wparam 参数
        //============================================================
        public const UInt32 SS_ANTI_PATCH_INJECT = 0x0201;  //  发现注入
        public const UInt32 SS_ANTI_MODULE_INVALID = 0x0202;  //  模块检测失败
        public const UInt32 SS_ANTI_ATTACH_FOUND = 0x0203;  //  发现被调试器附加
        public const UInt32 SS_ANTI_THREAD_INVALID = 0x0204;  //  无效的线程
        public const UInt32 SS_ANTI_THREAD_ERROR = 0x0205;  //  线程检测失败
        public const UInt32 SS_ANTI_CRC_ERROR = 0x0206;  //  CRC检验失败
        public const UInt32 SS_ANTI_DEBUGGER_FOUND = 0x0207;  //  发现调试器


        public const UInt32 SLM_FIXTIME_RAND_LENGTH = 0x08;    //  时钟校准随机数种子长度

        public const UInt32 SLM_CALLBACK_VERSION02 = 0x02;    //  SS_CALL_BACK的版本 （支持开发商API密码的版本）


        public const UInt32 SLM_MEM_MAX_SIZE = 2048;  //  内存托管最大字节
        public const UInt32 SLM_MAX_INPUT_SIZE = 1758;  //  代码执行，最大输入缓冲区大小（字节）
        public const UInt32 SLM_MAX_OUTPUT_SIZE = 1758;  //  代码执行，最大输出缓冲区大小（字节）
        public const UInt32 SLM_MAX_USER_CRYPT_SIZE = 1520;  //  加解密最大缓冲区大小（字节）

        public const UInt32 SLM_MAX_USER_DATA_SIZE = 2048;  //  用户数据区最大长度（字节）
        public const UInt32 SLM_MAX_WRITE_SIZE = 1904;  //  用户数据区写入最大长度（字节）

        public const String SLM_VERIFY_DEVICE_PREFIX = "SENSELOCK";  //  请求签名的数据前缀

        public const UInt32 SLM_VERIFY_DATA_SIZE = 41;  //  请求签名的数据大小，见slm_verify_device
        public const UInt32 SLM_LOCK_SN_LENGTH = 16;  //  锁号的长度
        public const UInt32 SLM_DEVELOPER_ID_SIZE = 8;  //  开发商ID长度
        public const UInt32 SLM_MAX_SERVER_NAME = 32;  //  服务器名称最大长度
        public const UInt32 SLM_MAX_ACCESS_TOKEN_LENGTH = 64;  //  云锁用户token最大长度
        public const UInt32 SLM_MAX_CLOUD_SERVER_LENGTH = 100;  //  云锁服务器地址最大长度
        public const UInt32 SLM_SNIPPET_SEED_LENGTH = 32;  //  碎片代码种子长度
        public const UInt32 SLM_DEV_PASSWORD_LENGTH = 16;  //  开发商密码长度

        public const UInt32 SLM_CLOUD_MAX_USER_GUID_SIZE = 128;   //  最大用户GUID长度 

        public const UInt32 SLM_FILE_TYPE_BINARY = 0;     //  文件类型：数据文件
        public const UInt32 SLM_FILE_TYPE_EXECUTIVE = 1;     //  文件类型：可执行文件文件
        public const UInt32 SLM_FILE_TYPE_KEY = 2;     //  文件类型：密钥文件 

        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_READ = 0x01;  //  可读
        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_WRITE = 0x02;  //  可写
        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_USE = 0x04;  //  （密钥文件）可使用
        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_UPDATE = 0x08;  //  可远程升级

        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_ENTRY_READ = 0x10;  //  可读
        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_ENTRY_WRITE = 0x20;  //  可写
        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_ENTRY_USE = 0x40;  //  （密钥文件）可使用
        public const UInt32 SLM_FILE_PRIVILEGE_FLAG_ENTRY_UPDATE = 0x80;  //  可远程升级

        public const UInt32 SLM_LOGIN_MODE_AUTO = 0x0000;  //  自动登录模式（依次尝试登录硬件锁、软锁、云锁、网络锁许可）
        public const UInt32 SLM_LOGIN_MODE_LOCAL = 0x0001;  //  指定登录本地USE锁
        public const UInt32 SLM_LOGIN_MODE_REMOTE = 0x0002;  //  指定登录远程USE锁 
        public const UInt32 SLM_LOGIN_MODE_CLOUD = 0x0004;  //  指定登录云锁  
        public const UInt32 SLM_LOGIN_MODE_SLOCK = 0x0008;  //  指定登录软锁

        public const UInt32 SLM_LOGIN_FLAG_FIND_ALL = 0x0001;  //  查找所有的锁，如果发现多个重名许可则不登录，提供选择，否则找到符合条件的锁直接登录
        public const UInt32 SLM_LOGIN_FLAG_VERSION = 0x0004;  //  指定许可版本
        public const UInt32 SLM_LOGIN_FLAG_LOCKSN = 0x0008;  //  指定锁号（USB）
        public const UInt32 SLM_LOGIN_FLAG_SERVER = 0x0010;  //  指定服务器
        public const UInt32 SLM_LOGIN_FLAG_SNIPPET = 0x0020;  //  指定碎片代码

        public const UInt32 LANGUAGE_CHINESE_ASCII = 0x0001;  //  语言ID：简体中文
        public const UInt32 LANGUAGE_ENGLISH_ASCII = 0x0002;  //  语言ID：英语
        public const UInt32 LANGUAGE_TRADITIONAL_CHINESE_ASCII = 0x0003;  //  语言ID：繁体中文

        public const UInt32 SLM_INIT_FLAG_NOTIFY = 0x01;  //  表示将收到SS的消息通知
    }
}
