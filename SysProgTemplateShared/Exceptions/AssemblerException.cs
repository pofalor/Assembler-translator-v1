using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysProgTemplateShared.Exceptions
{
    public class AssemblerException : Exception
    {
        public AssemblerException()
        {
        }

        public AssemblerException(string message)
            : base(message)
        {
        }

        public AssemblerException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
