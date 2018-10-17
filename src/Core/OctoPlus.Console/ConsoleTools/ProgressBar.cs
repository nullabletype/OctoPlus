using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlus.Console.ConsoleTools {
    internal class ProgressBar : IProgressBar {

        private int status = 0;
        private string[] clocks = new string[] {"\\", "|", "/", "-"};

        public void WriteStatusLine(string status) 
        {
            var builder = new StringBuilder(status);
            while (builder.Length < System.Console.BufferWidth) 
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(status);
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        public void CleanCurrentLine() 
        {
            var builder = new StringBuilder("\r");
            while (builder.Length < System.Console.BufferWidth) 
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(builder.ToString());
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        public void WriteProgress(int current, int total, string message) 
        {
            var builder = new StringBuilder("\r[");
            for (int i = 1; i <= current; i++) 
            {
                builder.Append("█");
            }
            for (int i = current + 1; i <= total; i++) 
            {
                builder.Append("·");
            }
            builder.Append("]");
            if (!string.IsNullOrEmpty(message)) 
            {
                builder.Append($" {clocks[status]} {message}");
            }
            while (builder.Length < System.Console.BufferWidth) 
            {
                builder.Append(" ");
            }

            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(builder.ToString());
            status++;

            if (status > clocks.Length - 1) 
            {
                status = 0;
            }
        }
    }
}
