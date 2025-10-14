using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysProgTemplateShared.Structure; 


namespace SysProgTemplateShared.Dto
{
    public class CommandDto
    {
        public string Name { get; set; } = default!; 
        public string Code { get; set; } = default!; 
        public string Length { get; set; } = default!;

        public CommandDto() { } 

        public CommandDto(Command command) 
        { 
            Name = command.Name;
            Code = command.Code.ToString(); 
            Length = command.Length.ToString(); 
        }
    }
}
