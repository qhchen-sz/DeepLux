using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.GSD.ViewModels
{
    public class GSDA
    {
        #region C sharp method

        public static PointArray PreProcessArray(List<double> X, List<double> Y, List<double> Z) {
            PointArray pa = new PointArray();
            double[] x = X.ToArray();
            double[] y = Y.ToArray();
            double[] z = Z.ToArray();
            int type_length = sizeof(double);
            IntPtr ptrx = Marshal.AllocHGlobal(x.Length * type_length);
            IntPtr ptry = Marshal.AllocHGlobal(y.Length * type_length);
            IntPtr ptrz = Marshal.AllocHGlobal(z.Length * type_length);
            Marshal.Copy(x, 0, ptrx, x.Length);
            Marshal.Copy(y, 0, ptry, y.Length);
            Marshal.Copy(z, 0, ptrz, z.Length);
            pa.x = ptrx;
            pa.y = ptry;
            pa.z = ptrz;
            pa.length = x.Length;
            return pa;
        }

        public static void FreePontArray(PointArray pa)
        {
            if (pa.x != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pa.x);
                pa.x = IntPtr.Zero;
            }
            if (pa.y != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pa.y);
                pa.y = IntPtr.Zero;
            }
            if (pa.z != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pa.z);
                pa.z = IntPtr.Zero;
            }
        }

        public static void RunGSDAFix(List<double> X,
            List<double> Y, List<double> Z, ref Vector3d transformationMatrix, 
            ref double height_threshold, out ResultPara result, string debug_path,
            bool deep_mode, bool debug_mode = false)
                {
                    PointArray pa = PreProcessArray(X, Y, Z);
                    detect_gap_step_array_dll_fix(ref pa, ref transformationMatrix, ref height_threshold, out result, debug_path, deep_mode, debug_mode);
                    FreePontArray(pa);
                }

        #endregion

        #region dll method

        [StructLayout(LayoutKind.Sequential)]
        public struct Vector3d
        {
            public double x;
            public double y;
            public double z;
            public Vector3d(double x, double y, double z)
            {
                this.x = x; this.y = y; this.z = z;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct PointArray
        {
            public IntPtr x;
            public IntPtr y;
            public IntPtr z;
            public int length;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ResultPara
        {
            public double step_height;
            public double step_width;
        }

        [DllImport("GapStepDetect.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void detect_gap_step_array_dll_fix(ref PointArray pa,
            ref Vector3d transformationMatrix,
            ref double height_threshold,
            out ResultPara result,
            string debug_path,
            bool deep_mode,
            bool debug_mode);
        #endregion
    }
}
