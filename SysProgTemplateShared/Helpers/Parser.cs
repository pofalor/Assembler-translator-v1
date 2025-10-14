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
                string pattern = @"((?:[CX])""[^""]*(?:""[^""]*)*""|\S+)"; 

                List<string> words = Regex.Matches(line, pattern).Select(s => s.Value).ToList();

                //List<string> words = GetChunks(lineWithoutTabs); 

                var trimmedAndFilteredWords = words
                                                .Select(word => word.Trim())
                                                .Where(word => !string.IsNullOrWhiteSpace(word));

                if (trimmedAndFilteredWords.Count() != 0)
                {
                    result.Add(trimmedAndFilteredWords.ToList());
                }
            }

            return result;
        }

        /*public static List<string> GetChunks(string input)
        {
            var words = new List<string>();

            input = input.Trim(); 

            for (int i = 0; i < input.Length; i++)
            {
                var word = string.Empty; 
                    if ((input[i] == 'C' || input[i] == 'X') && input[i+1] == '\"')
                    {
                        if (input.Last() == '\"')
                        {
                            word = input.Substring(i);
                        }
                        else
                        {
                            throw new Exception();
                        }
                    }
                    else
                    {
                        for (int j = i; j < input.Length; j++)
                        {
                            word += input[j];
                        }
                    }

                words.Add(word);
            }

            return words;
        }*/

        public static List<CommandDto> TextToCommandDtos(string text)
        {
            var lines = Parser.ParseCode(text);

            // check if there is the right amount of chunks in a line  
            foreach (List<string> line in lines)
            {
                if(line.Count != 3)
                    throw new AssemblerException($"Неправильны формат строки: {string.Join(" ", line)}");
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
