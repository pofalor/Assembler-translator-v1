using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysProgTemplateShared.Structure;
using SysProgTemplateShared.Exceptions;
using System.ComponentModel.DataAnnotations;
using SysProgTemplateShared.Dto;
using SysProgTemplateShared.Helpers;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Reflection.Emit;
using System.Text.Json;


namespace SysProgTemplateShared
{
    public class Assembler 
    {
        private const int maxAddress = 16777215;  // 2^24 - 1  
        private int startAddress = 0;
        private int endAddress = 0;
        private int ip = 0; 

        public List<Command> AvailibleCommands { get; set; } = [
            new Command(){ Name = "JMP", Code = 1, Length = 4 },
            new Command(){ Name = "LOADR1", Code = 2, Length = 4 },
            new Command(){ Name = "LOADR2", Code = 3, Length = 4 },
            new Command(){ Name = "ADD", Code = 4, Length = 2 },
            new Command(){ Name = "SAVER1", Code = 5, Length = 4 },
            new Command(){ Name = "INT", Code = 6, Length = 2 },
        ];

        private readonly string[] AvailibleDirectives = ["START", "END", "WORD", "BYTE", "RESB", "RESW"];

        private readonly List<SymbolicName> TSI = [];


        public void SetAvailibleCommands(List<CommandDto> newAvailibleCommandsDto)
        {
            // try to convert 
            var newAvailibleCommands = newAvailibleCommandsDto.Select(c => new Command(c)).ToList();


            // check Name uniqueness 
            var nhs = new HashSet<string>();
            bool isNameUnique = newAvailibleCommands.All(x => nhs.Add(x.Name.ToUpper()));

            if (!isNameUnique)
                throw new AssemblerException("Все имена команд должны быть уникальными.");


            // check Code uniqueness 
            var chs = new HashSet<int>();
            bool isCodeUnique = newAvailibleCommands.All(x => chs.Add(x.Code));

            if (!isCodeUnique)
                throw new AssemblerException("Все коды команд должны быть уникальными.");

            this.AvailibleCommands = newAvailibleCommands; 
        }

