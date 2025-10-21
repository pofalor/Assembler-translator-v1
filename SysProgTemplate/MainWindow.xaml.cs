using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using SysProgTemplate.Components;
using SysProgTemplateShared.Structure;
using SysProgTemplateShared;
using System.Text.Json;
using SysProgTemplateShared.Exceptions;
using SysProgTemplateShared.Dto;
using SysProgTemplateShared.Helpers;


namespace SysProgTemplate
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Assembler Assembler {get; set; } = new Assembler();

        // Исходный код
        private string SourceCode { get; set; } =
            @"PROG  START   100
    JMP     L1 
B1  WORD    40 
B3  BYTE    C""Hello!""
B4  BYTE    12	
L1  LOADR1  B1	
    LOADR2  B4
    ADD R1  R2
    SAVER1  B1
    INT     200	
    END 
            ";

        private TextBox SourceCodeTextBox { get; set; }

        private TextBox CommandsTextBox { get; set; } 
 
        // Вспомогатеьная таблица 
        private TextBox FirstPassTextBox { get; set; }

        // ТСИ 
        private TextBox TSITextBox { get; set; }

        //Двоичный код 
        private TextBox SecondPassTextBox { get; set; } 

        // Ошибки первого прохода 
        private TextBox FirstPassErrorsTextBox { get; set; }

        // Ошибки второго прохода 
        private TextBox SecondPassErrorsTextBox { get; set; }

        // кнопки 
        private Button FirstPassButton { get; set; }
        private Button SecondPassButton { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            SourceCodeTextBox = this.SourceCode_TextBox;
            SourceCodeTextBox.Text = SourceCode;

            CommandsTextBox = this.Commands_TextBox;
            CommandsTextBox.Text = string.Join("\n", Assembler.AvailibleCommands.Select(c => $"{c.Name} {c.Code} {c.Length}")); 

            FirstPassTextBox = this.FirstPass_TextBox;
            SecondPassTextBox = this.SecondPass_TextBox; 

            TSITextBox = this.TSI_TextBox;

            FirstPassErrorsTextBox = this.FirstPassErrors_TextBox; 
            SecondPassErrorsTextBox = this.SecondPassErrors_TextBox;

            FirstPassButton = this.FirstPass_Button; 
            SecondPassButton = this.SecondPass_Button;
        }

        private void FirstPass_ButtonClick(object sender, RoutedEventArgs e)
        {
           SecondPassButton.IsEnabled = true; 

            try
            {
                TSITextBox.Text = null;
                FirstPassTextBox.Text = null; 
                FirstPassErrorsTextBox.Text = null; 

                //Получаем команды, которые доступны в программе и парсим их в нужный формат
                var newCommands = Parser.TextToCommandDtos(CommandsTextBox.Text);
                Assembler.SetAvailibleCommands(newCommands);

                Assembler.ClearTSI();

                var sourceCode = Parser.ParseCode(SourceCodeTextBox.Text); 
                FirstPassTextBox.Text = string.Join("\n", Assembler.FirstPass(sourceCode));
                TSITextBox.Text = string.Join("\n", Assembler.TSI.Select(w => $"{w.Name} {w.Address.ToString("X6")}")); 
            }
            catch (AssemblerException ex)
            {
                FirstPassErrorsTextBox.Text = $"Ошибка: {ex.Message}"; 
            }

            if (!string.IsNullOrEmpty(FirstPassErrorsTextBox.Text))
            {
                SecondPassButton.IsEnabled = false; 
            }
        }

        private void SecondPass_ButtonClick(object sender, RoutedEventArgs e)
        {
            SecondPassTextBox.Text = null;
            SecondPassErrorsTextBox.Text = null;

            if (FirstPassTextBox.Text == String.Empty) return; 

            try
            {
                var firstPassCode = Parser.ParseCode(FirstPassTextBox.Text); 
                SecondPassTextBox.Text = string.Join("\n", Assembler.SecondPass(firstPassCode));
            }
            catch(AssemblerException ex)
            {
                SecondPassErrorsTextBox.Text = $"Ошибка: {ex.Message}";

            }
        }
    }
}
