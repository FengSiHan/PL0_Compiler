﻿using System;
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
using Compiler;
using Microsoft.Win32;
using MahApps.Metro.Controls;
namespace PL0Editor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        List<Compiler.ErrorInfo> info = new List<ErrorInfo>();
        public MainWindow()
        {
            InitializeComponent();
            for (int i = 0; i < 10; ++i)
            {
                info.Add(new ErrorInfo(1.ToString(), i, i));
            }
            ErrorList.ItemsSource = info;
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
    }
}
