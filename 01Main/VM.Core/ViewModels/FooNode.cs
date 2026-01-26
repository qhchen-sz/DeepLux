using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HV.ViewModels
{
    public class FooNode
    {
        public FooEnum FooType { get; set; }

        public string Name
        {
            get { return this.FooType.ToString(); }
        }
    }
}