        public List<string> FirstPass(List<List<string>> lines)
        {
            var firstPassCode = new List<string>();

            startAddress = 0; 
            endAddress = 0; 
            ip = 0;                        // command address counter thing (not exactly ip)
            
            bool startFlag = false;         // Was START directive found?  
            bool endFlag = false;           // Was END directive found? 

            foreach (List<string> line in lines)
            {
                var textLine = string.Join(" ", line);
                var firstPassLine = string.Empty;

                if (!startFlag && ip != 0) throw new AssemblerException($"Не найдена директива START в начале программы");

                // if  
                if(startFlag) OverflowCheck(ip, textLine); 

                // if the END directive has already been found in a previous lines, break 
                if (endFlag) break;

                var codeLine = GetCodeLineFromSource(line); 

                // processing label first 
                if(codeLine.Label != null)
                {
                    // try to find label in tsi 
                    if (TSI.Select(w => w.Name.ToUpper()).Contains(codeLine.Label.ToUpper()))
                    {
                        throw new AssemblerException($"Такая метка уже есть в таблице TSI: {textLine}");
                    }
                    else if (startFlag)
                    {
                        PushToTSI(codeLine.Label, ip);
                    }
                }

                // processing command part
                // cannot be null, so no null check needed
                // is it a keyword? 
                if (IsDirective(codeLine.Command))
                {
                    switch (codeLine.Command)
                    {
                        case "START":
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Не было задано значение адреса начала программы, но адрес начала программы не может быть равен нулю (значение по умолчанию): {textLine}");

                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается один операнд, но найдено два: {textLine}");

                                // start should be at the beginning and first 
                                // Исключение вызывается, если пришли сюда ещё раз (после самого старта программы).
                                if (ip != 0 || startFlag) throw new AssemblerException($"START должен быть единственным, в начале исходного кода: {textLine}");

                                // start was found 
                                startFlag = true;

                                // process first operand
                                int address;
                                
                                // check if it is a valid hex value 
                                try
                                {
                                    address = Convert.ToInt32(codeLine.FirstOperand, 10);
                                }
                                catch
                                {
                                    throw new AssemblerException($"Невозможно преобразовать первый операнд в адрес начала программы: {textLine}");
                                }

                                // check if it's within allocated memory bounds  
                                OverflowCheck(address, textLine); 

                                if (address == 0) throw new AssemblerException($"Адрес начала программы не может быть равен нулю: {textLine}");

                                if(codeLine.Label == null) throw new AssemblerException($"Перед директивой START должна быть метка");

                                ip = address;
                                startAddress = address;

                                // output 
                                // X6 - шестнадцатиричное число с 6 символами
                                firstPassLine = $"{codeLine.Label} {codeLine.Command} {address:X6}";
                                break;
                            }

                        case "WORD":
                            // can only contain a 3-byte unsigned int value 
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Ожидается один операнд, но было получено ноль: {textLine}");
                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается один операнд, но найдено два: {textLine}");

                                int value;

                                // try convert 
                                try
                                {
                                    value = Convert.ToInt32(codeLine.FirstOperand, 10);
                                }
                                catch
                                {
                                    throw new AssemblerException($"Невозможно преобразовать первый операнд в число: {textLine}");
                                }

                                // check if within 0-16777215 
                                if (value <= 0 || value > maxAddress) throw new AssemblerException($"Значение первого операнда выходит за границы допустимого диапазона (1-{maxAddress}): {textLine}");

                                // check for allocated memory overflow 
                                OverflowCheck(ip + 3, textLine);

                                firstPassLine = $"{ip:X6} {codeLine.Command} {value:X6}";
                                ip += 3;
                                break;
                            }

                        case "BYTE":
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Ожидается один операнд, но было получено ноль: {textLine}");
                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается один операнд, но найдено два: {textLine}");


                                // try to parse as a 1 byte value 
                                if (int.TryParse(codeLine.FirstOperand, out var value))
                                {
                                    // check if within 0-255 
                                    if (value < 0 || value > 255) throw new AssemblerException($"Значение первого операнда выходит за границы допустимого диапазона (0-255): {textLine}");

                                    // check for allocated memory overflow 
                                    OverflowCheck(ip + 1, textLine);

                                    firstPassLine = $"{ip:X6} {codeLine.Command} {value:X2}";
                                    ip += 1;
                                    break;
                                }
                                // couldnt parse as a numeric value => parse as a character string 
                                else if (IsCString(codeLine.FirstOperand))
                                {
                                    string symbols = codeLine.FirstOperand.Trim('C').Trim('\"');

                                    // check for allocated memory overflow 
                                    OverflowCheck(ip + symbols.Length, textLine); 

                                    firstPassLine = $"{ip:X6} {codeLine.Command} {codeLine.FirstOperand}";
                                    ip += symbols.Length;
                                    break;
                                }
                                else if (IsXString(codeLine.FirstOperand))
                                {
                                    string symbols = codeLine.FirstOperand.Trim('X').Trim('\"');

                                    // check for allocated memory overflow 
                                    OverflowCheck(ip + symbols.Length, textLine);

                                    firstPassLine = $"{ip:X6} {codeLine.Command} {codeLine.FirstOperand.ToUpper()}";
                                    ip += symbols.Length;
                                    break;
                                }
                                else
                                {
                                    throw new AssemblerException($"Невозможно преобразовать первый операнд в символьную или шестнадцатеричную строку: {textLine}");
                                }
                            }

                        case "RESW":
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Ожидается один операнд, но не было получено ни одного: {textLine}");
                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается один операнд, но найдено два: {textLine}");

                                int value;

                                // try convert 
                                try
                                {
                                    value = Convert.ToInt32(codeLine.FirstOperand, 10);
                                }
                                catch
                                {
                                    throw new AssemblerException($"Невозможно преобразовать первый операнд в число: {textLine}");
                                }

                                // check if within 0-255 
                                if (value <= 0 || value > 255) throw new AssemblerException($"Значение первого операнда выходит за границы допустимого диапазона (1-255): {textLine}");

                                // check for allocated memory overflow 
                                OverflowCheck(ip + value * 3, textLine);

                                firstPassLine = $"{ip:X6} {codeLine.Command} {value:X2}";
                                ip += value * 3;
                                break;
                            }

