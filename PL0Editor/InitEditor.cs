using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using System.IO;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using System.Reflection;
namespace PL0Editor
{
    public class InitEditor
    {
        public static TextEditor Target { get; set; }

        private static void InitialHighlight()
        {
            using (Stream stream = new FileStream("PL0.xshd",FileMode.Open))
            {
                if (stream is null)
                {
                     
                }
            }
        }
        private static 
    }
}
