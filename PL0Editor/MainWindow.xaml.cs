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
            RowText.DataContext = CodeEditor.TextArea.Caret;
            ColText.DataContext = CodeEditor.TextArea.Caret;
        }
        BraceFoldingStrategy foldingStrategy = new BraceFoldingStrategy();
        FoldingManager foldingManager;
        CompletionWindow completionWindow;


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

        private void LoadWindow(object sender, RoutedEventArgs e)
        {
            string code = File.ReadAllText($"../../../Compiler/test.pl0");
            Parser parser = new Parser(code);
            parser.Parse();
            ErrorList.ItemsSource = parser.ErrorMsg.Errors;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            RowText.Text = CodeEditor.TextArea.Caret.Line.ToString();
            
        }
    }
}
