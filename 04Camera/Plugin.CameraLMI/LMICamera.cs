using HalconDotNet;
using HV.Common.Enums;
using HV.Dialogs.Views;
using Lmi3d.GoSdk;
using Lmi3d.GoSdk.Messages;
using Lmi3d.Zen;
using Lmi3d.Zen.Io;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.CameraLMI
{

    public class LMICamera
    {
        public GoSystem system;
        public GoSensor sensor;
        public  string SENSOR_IP = "127.0.0.1";
        public LMICamera()
        {
            KApiLib.Construct();
            GoSdkLib.Construct();
            system = new GoSystem();

        }
        public KIpAddress kIpAddress;

        // Data callback function
        // This function is called from a separate thread spawned by the GoSDK library.
        // Processing within this function should be minimal.
        
        public List<CamInfo> SelectCam()//获取相机
        {
            List<CamInfo> cam = new List<CamInfo>();
            int CamNum  = (int)system.SensorCount;
            for (int i = 0; i < CamNum; i++)
            {
                
                GoSensor goSensor = system.GetSensor(i);
                CamInfo camInfo = new CamInfo() { ID = goSensor.Id,Name = goSensor .Model,Index = i};
                cam.Add(camInfo);
            }
            return cam;
        }
        public bool ConnectCam(CamInfo camInfo)
        {

            try
            {
                sensor = system.GetSensor(camInfo.Index);
                sensor.Connect();
                return true;
            }
            catch (Exception e)
            {
                MessageView.Ins.MessageBoxShow(e.ToString(), eMsgType.Warn);
            }
            return false;
        }
        public bool SetCallbackFunc(CameraLMI lMI)
        {
            try
            {
                sensor.EnableData(true);
                sensor.SetDataHandler(lMI.ImageCallbackFunc);
                return true;
            }
            catch (Exception e)
            {
                MessageView.Ins.MessageBoxShow(e.ToString(), eMsgType.Warn);
            }
            return false;


        }
        public bool SetImageHeight( int Value)
        {
            try
            {
                if(sensor.State == GoState.Running)
                {
                    sensor.Stop();
                }
                sensor.Setup.GetSurfaceGeneration().GenerationType = GoSurfaceGenerationType.FixedLength;
                sensor.Setup.GetSurfaceGeneration().FixedLengthLength = Value;
                sensor.Flush();
                return true;
            }
            catch (Exception e)
            {
                MessageView.Ins.MessageBoxShow(e.ToString(), eMsgType.Warn);
            }
            return false;
        }
        public bool Stop()
        {
            
            try
            {
                sensor.Stop();
                return true;
            }
            catch (Exception e)
            {
                MessageView.Ins.MessageBoxShow(e.ToString(), eMsgType.Warn);
            }
            return false;
        }
        public bool Start()
        {
            
            try
            {
                sensor.Stop();
                //sensor.Setup.GetSurfaceGeneration().FixedLengthLength = 2000;
                sensor.Start();
                return true;
            }
            catch (Exception e)
            {
                MessageView.Ins.MessageBoxShow(e.ToString(), eMsgType.Warn);
            }
            return false;
        }
        
        public bool Close()
        {
            try
            {
                sensor.Disconnect();
                return true;
            }
            catch (Exception e )
            {
                MessageView.Ins.MessageBoxShow(e.ToString(), eMsgType.Warn);
            }
            return false;
        }
        public class CamInfo
        {
            public string Name { get; set; }
            public int Index { get; set; }
            public uint ID { get; set; }
        }
        public class DataContext
        {
            public double xResolution;
            public double yResolution;
            public double zResolution;
            public double xOffset;
            public double yOffset;
            public double zOffset;
            public uint serialNumber;
        }
        public struct GoPoints
        {
            public Int16 x;
            public Int16 y;
            public Int16 z;
        }

        public struct SurfacePoint
        {
            public double x;
            public double y;
            public double z;
            byte intensity;
        }
    }



}

