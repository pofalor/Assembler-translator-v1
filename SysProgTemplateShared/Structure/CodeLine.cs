using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SysProgTemplateShared.Structure
{
    public class CodeLine
    {
        public string? Label { get; set; }
        public string Command { get; set; } = default!; 
        public string? FirstOperand { get; set; } 
        public string? SecondOperand { get; set; } 
    }
}