                        case "RESB":
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Ожидается один операнд, но не было получено ни одного: {textLine}");
                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается один операнд, но найдено два: {textLine}");

                                int value;

                                // try convert 
                                try
                                {
                                    value = Convert.ToInt32(codeLine.FirstOperand, 10);
                                }
                                catch
                                {
                                    throw new AssemblerException($"Невозможно преобразовать первый операнд в число: {textLine}");
                                }

                                // check if within 0-255 
                                if (value <= 0 || value > 255) throw new AssemblerException($"Значение первого операнда выходит за границы допустимого диапазона (1-255): {textLine}");

                                // check for allocated memory overflow 
                                OverflowCheck(ip + value, textLine);

                                firstPassLine = $"{ip:X6} {codeLine.Command} {value:X2}";
                                ip += value;
                                break;
                            }

                        case "END":
                            {
                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается максимум один операнд, но найдено два: {textLine}");

                                if (!startFlag) throw new AssemblerException($"Не найдена метка START либо ошибка в директивах START/END: {textLine}");

                                if (endFlag) throw new AssemblerException($"Метка END должна быть только один раз: {textLine}");

                                if (codeLine.FirstOperand == null)
                                {
                                    endAddress = startAddress;
                                }
                                else
                                {
                                    int address;

                                    // check if it is a valid hex value 
                                    try
                                    {
                                        address = Convert.ToInt32(codeLine.FirstOperand, 10);
                                    }
                                    catch
                                    {
                                        throw new AssemblerException($"Невозможно преобразовать первый операнд в адрес входа в программу: {textLine}");
                                    }

                                    if (address < 0 || address > maxAddress) throw new AssemblerException($"Значение первого операнда выходит за границы допустимого диапазона (0-{maxAddress}): {textLine}");

                                    // check if it's within allocated memory bounds  
                                    OverflowCheck(address, textLine); 

                                    endAddress = address;
                                }

                                endFlag = true;
                                break;
                            }
                    }
                }
                // is it a command? 
                else if (IsCommand(codeLine.Command))
                {
                    var command = AvailibleCommands.Find(c => c.Name.ToUpper() == codeLine.Command) ?? throw new AssemblerException($"Не найдена команда: {textLine}");
                    switch (command.Length) 
                    {
                        // length is 1 
                        case 1:
                            {
                                if (codeLine.FirstOperand != null) throw new AssemblerException($"Ожидается ноль операндов: {textLine}");

                                // check for allocated memory overflow 
                                OverflowCheck(ip + 1, textLine);

                                // addressing type 00 
                                firstPassLine = $"{ip:X6} {(command.Code * 4):X2}";

                                ip += 1;
                                break;
                            }

                        // length is 2  
                        // either two registers as two operands
                        // or one 1-byte value 
                        case 2:
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Ожидается минимум один операнд, но было получено ноль: {textLine}");

                                // two registers 
                                if (codeLine.SecondOperand != null)
                                {
                                    if (IsRegister(codeLine.FirstOperand) && IsRegister(codeLine.SecondOperand))
                                    {
                                        // check for allocated memory overflow 
                                        OverflowCheck(ip + 2, textLine);

                                        // addressing type 00 
                                        firstPassLine = $"{ip:X6} {(command.Code * 4):X2} {codeLine.FirstOperand} {codeLine.SecondOperand}";

                                        ip += 2;
                                        break;
                                    }
                                    else
                                    {
                                        throw new AssemblerException($"Неверный формат команды. Ожидалось два регистра: {textLine}");
                                    }
                                }
                                // 1-byte value 
                                else
                                {
                                    int value; 

                                    // try convert 
                                    try
                                    {
                                        value = Convert.ToInt32(codeLine.FirstOperand, 10);
                                    }
                                    catch
                                    {
                                        throw new AssemblerException($"Невозможно преобразовать первый операнд в число: {textLine}");
                                    }

                                    // check if within 0-255
                                    if (value < 0 || value > 255)
                                        throw new AssemblerException($"Значение первого операнда выходит за границы допустимого диапазона (0-255): {textLine}");

                                    // check for allocated memory overflow 
                                    OverflowCheck(ip + 2, textLine); 

                                    // addressing type 00 
                                    firstPassLine = $"{ip:X6} {(command.Code * 4):X2} {value:X2}";
                                    
                                    ip += 2;
                                    break;
                                }
                            }

                        // length 4 
                        case 4:
                            {
                                if (codeLine.FirstOperand == null) throw new AssemblerException($"Ожидается один операнд, но было получено ноль: {textLine}");
                                if (codeLine.SecondOperand != null) throw new AssemblerException($"Ожидается один операнд, но найдено два: {textLine}");

                                // is it a label? 
                                if (IsLabel(codeLine.FirstOperand))
                                {   
                                    // check for allocated memory overflow 
                                    OverflowCheck(ip + 4, textLine);

                                    // addressing type 01 
                                    firstPassLine = $"{ip:X6} {(command.Code * 4 + 1):X2} {codeLine.FirstOperand}"; 

                                    ip += 4;
                                    break;
                                }
                                // is it a parsable 3-byte value? 
                                else if (int.TryParse(codeLine.FirstOperand, out var value))
                                {
                                    if(value < 0 || value > 16777215) throw new AssemblerException($"Недопустимое значение операнда: {textLine}"); 

                                    // check for allocated memory overflow 
                                    OverflowCheck(ip + 4, textLine);

                                    // addressing type 01 
                                    firstPassLine = $"{ip:X6} {(command.Code * 4):X2} {value:X6}";

                                    ip += 4;
                                    break;
                                }
                                else
                                {
                                    throw new AssemblerException($"Недопустимое значение операнда: {textLine}");
                                }
                            }
                    }
                }
                else
                {
                    throw new AssemblerException($"Неизвестная команда: {textLine}"); 
                }

                firstPassCode.Add(firstPassLine);
            }

            if (!endFlag) throw new AssemblerException($"Не найдена точка входа в программу."); 

            return firstPassCode; 
        }

        public List<string> SecondPass(List<List<string>> firstPassCode)
        {
            var secondPassCode = new List<string>();
            CodeLine codeLine;
            string textLine;
            string secondPassLine;

            for (int i = 0; i < firstPassCode.Count;  i++)
            {
                codeLine = GetCodeLineFromFirstPass(firstPassCode[i]);
                textLine = string.Join(" ", firstPassCode[i]);  

                // first line = start directive 
                if (i == 0)
                {
                    // output 
                    secondPassLine = $"H {codeLine.Label} {startAddress:X6} {(ip - startAddress):X6}";
                }
                else
                {
                    // !!! length in half-bytes (?????) basically number of hex digits. 
                    // !!! I'll switch to length in bytes, makes more sense, switch back if needed 
                    //
                    // if WORD + 3-byte value: WORD => 6 (length) + hex value 
                    // if BYTE + 1-byte value: BYTE => 1 (length) + hex value 
                    //    BYTE + string: BYTE => length of the string + string converted to ASCII 
                    // if command + two registers: length of the resulting value + {value = command code * numbers of registers} 
                    // if command + label: length of the resulting value + {value = command code * address from TSI} 
                    // if command + 1-byte value: length of the resulting value + {value = command code * 1-byte value}
                    // if RESB/RESW: length only
                    // if END: E + endAddress

                    switch (codeLine.Command) {

                        // if WORD + 3-byte value: WORD => 6 (length) + hex value 
                        case "WORD":
                            {
                                secondPassLine = $"T {codeLine.Label} {3:X2} {codeLine.FirstOperand:X6}";
                                break; 
                            }

                        // if BYTE + 1-byte value: BYTE => 1 (length) + hex value 
                        //    BYTE + string: BYTE => length of the string + string converted to ASCII 
                        case "BYTE":
                            {
                                try
                                {
                                    int value = Convert.ToInt32(codeLine.FirstOperand, 16);

                                    secondPassLine = $"{"T"} {codeLine.Label} {1:X2} {value:X2}";
                                    break; 
                                }
                                catch
                                {
                                    if (codeLine.FirstOperand == null)
                                    {
                                        throw new AssemblerException($"Встречена пустая строка: {textLine}");
                                    }
                                    if (IsCString(codeLine.FirstOperand))
                                    {
                                        string symbols = codeLine.FirstOperand.Substring(2, codeLine.FirstOperand.Length - 3);

                                        int length = symbols.Length;

                                        secondPassLine = $"{"T"} {codeLine.Label} {length:X2} {ConvertToASCII(symbols)}";
                                        break;
                                    }
                                    else if (IsXString(codeLine.FirstOperand))
                                    {
                                        string symbols = codeLine.FirstOperand.Trim('X').Trim('\"');

                                        int length = symbols.Length;

                                        secondPassLine = $"T {codeLine.Label} {length:X2} {symbols}";
                                        break;
                                    }
                                    else
                                    {
                                        throw new AssemblerException($"Невозможно преобразовать первый операнд в строку: {textLine}");
                                    }
                                }
                            }

                        // if RESB/RESW: length only
                        case "RESB":
                            {
                                int length;

                                // try parse 
                                try
                                {
                                    length = Convert.ToInt32(codeLine.FirstOperand, 16); 
                                }
                                catch
                                {
                                    throw new AssemblerException($"Неыозможно преобразовать операнд в числовое значение {textLine}");
                                }

                                secondPassLine = $"{"T"} {codeLine.Label} {length:X2}";

                                break; 
                            }

                        case "RESW":
                            {
                                int length;

                                // try parse 
                                try
                                {
                                    length = Convert.ToInt32(codeLine.FirstOperand, 16);
                                }
                                catch
                                {
                                    throw new AssemblerException($"Неыозможно преобразовать операнд в числовое значение {textLine}");
                                }

                                secondPassLine = $"{"T"} {codeLine.Label} {(length*3):X2}";

                                break;
                            }

                        // aka command
                        // if command + two registers: length of the resulting value + {value = command code * numbers of registers} 
                        // if command + label: length of the resulting value + {value = command code * address from TSI} 
                        // if command + 1-byte value: length of the resulting value + {value = command code * 1-byte value}
                        default:
                            {
                                // Определяем тип адресации из числового значения команды
                                // Обнуляем все биты кроме последних двух. 
                                int addressingType = (byte)Convert.ToInt32(codeLine.Command, 16) & 0x03;

                                switch (addressingType) 
                                {
                                    // тип адресации 0 (непосредственная)
                                    case 0:
                                        {
                                            if(codeLine.FirstOperand == null && codeLine.SecondOperand == null) // operandless command
                                            {
                                                secondPassLine = $"T {codeLine.Label} {1:X2} {codeLine.Command}";
                                            }
                                            else if(codeLine.SecondOperand != null) // registers 
                                            {
                                                if(codeLine.FirstOperand == null)
                                                {
                                                    throw new AssemblerException($"Первый операнд имеет пустое значение: {textLine}");
                                                } 
                                                secondPassLine = $"T {codeLine.Label} {2:X2} {codeLine.Command}{GetRegisterNumber(codeLine.FirstOperand):X1}{GetRegisterNumber(codeLine.SecondOperand):X1}";
                                            }
                                            else // one operand 
                                            {
                                                if (codeLine.FirstOperand == null)
                                                {
                                                    throw new AssemblerException($"Первый операнд имеет пустое значение: {textLine}");
                                                }
                                                int length = codeLine.FirstOperand.Length / 2;

                                                secondPassLine = $"T {codeLine.Label} {length:X2} {codeLine.Command}{codeLine.FirstOperand}";
                                            }

                                            break;
                                        }
                                    //тип адресации 1 (прямая)
                                    case 1:
                                        {
                                            if (codeLine.FirstOperand == null)
                                            {
                                                throw new AssemblerException($"Первый операнд имеет пустое значение: {textLine}");
                                            }

                                            var symbolicName = GetSymbolicName(codeLine.FirstOperand);    

                                            if(symbolicName == null)
                                            {
                                                throw new AssemblerException($"Метка не найдена в ТСИ: {textLine}");
                                            }
                                            else
                                            {
                                                secondPassLine = $"{"T"} {codeLine.Label} {4:X2} {codeLine.Command}{symbolicName.Address:X6}";
                                            }

                                            break; 
                                        }

                                    default:  
                                        {
                                            throw new AssemblerException($"Неизвестный тип адресации: {textLine}");
                                        }
                                }

                                break;  
                            }
                    }
                }

                secondPassCode.Add(secondPassLine); 
            }
            
            if (endAddress < startAddress || endAddress > ip) throw new AssemblerException($"Некорректный адрес входа в программу: {endAddress:X6}");

            secondPassCode.Add($"E {endAddress:X6}"); 

            return secondPassCode; 
        }


        private void PushToTSI(string symbolicName, int address)
        {
            TSI.Add(new SymbolicName() {
                Name = symbolicName,
                Address = address 
            });
        }

        public void ClearTSI()
        {
            TSI.Clear();
        }

        public string GetFormattedTsiForMain()
        {
            return string.Join("\n", TSI.Select(w => $"{w.Name} {w.Address:X6}"));
        }

        private bool IsCommand(string? chunk)
        {
            if(chunk == null) return false;  

            return AvailibleCommands.Select(c => c.Name.ToUpper()).Contains(chunk.ToUpper()); 
        } 

        private bool IsDirective(string? chunk)
        {
            if (chunk == null) return false; 

            return AvailibleDirectives.Contains(chunk.ToUpper());
        }

        // is it a label-formatted chunk and is it distinct from commands & directives 
        private bool IsLabel(string? chunk)
        {
            if(chunk == null) return false;

            if (chunk.Length > 10) return false; 

            if (!StringChecker.IsLatinLetter(chunk[0])) return false; 

            if (!chunk.All(c => StringChecker.IsLatinLetterOrDigitOrUnderscore(c))) return false;

            if (IsRegister(chunk.ToUpper())) return false; 

            if (AvailibleCommands.Select(c => c.Name.ToUpper()).Contains(chunk.ToUpper())
                || AvailibleDirectives.Select(c => c.ToUpper()).Contains(chunk.ToUpper())) return false;

            return true; 
        }

        private static bool IsXString(string? chunk)
        {
            if (chunk == null
                || !chunk.StartsWith("X\"", StringComparison.OrdinalIgnoreCase)
                || !chunk.EndsWith('\"'))
                return false;

            string symbols = chunk.Trim('X').Trim('\"').ToUpper();

            if (symbols.Length < 1
                || symbols.Contains('\"')
                || !symbols.All(c => StringChecker.IsDigit(c) || "ABCDEF".Contains(c))
                // true - если длина нечетная, false если четная
                || symbols.Length % 2 != 0
                )
                return false;

            return true;
        }

        private static bool IsCString(string? chunk)
        {
            if (chunk == null
                || !chunk.StartsWith("C\"", StringComparison.OrdinalIgnoreCase)
                || !chunk.EndsWith('\"')
                || chunk.Length < 4)
                return false;

            string symbols = chunk.Substring(1);

            if (symbols.Length < 1
                || symbols.Any(c => c > 127) 
                )
                return false;

            return true; 
        }

        /// <summary>
        /// Регистр должен начинаться с заглавной буквы R после неё следует цифра от 1 до 16 (включительно)
        /// </summary>
        private static bool IsRegister(string? chunk)
        {
            if (chunk == null) return false; 

            return Regex.IsMatch(chunk, @"^R(?:[1-9]|1[0-6])$");
        }

        private static int GetRegisterNumber(string chunk)
        {
            return int.Parse(chunk.Substring(1));
        }
 
        private SymbolicName? GetSymbolicName(string chunk)
        {
            var symbolicName = TSI.Find(n => n.Name.Equals(chunk, StringComparison.CurrentCultureIgnoreCase));

            return symbolicName; 
        }

        private static string? ConvertToASCII(string chunk)
        {
            var result = new StringBuilder();
            byte[] textBytes = Encoding.ASCII.GetBytes(chunk);

            for (int i = 0; i < textBytes.Length; i++)
            {
                result.Append(textBytes[i].ToString("X2"));
            }

            return result.ToString();
        }

        private static void OverflowCheck(int value, string textLine)
        {
            if (value < 0 || value > maxAddress) throw new AssemblerException($"Произошло переполнение выделенной памяти: {textLine}");
        }

        // returns a command object that has nullable parameters (label, first operand and second operand) and a non-nullable command. 
        // guarantees that label & command/directive fit the format. Doesnt check operands 
        // labels & commands/directives are set to upper case 
        private CodeLine GetCodeLineFromSource(List<string> line)
        {
            var textLine = string.Join(" ", line);

            if(line.Count < 1 || line.Count > 4)
                throw new AssemblerException($"Неверный формат команды: {textLine}");

            switch (line.Count) 
            {
                case 1:
                    // Can only be an operand-less command or END 
                    if (IsCommand(line[0]) || line[0].ToUpper() == "END")
                    {
                        return new CodeLine()
                        {
                            Label = null,
                            Command = line[0].ToUpper(), 
                            FirstOperand = null, 
                            SecondOperand = null 
                        };  
                    } 
                    else
                    {
                        throw new AssemblerException($"Неверный формат команды: {textLine}");
                    }

                case 2:
                    // Can be a label and an operand-less command or start/end 
                    if (IsLabel(line[0]) && (IsCommand(line[1]) || line[1].ToUpper() == "START" || line[1].ToUpper() == "END"))
                    {
                        return new CodeLine()
                        {
                            Label = line[0].ToUpper(),
                            Command = line[1].ToUpper(),
                            FirstOperand = null,
                            SecondOperand = null
                        };
                    }
                    // can be a command with one operand
                    // or a keyword with one operand
                    else if (IsCommand(line[0]) || IsDirective(line[0]))
                    {
                        return new CodeLine()
                        {
                            Label = null, 
                            Command = line[0].ToUpper(),
                            FirstOperand = line[1], 
                            SecondOperand = null
                        };
                    }
                    else
                    {
                        throw new AssemblerException($"Неверный формат команды: {textLine}");
                    }

                case 3:
                    // can be a label and a keyword with one operand
                    // can be a command with two operands 
                    if (IsLabel(line[0]) &&
                        (IsCommand(line[1]) || IsDirective(line[1])))
                    {
                        return new CodeLine()
                        {
                            Label = line[0].ToUpper(),
                            Command = line[1].ToUpper(),
                            FirstOperand = line[2], 
                            SecondOperand = null
                        };
                    }
                    else if (IsCommand(line[0]))
                    {
                        return new CodeLine()
                        {
                            Label = null,
                            Command = line[0].ToUpper(),
                            FirstOperand = line[1], 
                            SecondOperand = line[2] 
                        };
                    }
                    else
                    {
                        throw new AssemblerException($"Неверный формат команды: {textLine}");
                    }

                case 4:
                    // can only be a label and a command and two operands 
                    if (IsLabel(line[0])
                        && IsCommand(line[1]))
                    {
                        return new CodeLine()
                        {
                            Label = line[0].ToUpper(),
                            Command = line[1].ToUpper(),
                            FirstOperand = line[2], 
                            SecondOperand = line[3] 
                        };
                    }
                    else
                    {
                        throw new AssemblerException($"Неверный формат команды: {textLine}"); 
                    }

                default:
                    throw new AssemblerException($"Неверный формат команды. Ни один из известных форматов не применим: {textLine}");
            }
        }

        private static CodeLine GetCodeLineFromFirstPass(List<string> line)
        {
            var textLine = string.Join(" ", line);

            if (line.Count < 2 || line.Count > 4)
                throw new AssemblerException($"Неверный формат команды: {textLine}");

            switch (line.Count)
            {
                case 2:
                    {
                        // ip + operand-less command  
                        return new CodeLine()
                        {
                            Label = line[0].ToUpper(),
                            Command = line[1].ToUpper(),
                            FirstOperand = null,
                            SecondOperand = null
                        };
                    }

                case 3:
                    {
                        // ip + command + operand 
                        return new CodeLine()
                        {
                            Label = line[0].ToUpper(),
                            Command = line[1].ToUpper(),
                            FirstOperand = line[2],
                            SecondOperand = null
                        };
                    }

                case 4:
                    {
                        // ip + command + operand1 + operand2 
                        return new CodeLine()
                        {
                            Label = line[0].ToUpper(),
                            Command = line[1].ToUpper(),
                            FirstOperand = line[2],
                            SecondOperand = line[3]
                        };
                    }

                default:
                    throw new AssemblerException($"Неверный формат команды: {textLine}");
            }
        }
    }
}
