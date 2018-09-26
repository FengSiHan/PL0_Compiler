using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Compiler;
using Microsoft.Win32;
using MahApps.Metro.Controls;
using System.Threading;
using System.IO;
using RTFExporter;
using System.Text;

namespace PL0Editor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private Compiler.Position Location { get; set; }
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var tr = new TextRange(CodeEditor.Document.ContentStart, CodeEditor.Document.ContentEnd);
            if (tr.Text.Length == 0)
            {
                MessageBox.Show(tr.Text);
                e.CanExecute = false;
            }
            else
            {
                e.CanExecute = true;
            }
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "文本文件|*.txt|PL0文件|*.pl0|所有文件|*.*";
            bool? result = dialog.ShowDialog();
            if (result.Value)
            {

                var tr = new TextRange(CodeEditor.Document.ContentStart, CodeEditor.Document.ContentEnd);
            }
        }

        private void LoadWindow(object sender, RoutedEventArgs e)
        {
            string code = File.ReadAllText($"../../../Compiler/test.pl0");
            Parser parser = new Parser(code);
            parser.Parse();
            ErrorList.ItemsSource = parser.ErrorMsg.Errors;



            RTFDocument doc = new RTFDocument();
            var p = doc.AppendParagraph();
            p.style.alignment = Alignment.Left;
            p.style.indent = new Indent(1, 0, 0);
            p.style.spaceAfter = 400;
            var t = p.AppendText("Boy toy named Troy used to live in Detroit\n");
            t.content += "Big big big money, he was gettin' some coins哈哈";

            t.style.bold = true;
            t.style.color = new Color(255, 0, 0);
            t.style.fontFamily = "Courier";
            string res = doc.ToString();
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(res));
            CodeEditor.Selection.Load(stream, DataFormats.Rtf);
            return;
            TextRange tr = new TextRange(CodeEditor.Document.ContentStart, CodeEditor.Document.ContentEnd);
            tr.Load(stream, DataFormats.Rtf);
        }
    }
}
