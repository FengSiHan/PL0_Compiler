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
            parser = new Parser();
            Temp = new StringBuilder();
            codeCompletion = new CodeCompletion(this);
            InputText = new StringBuilder();

            CodeEditor.ShowLineNumbers = true;
            CodeEditor.Options.HighlightCurrentLine = true;
            CodeEditor.Options.ConvertTabsToSpaces = true;

            //自动折叠扫描
            DispatcherTimer foldingUpdateTimer = new DispatcherTimer();
            foldingManager = FoldingManager.Install(CodeEditor.TextArea);
            foldingStrategy.UpdateFoldings(foldingManager, CodeEditor.Document);
            foldingUpdateTimer.Interval = TimeSpan.FromSeconds(2);
            foldingUpdateTimer.Tick += (i, j) =>
            {
                foldingStrategy.UpdateFoldings(foldingManager, CodeEditor.Document);
            };
            //foldingUpdateTimer.Start();

            //后台代码检查线程
            AnalyzeCodeError();
            DispatcherTimer ErrorUpdateTimer = new DispatcherTimer();
            ErrorUpdateTimer.Interval = TimeSpan.FromSeconds(2);
            ErrorUpdateTimer.Tick += (i, j) =>
            {
                AnalyzeCodeError();
            };
            ErrorUpdateTimer.Start();

            DispatcherTimer CodeAnalysisTimer = new DispatcherTimer();
            CodeAnalysisTimer.Interval = TimeSpan.FromSeconds(2);
            CodeAnalysisTimer.Tick += (i, j) =>
            {
                codeCompletion.Analyze(new string(CodeEditor.Text.ToCharArray()));
            };
            CodeAnalysisTimer.Start();

            //用于重置代码提示功能
            DispatcherTimer ResetTimer = new DispatcherTimer();
            ResetTimer.Interval = TimeSpan.FromSeconds(2);
            ResetTimer.Tick += (i, j) =>
            {
                this.Invoke(() =>
                {
                    try
                    {
                        lock (completionWindow)
                        {
                            if (completionWindow != null && completionWindow.WindowState != WindowState.Normal)
                            {
                                completionWindow.Close();
                                StatusContent.Text = "代码提示重新载入完成";
                            }
                        }
                    }
                    catch
                    {

                    }
                });
            };
            ResetTimer.Start();
        }

        PL0FoldingStrategy foldingStrategy = new PL0FoldingStrategy();
        FoldingManager foldingManager;
        CompletionWindow completionWindow;
        Parser parser;
        CodeCompletion codeCompletion;
        StringBuilder InputText;
        public static int StartIndex { get; private set; }
        public static int Length { get; private set; }

        public void Init()
        {
            IHighlightingDefinition customHighlighting;
            using (Stream stream = new FileStream("../../PL0.xshd", FileMode.Open))
            {
                if (stream is null)
                {
                    MessageBox.Show("PL0 highlight ruleset is lost");
                    this.Close();
                    return;
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
            try
            {
                if (e.Text.Length != 0)
                {
                    if (char.IsLetterOrDigit(e.Text[0]))
                    {
                        if (InputText.Length == 0)
                        {
                            StartIndex = CodeEditor.SelectionStart - 1;
                        }
                        InputText.Append(e.Text);
                        Length = InputText.Length;
                        if (completionWindow == null)
                        {
                            completionWindow = new CompletionWindow(CodeEditor.TextArea);
                        }

                        IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;

                        var symbol = codeCompletion.Symbols;
                        CodeCompletion.Envirment host = null;
                        foreach (var i in symbol)
                        {
                            if (Between(i.Start, i.End, CodeEditor.TextArea.Caret.Line))
                            {
                                host = i;
                                break;
                            }
                        }
                        if (host == null)
                        {
                            host = codeCompletion.Global;
                        }
                        List<CompletionInfo> result = host?.Find(InputText[0]);
                        if (result == null || result.Count == 0)
                        {
                            completionWindow.Close();
                            return;
                        }
                        foreach (var i in result)
                        {
                            var tmp = new CompletionData(i);
                            bool find = false;
                            foreach (var k in data)
                            {
                                if (k.Text == tmp.Text)
                                {
                                    find = true;
                                    break;
                                }
                            }
                            if (!find)
                            {
                                data.Add(tmp);
                            }
                        }
                        completionWindow.CompletionList.SelectItem(InputText.ToString());
                        completionWindow.Closed += delegate
                        {
                            completionWindow = null;
                            InputText.Clear();
                        };
                        try
                        {
                            completionWindow.Show();
                        }
                        catch
                        {
                            completionWindow.Close();
                        }
                    }
                    else
                    {
                        completionWindow?.Close();
                    }
                }
                else
                {
                    completionWindow?.Close();
                }
            }
            catch
            {
                StatusContent.Text = "代码提示模块错误......";
            }
            bool Between(Compiler.Position start, Compiler.Position end, int row)
            {
                if (ReferenceEquals(start, null) || ReferenceEquals(end, null))
                {
                    return false;
                }
                row--;
                return row >= start.Row && row <= end.Row;
            }
        }
        public void CodeEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (e.Text.Length > 0 && completionWindow != null)
                {
                    if (!char.IsLetterOrDigit(e.Text[0]))
                    {
                        completionWindow.CompletionList.RequestInsertion(e);
                    }
                }
            }
            catch
            {

            }
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (CodeEditor.Text.Length > 0)
            {
                e.CanExecute = true;
            }
            else
            {
                e.CanExecute = false;
            }
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
            try
            {
                RowText.Text = CodeEditor.TextArea.Caret.Line.ToString();
                ColText.Text = CodeEditor.TextArea.Caret.Column.ToString();
            }
            catch { }
        }
        private void AnalyzeCodeError()
        {
            try
            {
                string code = CodeEditor.Text;
                parser.Parse(new string(code.ToCharArray()));
                ErrorList.ItemsSource = parser.ErrorMsg.Errors;
            }
            catch { }
            //MessageBox.Show(parser.ErrorMsg.Errors.Count.ToString());
            //MessageBox.Show(((List<ErrorInfo>)ErrorList.ItemsSource).Count.ToString());
        }

        private void ChangeLocation(object sender, MouseButtonEventArgs e)
        {
            try
            {
                RowText.Text = CodeEditor.TextArea.Caret.Line.ToString();
                ColText.Text = CodeEditor.TextArea.Caret.Column.ToString();
            }
            catch { }
        }

        private sealed class VMStartup
        {
            private string Code;
            private VirtualMachine VM;
            private MainWindow Window;
            internal VMStartup(VirtualMachine vm, string code, MainWindow window)
            {
                VM = vm;
                Code = code;
                Window = window;
                VM.SetInOutFunction(window.Ctrl_Read, window.Ctrl_Write, window.Ctrl_Write);
            }
            internal void Execute()
            {
                try
                {
                    Window.Invoke(() =>
                    {
                        Window.ExecuteMI.IsEnabled = false;
                        Window.StopMI.IsEnabled = true;
                        Window.StatusContent.Text = "程序开始执行";
                    });
                    VM.Run(Code);
                    Window.Invoke(() =>
                    {
                        Window.ExecuteMI.IsEnabled = true;
                        Window.StopMI.IsEnabled = false;
                        Window.ConsoleCtrl.AppendText("程序成功退出");
                        Window.StatusContent.Text = "程序执行完毕";
                    });
                }
                catch
                { }
            }
        }

        private void ExecuteCode(object sender, RoutedEventArgs e)
        {
            try
            {
                List<ErrorInfo> list = ErrorList.ItemsSource as List<ErrorInfo>;
                if (list.Count > 0)
                {
                    StatusContent.Text = "在执行前请改正所有错误";
                    return;
                }
                ConsoleTab.IsSelected = true;
                string code = CodeEditor.Text;
                VirtualMachine vm = new VirtualMachine();
                VMStartup v = new VMStartup(vm, new string(code.ToCharArray()), this);
                ConsoleThread = new Thread(v.Execute);
                ConsoleThread.Start();
            }
            catch { }
        }
        private void StopExecuteCode(object sender, RoutedEventArgs e)
        {
            try
            {
                ConsoleThread?.Abort();
            }
            catch
            {

            }
            ExecuteMI.IsEnabled = true;
            StopMI.IsEnabled = false;
            StatusContent.Text = "程序终止执行";
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
            try
            {
                if (KeydownHandled)
                {
                    e.Handled = true;
                    return;
                }
                int Line = ConsoleCtrl.GetLineIndexFromCharacterIndex(ConsoleCtrl.SelectionStart);
                int start = ConsoleCtrl.GetCharacterIndexFromLineIndex(Line);
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Left)
                {
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
                if (Key.Enter == e.Key)
                {
                    ConsoleThread.Resume();
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
            catch { }

        }
        bool KeydownHandled = true;
        Thread ConsoleThread;
        private string Ctrl_Read()
        {
            try
            {
                KeydownHandled = false;
                ConsoleThread.Suspend();
                //等待回调
                //开线程，用suspend模拟中断？
                KeydownHandled = true;

                return this.Invoke(() =>
                {
                    string str = ConsoleCtrl.GetLineText(ConsoleCtrl.LineCount - 2);
                    return str.Substring(0, str.Length - 2);
                });
            }
            catch
            {
                return "";
            }
        }
        private void Ctrl_Write(int value)
        {
            try
            {
                int Line = this.Invoke(() => ConsoleCtrl.GetLineIndexFromCharacterIndex(ConsoleCtrl.SelectionStart));
                int start = this.Invoke(() => ConsoleCtrl.GetCharacterIndexFromLineIndex(Line));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(value.ToString());
                this.Invoke(() =>
                {
                    ConsoleCtrl.AppendText(sb.ToString());
                    ConsoleCtrl.SelectionStart = ConsoleCtrl.Text.Length;
                });
            }
            catch { }
        }
        private void Ctrl_Write(string str)
        {
            try
            {
                int Line = ConsoleCtrl.Invoke(() => ConsoleCtrl.GetLineIndexFromCharacterIndex(ConsoleCtrl.SelectionStart));
                int start = ConsoleCtrl.Invoke(() => ConsoleCtrl.GetCharacterIndexFromLineIndex(Line));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(str);
                ConsoleCtrl.Invoke(() =>
                {
                    ConsoleCtrl.AppendText(sb.ToString());
                    ConsoleCtrl.SelectionStart = ConsoleCtrl.Text.Length;
                });
            }
            catch { }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                codeCompletion.Analyze(new string(CodeEditor.Text.ToCharArray()));
            }
            catch { }
        }
    }
}
