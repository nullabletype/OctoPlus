using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OctoPlus.Console.ConsoleTools
{
    class InteractiveRunner
    {
        private List<string> Columns;
        private List<string[]> Rows;
        private List<int> Selected;
        private bool Cancelled;

        internal InteractiveRunner(string promptText, params string[] columns)
        {
            Columns = new List<string>(columns);
            Rows = new List<string[]>();
            Selected = new List<int>();
        }

        public void AddRow(params string[] values)
        {
            if (values.Count() != Columns.Count())
            {
                throw new Exception(String.Format(UiStrings.ErrorColumnHeadingMismatch, values.Count(), Columns.Count()));
            }
            Rows.Add(values);
        }

        public void Run()
        {
            bool run = true;
            while (run)
            {
                var newColumns = Columns.ToList();
                newColumns.Insert(0, "#");
                newColumns.Insert(1, "*");
                var table = new ConsoleTable(newColumns.ToArray());

                var rowPosition = 1;

                foreach (var row in Rows)
                {
                    var newRow = row.ToList();
                    newRow.Insert(0, rowPosition.ToString());
                    newRow.Insert(1, Selected.Contains(rowPosition - 1) ? "*" : string.Empty);
                    table.AddRow(newRow.ToArray());
                    rowPosition++;
                }

                table.Write(Format.Minimal);

                System.Console.WriteLine(UiStrings.InteractiveRunnerInstructions);
                var prompt = Prompt.GetString("");

                switch (prompt)
                {
                    case "1":
                        SelectProjectsForDeployment(true);
                        break;
                    case "2":
                        SelectProjectsForDeployment(false);
                        break;
                    case "c":
                        run = false;
                        break;
                    case "e":
                        System.Console.WriteLine(UiStrings.Exiting);
                        Environment.Exit(0);
                        break;
                    default:
                        System.Console.WriteLine(Environment.NewLine);
                        break;
                }
            }
        }

        public IEnumerable<int> GetSelectedIndexes()
        {
            return Selected.ToList();
        }

        private void SelectProjectsForDeployment(bool select)
        {
            var range = GetRangeFromPrompt(Rows.Count());
            foreach (var index in range)
            {
                if (select)
                {
                    if (!Selected.Contains(index-1))
                    {
                        Selected.Add(index-1);
                    }
                }
                else
                {
                    if (Selected.Contains(index-1))
                    {
                        Selected.Remove(index-1);
                    }
                }
            }
        }

        protected IEnumerable<int> GetRangeFromPrompt(int max)
        {
            bool rangeValid = false;
            var intRange = new List<int>();
            while (!rangeValid)
            {
                intRange.Clear();
                var userInput = Prompt.GetString(UiStrings.InteractiveRunnerSelectionInstructions);
                if (string.IsNullOrEmpty(userInput))
                {
                    return new List<int>();
                }
                if (!userInput.All(c => c >= 0 || c <= 9 || c == '-'))
                {
                    continue;
                }
                var segments = userInput.Split(",");
                foreach (var segment in segments)
                {
                    var match = Regex.Match(segment, "([0-9]+)-([0-9]+)");
                    if (match.Success)
                    {
                        var start = Convert.ToInt32(match.Groups[1].Value);
                        var end = Convert.ToInt32(match.Groups[2].Value);
                        if (start > end || end > max)
                        {
                            continue;
                        }
                        intRange.AddRange(Enumerable.Range(start, (end - start) + 1).ToList());
                    }
                    else
                    {
                        var number = 0;
                        if (!Int32.TryParse(segment, out number))
                        {
                            continue;
                        }
                        else
                        {
                            if (number > max || number < 1)
                            {
                                continue;
                            }
                            intRange.Add(number);
                        }
                    }
                }
                rangeValid = true;
            }
            return intRange.Distinct().OrderBy(i => i);
        }
    }

}
