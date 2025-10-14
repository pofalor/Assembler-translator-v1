using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Diagnostics;
using SysProgTemplateShared.Helpers; 


namespace SysProgTemplate.Components
{
    public class SourceCodeTextBox : TextBox
    {
        public List<List<string>> GetCode()
        {
            return Parser.ParseCode(this.Text); 
        }
    }
}
