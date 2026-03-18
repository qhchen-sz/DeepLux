using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.AIPost.ViewModels
{
    public static class Algorithm
    {
        // Local procedures 
        public static void shuchutupian(HObject ho_Image, out HObject ho_DupImage)
        {


            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_DupImage);
            ho_DupImage.Dispose();
            HOperatorSet.CopyImage(ho_Image, out ho_DupImage);


            return;
        }
    }
}
