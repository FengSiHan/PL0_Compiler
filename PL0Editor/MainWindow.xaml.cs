using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Compiler;
using Microsoft.Win32;
using MahApps.Metro.Controls;
using System.Threading;
using System.IO;
using System.Text;
using System.Windows.Media;

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
            CodeEditor
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "文本文件|*.txt|PL0文件|*.pl0|所有文件|*.*";
            bool? result = dialog.ShowDialog();
            if (result.Value)
            {
                
            }
        }

        private void LoadWindow(object sender, RoutedEventArgs e)
        {
            string code = File.ReadAllText($"../../../Compiler/test.pl0");
            Parser parser = new Parser(code);
            parser.Parse();
            ErrorList.ItemsSource = parser.ErrorMsg.Errors;
        }
        
        
    }
}
