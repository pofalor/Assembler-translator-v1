using SysProgTemplateShared.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SysProgTemplateShared.Exceptions;
using SysProgTemplateShared.Structure;


namespace SysProgTemplateShared.Helpers
{
    public static class Parser
    {
        public static List<List<string>> ParseCode(string input)
        {
            // 1. Split the string into lines
            string[] lines = Regex.Split(input, @"\r?\n");

            var result = new List<List<string>>();

            foreach (string line in lines)
            {
                // Выражение ищет два типа токенов:
                // 1. Строки в кавычках, которые начинаются с C или X, за которыми сразу идут кавычки
                // 2. Отдельные слова без пробелов
                string pattern = @"((?:[CX])""[^""]*(?:""[^""]*)*""|\S+)"; 

                var trimmedAndFilteredWords = Regex.Matches(line, pattern)
                    .Select(s => s.Value)
                    .Select(word => word.Trim())
                    .Where(word => !string.IsNullOrWhiteSpace(word))
                    .ToList();

                if (trimmedAndFilteredWords.Count != 0)
                {
                    result.Add([.. trimmedAndFilteredWords]);
                }
            }

            return result;
        }

        public static List<CommandDto> TextToCommandDtos(string text)
        {
            var lines = Parser.ParseCode(text);

            // check if there is the right amount of chunks in a line  
            foreach (List<string> line in lines)
            {
                if(line.Count != 3)
                    throw new AssemblerException($"Неправильный формат строки: {string.Join(" ", line)}");
            }

            var commandDtos = lines.Select(l => new CommandDto() {
                Name = l[0], 
                Code = l[1], 
                Length = l[2] 
            }).ToList();

            return commandDtos; 
        }

        public static CodeLine ParseCodeLine(List<string> line)
        {
            var textLine = string.Join(" ", line);
            var codeLine = new CodeLine();

            // a code line can only be 1-4 chunks long. 
            if(line.Count < 1 || line.Count >= 4)
            {
                throw new AssemblerException($"Неверный формат команды: {textLine}"); 
            }

            // if begin with a label 



            return codeLine; 
        }
    }
}
