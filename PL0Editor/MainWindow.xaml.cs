using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Compiler;
using Microsoft.Win32;
using MahApps.Metro.Controls;
using System.IO;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Folding;
using System;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using System.Xml;
using System.Windows.Threading;
using System.Threading;
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
            Init();
            SearchPanel.Install(CodeEditor);
            foldingManager = FoldingManager.Install(CodeEditor.TextArea);
            foldingStrategy.UpdateFoldings(foldingManager, CodeEditor.Document);
            DispatcherTimer foldingUpdateTimer = new DispatcherTimer();
            foldingUpdateTimer.Interval = TimeSpan.FromSeconds(2);
            foldingUpdateTimer.Tick += (i, j) =>
            {
                foldingStrategy.UpdateFoldings(foldingManager, CodeEditor.Document);
            };
            foldingUpdateTimer.Start();
            CodeEditor.ShowLineNumbers = true;
            CodeEditor.Options.HighlightCurrentLine = true;
            CodeEditor.Options.ConvertTabsToSpaces = true;


            DispatcherTimer ErrorUpdateTimer = new DispatcherTimer();
            ErrorUpdateTimer.Interval = TimeSpan.FromSeconds(3);
            ErrorUpdateTimer.Tick += (i, j) =>
            {
                AnalyzeCodeError();
            };
            ErrorUpdateTimer.Start();

            parser = new Parser();
        }

        BraceFoldingStrategy foldingStrategy = new BraceFoldingStrategy();
        FoldingManager foldingManager;
        CompletionWindow completionWindow;
        Parser parser;

        public void Init()
        {
            IHighlightingDefinition customHighlighting;
            using (Stream stream = new FileStream("../../PL0.xshd", FileMode.Open))
            {
                if (stream is null)
                {
                    throw new Exception("PL0 highlight ruleset is lost");
                }
                else
                {
                    using (XmlReader reader = new XmlTextReader(stream))
                    {
                        customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.
                            HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                };
            }

            HighlightingManager.Instance.RegisterHighlighting("PL0 Highlighting", new string[] { ".PL0", ".pl0" }, customHighlighting);

            CodeEditor.SyntaxHighlighting = customHighlighting;

            SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);

            CodeEditor.TextArea.TextEntering += CodeEditor_TextArea_TextEntering;
            CodeEditor.TextArea.TextEntered += CodeEditor_TextArea_TextEntered;
        }
        public void CodeEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == ".")
            {
                // open code completion after the user has pressed dot:
                completionWindow = new CompletionWindow(CodeEditor.TextArea);
                // provide AvalonEdit with the data:
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                data.Add(new MyCompletionData("Item1"));
                data.Add(new MyCompletionData("Item2"));
                data.Add(new MyCompletionData("Item3"));
                data.Add(new MyCompletionData("Another item"));
                completionWindow.Show();
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            }
        }
        public void CodeEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            // do not set e.Handled=true - we still want to insert the character that was typed
        }
        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
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

        private void ChangeLocation(object sender, KeyEventArgs e)
        {
            RowText.Text = CodeEditor.TextArea.Caret.Line.ToString();
            ColText.Text = CodeEditor.TextArea.Caret.Column.ToString();
        }
        private void AnalyzeCodeError()
        {
            string code = CodeEditor.Text;
            parser.Parse(new string(code.ToCharArray()));
            ErrorList.ItemsSource = parser.ErrorMsg.Errors;
            //MessageBox.Show(parser.ErrorMsg.Errors.Count.ToString());
            //MessageBox.Show(((List<ErrorInfo>)ErrorList.ItemsSource).Count.ToString());
        }

        private void ChangeLocation(object sender, MouseButtonEventArgs e)
        {
            RowText.Text = CodeEditor.TextArea.Caret.Line.ToString();
            ColText.Text = CodeEditor.TextArea.Caret.Column.ToString();
        }

        private void ExecuteCode(object sender, RoutedEventArgs e)
        {
            string code = CodeEditor.Text;
            VirtualMachine vm = new VirtualMachine();
            vm.Run(new string(code.ToCharArray()), 0);
        }
        private void Ctrl_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy ||
                e.Command == ApplicationCommands.Cut ||
                e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }
        private void Ctrl_PreKeyDown(object sender, KeyEventArgs e)
        {
            if (KeydownHandled)
            {
                e.Handled = true;
                return;
            }
            int Line = ConsoleCtrl.GetLineIndexFromCharacterIndex(ConsoleCtrl.SelectionStart);
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Left)
            {
                int start = ConsoleCtrl.GetCharacterIndexFromLineIndex(Line);
                //判断是否最后一行
                if (start == ConsoleCtrl.SelectionStart)
                {
                    e.Handled = true;
                }
                return;
            }
            if (Line != ConsoleCtrl.LineCount - 1)
            {
                e.Handled = true;
                return;
            }
            /*
            switch (e.Key)
            {
                case Key.NumPad0:
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                    string res = Convert.ToString(e.Key - Key.NumPad0);
                    ConsoleCtrl.SelectionStart++;
                    string text = ConsoleCtrl.Text;
                    ConsoleCtrl.Text = text.Substring(0, ConsoleCtrl.SelectionStart - 2) + res + text.Substring(ConsoleCtrl.SelectionStart - 1);
                    break;
                case Key.D0:
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    ConsoleCtrl.AppendText(Convert.ToString(e.Key - Key.D0));
                    e.Handled = true;
                    break;
            }
            */
        }
        bool KeydownHandled = true;
        private string Ctrl_Read()
        {
            KeydownHandled = false;
            //等待回调
            //开线程，用suspend模拟中断？
            KeydownHandled = true;
            return null;
        }
        private void Ctrl_Write(int value)
        {
            int Line = ConsoleCtrl.GetLineIndexFromCharacterIndex(ConsoleCtrl.SelectionStart);
            int start = ConsoleCtrl.GetCharacterIndexFromLineIndex(Line);
            StringBuilder sb = new StringBuilder();
            if (start != ConsoleCtrl.SelectionStart)
            {
                sb.Append('\n');
            }
            sb.AppendLine(value.ToString());
            ConsoleCtrl.AppendText(sb.ToString());
            ConsoleCtrl.SelectionStart = ConsoleCtrl.Text.Length;
        }
    }
}
